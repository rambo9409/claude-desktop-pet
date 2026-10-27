using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class GlideFallTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static object Get(PetWindow pet, string field) { return typeof(PetWindow).GetField(field, Private).GetValue(pet); }
    private static void Set(PetWindow pet, string field, object value) { typeof(PetWindow).GetField(field, Private).SetValue(pet, value); }
    private static object Call(PetWindow pet, string method, params object[] args) { return typeof(PetWindow).GetMethod(method, Private).Invoke(pet, args); }

    private static void Physics()
    {
        foreach (double width in new[] { 100.0, 165.0, 450.0 })
        foreach (double dt in new[] { 0.01, 0.02, 0.05 })
        foreach (double release in new[] { -220.0, 16.0, 300.0, 520.0 })
        {
            double x = 280, y = release, distance = 0;
            double scale = Math.Max(0.6, width / 165.0);
            for (int i = 0; i < (int)(8 / dt); i++)
            {
                double openness = Math.Min(1, i * dt / GlideAnimation.DeployDuration);
                double step = GlidePhysics.Step(ref x, ref y, openness, width, dt);
                distance += step;
                Check(step <= 140 * scale * dt + 0.001, "Release momentum bypassed the glider speed limit.");
                Check(Math.Abs(x) <= 110 * scale + 0.001, "An open glider retained sprint horizontal momentum.");
                Check(y >= -90 * scale, "An upward throw bypassed the glider catch.");
                if (i * dt > 2) Check(y <= 88 * scale + 0.01, "Deployed wings still fall at free-fall speed.");
            }
            Check(distance > 400 * scale && distance < 730 * scale, "Glider descent is not slow, continuous and bounded.");
            Check(Math.Abs(y - 88 * scale) < 0.01, "Glider did not settle at its cruise descent speed.");
        }
        double a = 280, b = 300;
        Check(GlidePhysics.Step(ref a, ref b, 1, 165, 0) == 0 && a == 280 && b == 300,
            "Zero elapsed time mutated fall physics.");
    }

    private static void Integration(PetWindow pet)
    {
        Rect monitor = new Rect(-1280, -300, 1280, 1300);
        PlatformEdge platform = new PlatformEdge { Handle = new IntPtr(71), Left = -1100, Right = -100, Y = 600, Bottom = 900 };
        PlatformEdge floor = new PlatformEdge { Handle = IntPtr.Zero, Left = monitor.Left, Right = monitor.Right,
            Y = monitor.Bottom, Bottom = monitor.Bottom, IsFloor = true };
        foreach (string character in new[] { "hutao", "ganyu", "varesa", "kolo" })
        {
            Set(pet, "currentCharacter", character);
            Set(pet, "frames", Call(pet, "LoadCharacterFrames", character));
            Call(pet, "CancelWings");
            Set(pet, "state", "idle"); Set(pet, "movement", "rest");
            Set(pet, "supportHandle", IntPtr.Zero);
            Set(pet, "platformCache", new List<PlatformEdge> { platform, floor });
            Set(pet, "platformCacheTime", DateTime.MaxValue);
            Set(pet, "monitorWorkAreas", new List<Rect> { monitor });
            Set(pet, "monitorsCachedAt", DateTime.MaxValue);
            pet.Left = -700; pet.Top = 200 - pet.Height * 0.962;
            Call(pet, "BeginFall", 0.0);
            double seconds = 0, previous = pet.Top;
            while ((string)Get(pet, "movement") == "fall" && seconds < 12)
            {
                Call(pet, "UpdateFall", 0.02);
                Dictionary<string, BitmapSource[]> frames = (Dictionary<string, BitmapSource[]>)Get(pet, "frames");
                Call(pet, "PrepareGlidingFrame", frames["idle"][0], 0.02);
                Check(pet.Top >= previous, character + " unexpectedly rose during normal descent.");
                if (character != "kolo") Check(pet.Top - previous <= ((string)Get(pet, "movement") == "rest" ? 4.8 : 2.8),
                    character + " skipped a gliding descent step.");
                previous = pet.Top; seconds += 0.02;
            }
            Check((string)Get(pet, "movement") == "rest" && (IntPtr)Get(pet, "supportHandle") == platform.Handle,
                character + " missed the visible window while gliding.");
            Check(Math.Abs(pet.Top + pet.Height * 0.962 - 600) < 0.001, "Landing did not align the feet.");
            if (character == "kolo") Check(seconds < 2.5, "Default character without wings gained glider physics.");
            else Check(seconds > 4.3 && seconds < 5.2, character + " still appears to plummet rather than glide.");
        }
    }

    [STAThread]
    public static void Main()
    {
        try { Run(); }
        catch (Exception error) { Console.Error.WriteLine(error.Message); Console.Error.WriteLine(error.StackTrace); Environment.ExitCode = 1; }
    }

    private static void Run()
    {
        Physics();
        PetWindow pet = new PetWindow();
        ((DispatcherTimer)Get(pet, "timer")).Stop();
        try { Integration(pet); }
        finally { pet.Close(); }
        Console.WriteLine("PASS: " + checks + " slow-glide physics, throw momentum, sizes, timer cadence, negative-coordinate monitor and window landing checks.");
    }
}
