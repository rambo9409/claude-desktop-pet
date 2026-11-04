using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class MotionTests
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Set(PetWindow pet, string name, object value)
    {
        typeof(PetWindow).GetField(name, Private).SetValue(pet, value);
    }

    private static object Get(PetWindow pet, string name)
    {
        return typeof(PetWindow).GetField(name, Private).GetValue(pet);
    }

    private static void Tick(PetWindow pet)
    {
        for (int step = 0; step < 24; step++)
        {
            Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-50));
            typeof(PetWindow).GetMethod("OnTick", Private).Invoke(pet, new object[] { null, EventArgs.Empty });
        }
    }

    [STAThread]
    public static void Main()
    {
        try { Run(); }
        catch (Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException)
                Console.Error.WriteLine(current.Message + "\n" + current.StackTrace);
            Environment.ExitCode = 1;
        }
    }

    private static void Run()
    {
        // Two wildly different acceleration schedules must arrive at the same
        // pose after covering the same distance, regardless of elapsed time.
        const double cycle = 69.3;
        double steady = 0, variable = 0;
        for (int i = 0; i < 100; i++) steady = MotionCycle.Advance(steady, 1.2, cycle);
        for (int i = 0; i < 50; i++) variable = MotionCycle.Advance(variable, 0.4, cycle);
        for (int i = 0; i < 50; i++) variable = MotionCycle.Advance(variable, 2.0, cycle);
        Require(Math.Abs(steady - variable) < 0.000001, "Changing speed changed the pose at equal distance.");
        Require(MotionCycle.Advance(variable, 0, cycle) == variable, "Stopped feet continue cycling.");

        // Reversing a climb switches artwork order without jumping to a new
        // physical pose; descending retraces the ascent, including cycle wrap.
        foreach (double phase in new[] { 0.01, 0.26, 0.51, 0.76, 0.99 })
        {
            int up = MotionCycle.Frame(phase, 4, false);
            int down = MotionCycle.Frame(phase, 4, true);
            Require(up == 3 - down, "Climb reversal changed physical pose.");
            double roundTrip = MotionCycle.Advance(MotionCycle.Advance(phase, 60, 36), -60, 36);
            Require(Math.Abs(phase - roundTrip) < 0.000001, "Climb reversal failed across cycle boundary.");
        }
        double large = MotionCycle.Advance(0, 40 * (450.0 / 165), 450 * 0.42);
        double small = MotionCycle.Advance(0, 40 * (100.0 / 165), 100 * 0.42);
        Require(Math.Abs(large - small) < 0.000001, "Resizing changed stride timing at scaled speed.");

        foreach (bool screen in new[] { true, false })
        {
            foreach (int side in new[] { -1, 1 })
            {
                int facing = MotionCycle.ClimbFacing(side, screen);
                Require(facing == (screen ? side : -side), "Character faces away from its wall.");
                double left = MotionCycle.ClimbLeft(1000, 165, facing);
                double contact = left + 165 * (facing > 0 ? 400.0 / 512 : 112.0 / 512);
                Require(Math.Abs(contact - 1000) < 0.000001, "Hands do not meet the wall.");
            }
        }

        // Exercise the actual renderer for every character without opening a
        // window or modifying saved preferences. Pause physics only, not frames.
        PetWindow pet = new PetWindow();
        ((DispatcherTimer)Get(pet, "timer")).Stop();
        Set(pet, "pointerHeld", true);
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
        Set(pet, "state", "idle");
        Set(pet, "stateEnds", DateTime.MaxValue);
        Image sprite = (Image)Get(pet, "sprite");
        ((NotebookState)Get(pet, "notebook")).Detach(); // Empty-handed gait; held-book mapping has its own suite.
        foreach (string character in new[] { "kolo", "hutao", "ganyu", "varesa" })
        {
            Dictionary<string, BitmapSource[]> frames = (Dictionary<string, BitmapSource[]>)
                typeof(PetWindow).GetMethod("LoadCharacterFrames", Private).Invoke(pet, new object[] { character });
            Set(pet, "frames", frames);
            for (int pose = 0; pose < 4; pose++)
                Require(ReferenceEquals(frames["climb-down"][pose], frames["climb-up"][3 - pose]) && frames["climb-up"][pose].IsFrozen,
                    character + " must reuse immutable climbing poses in reverse.");
            Set(pet, "currentCharacter", character);
            Set(pet, "movement", "walk");
            Set(pet, "runPhase", 0.32);
            Set(pet, "moveSpeed", 95.0);
            Tick(pet);
            Require(ReferenceEquals(sprite.Source, frames["walk"][1]), character + " uses elapsed time for run pose.");
            Set(pet, "moveSpeed", 140.0);
            Tick(pet);
            Require(ReferenceEquals(sprite.Source, frames["walk"][1]), character + " skips pose when speed changes.");
            Require(((TransformGroup)sprite.RenderTransform).Children.Count == 1,
                character + " still has a synthetic run bounce.");

            Set(pet, "movement", "climb");
            Set(pet, "climbPhase", 0.51);
            Set(pet, "climbSide", 1);
            Set(pet, "climbIsScreen", false);
            Set(pet, "climbVerticalDirection", -1);
            Tick(pet);
            Require(ReferenceEquals(sprite.Source, frames["climb-up"][2]), character + " has wrong up pose.");
            TransformGroup transform = (TransformGroup)sprite.RenderTransform;
            Require(transform.Children.Count == 1 && ((ScaleTransform)transform.Children[0]).ScaleX == -1,
                character + " window climbing faces away or has synthetic tilt.");
            Set(pet, "climbVerticalDirection", 1);
            Tick(pet);
            Require(ReferenceEquals(sprite.Source, frames["climb-down"][1]), character + " jumps pose when reversing.");

            Set(pet, "movement", "fall");
            Tick(pet);
            Require(character == "kolo" ? ReferenceEquals(sprite.Source, frames["idle"][0]) :
                Array.IndexOf(frames["glide"], sprite.Source) >= 3 && Array.IndexOf(frames["glide"], sprite.Source) <= 5,
                character + " does not use the proper falling/gliding pose.");
        }
        pet.Close();
        Console.WriteLine("PASS: distance-driven gait, speed changes, scale, climb reversal, all wall directions, four character renderers, falling pose.");
    }
}
