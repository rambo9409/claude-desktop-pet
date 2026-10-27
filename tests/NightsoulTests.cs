using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class NightsoulTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Near(double expected, double actual, string message)
    { Check(Math.Abs(expected - actual) < 0.0001, message + " expected=" + expected + " actual=" + actual); }
    private static object Get(PetWindow pet, string field) { return typeof(PetWindow).GetField(field, Private).GetValue(pet); }
    private static void Set(PetWindow pet, string field, object value) { typeof(PetWindow).GetField(field, Private).SetValue(pet, value); }
    private static object Call(PetWindow pet, string method, params object[] args)
    { return typeof(PetWindow).GetMethod(method, Private).Invoke(pet, args); }
    private static NightsoulEnergy Meter(PetWindow pet) { return (NightsoulEnergy)Get(pet, "nightsoul"); }
    private static PlatformEdge Floor(Rect work)
    { return new PlatformEdge { Left = work.Left, Right = work.Right, Y = work.Bottom, Bottom = work.Bottom, IsFloor = true }; }

    private static void CheckReservoir()
    {
        NightsoulEnergy energy = new NightsoulEnergy();
        Near(1, energy.Charge, "New reservoir is not full.");
        Near(30, energy.SpendDistance(30, 100), "Consumption does not use actual distance.");
        energy.Recover(50, true); Near(0.7, energy.Charge, "Active sprint recharges while consuming.");
        energy.Recover(6, false); Near(0.8, energy.Charge, "Partial recharge is not linear over a minute.");
        Near(80, energy.SpendDistance(900, 100), "A large step overspends the remaining resource.");
        Check(energy.Locked && !energy.CanSprint, "Empty resource does not lock sprint.");
        energy.Recover(59, false);
        Check(energy.Locked && !energy.CanSprint, "Sprint unlocks before all energy has regenerated.");
        Near(0, energy.SpendDistance(1, 100), "A locked partial charge can be spent.");
        energy.Recover(1, false);
        Check(!energy.Locked && energy.CanSprint, "The 60-second recovery does not unlock sprint.");
        Near(1, energy.Charge, "Recovery is not exactly full after 60 seconds.");
        energy.Recover(2000, false); Near(1, energy.Charge, "Long recovery overfills the bar.");
        Near(0, energy.SpendDistance(-100, 100), "Negative travel creates energy.");
        foreach (Rect work in new[] { new Rect(0, 0, 1920, 1040), new Rect(-1440, -920, 1440, 860), new Rect(400, 120, 2560, 1400) })
        foreach (double width in new[] { 100.0, 165.0, 450.0 })
        {
            double height = width * 225 / 165, inset = VaresaRunTrack.ContactRadius(width, height);
            double radius = Math.Min(width * 0.28, Math.Min(work.Width - 2 * inset, work.Height - 2 * inset) / 4);
            double approach = inset + radius - width * 0.18;
            Point start = new Point(work.Left + inset + radius, work.Bottom - inset);
            VaresaRunTrack path = VaresaRunTrack.Screen(work, start, width, height, -1);
            double rotation;
            Point end = path.At(NightsoulEnergy.RouteDistance(work, width, height) - approach, out rotation);
            Near(work.Left + work.Width / 2, end.X, "Full budget does not reach the upper midpoint.");
            Near(work.Top + inset, end.Y, "Budget ends below the screen top.");
            Near(NightsoulEnergy.RouteDistance(work, width, height),
                NightsoulEnergy.RouteDistance(new Rect(0, 0, work.Width, work.Height), width, height),
                "Negative monitor origins change resource capacity.");
        }
    }

    private static void Prepare(PetWindow pet, Rect work, int direction, bool leftEdge)
    {
        Call(pet, "ClearVaresaRun"); Call(pet, "CancelWings");
        Meter(pet).Recover(60, false);
        Set(pet, "nightsoulSpentThisTick", false); Set(pet, "nightsoulUpdatedAt", DateTime.MinValue);
        Set(pet, "currentCharacter", "varesa"); Set(pet, "frames", Call(pet, "LoadCharacterFrames", "varesa"));
        Set(pet, "state", "idle"); Set(pet, "stateEnds", DateTime.MaxValue); Set(pet, "movement", "walk");
        Set(pet, "activeGait", "run"); Set(pet, "direction", direction); Set(pet, "pointerHeld", false);
        Set(pet, "autoWalk", false); Set(pet, "quietMode", true); Set(pet, "runPhase", 0.0);
        Set(pet, "moveSpeed", 470.0); Set(pet, "targetMoveSpeed", 470.0); Set(pet, "nextSpeedChange", DateTime.MaxValue);
        Set(pet, "walkEnds", DateTime.MaxValue); Set(pet, "supportHandle", IntPtr.Zero); Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
        Set(pet, "platformCache", new List<PlatformEdge> { Floor(work) }); Set(pet, "platformCacheTime", DateTime.MaxValue);
        Set(pet, "monitorWorkAreas", new List<Rect> { work }); Set(pet, "monitorsCachedAt", DateTime.MaxValue);
        pet.Left = work.Left + (leftEdge ? pet.Width * 0.18 : work.Width / 2) - pet.Width / 2;
        pet.Top = work.Bottom - pet.Height * 0.962;
    }

    private static void CheckRuntime(PetWindow pet)
    {
        Rect work = new Rect(-1440, -960, 1440, 900);
        foreach (double width in new[] { 100.0, 165.0, 450.0 })
        foreach (int direction in new[] { -1, 1 })
        {
            pet.Width = width; pet.Height = width * 225 / 165;
            Prepare(pet, work, direction, true);
            bool usedLeftWall = false, usedRightWall = false;
            double inset = VaresaRunTrack.ContactRadius(pet.Width, pet.Height);
            for (int tick = 0; tick < 400 && !Meter(pet).Locked; tick++)
            {
                Call(pet, "UpdateMovement", DateTime.Now, 0.01);
                double center = pet.Left + pet.Width / 2;
                usedLeftWall |= Math.Abs(center - work.Left - inset) < 0.0001;
                usedRightWall |= Math.Abs(center - work.Right + inset) < 0.0001;
            }
            Check(Meter(pet).Locked, "Actual left-bottom run never exhausts its meter.");
            Check(usedLeftWall && !usedRightWall, "Corner launch reverses into the opposite screen wall.");
            Near(work.Left + work.Width / 2, pet.Left + pet.Width / 2, "Actual bottom-left route exhausts before the upper midpoint.");
            Check((string)Get(pet, "movement") == "fall" && Get(pet, "varesaRunTrack") == null,
                "Exhausted ceiling runner remains attached to the ceiling.");
            Check(((GlideAnimation)Get(pet, "glideAnimation")).Phase == GlidePhase.Deploy,
                "Exhaustion does not begin wing deployment immediately.");
            double ceilingWindowTop = work.Top + inset - pet.Height + pet.Width / 2;
            Check(Math.Abs(pet.Top - ceilingWindowTop) < 1,
                "Ceiling exhaustion teleports the body instead of preserving its height while wings open.");
            Check(pet.Top + (double)Call(pet, "GlidingTopInset") >= work.Top,
                "Gliding artwork clips above the screen after ceiling exhaustion.");
            Check((double)Get(pet, "moveSpeed") <= 140 * Math.Max(0.6, width / 165), "Empty resource retains super speed.");
        }
        pet.Width = 165; pet.Height = 225;

        // An energy-limited ground tick carries its remaining time into the
        // ordinary gait; it neither overspends nor stalls for a whole frame.
        Prepare(pet, work, 1, false);
        double capacity = NightsoulEnergy.RouteDistance(work, pet.Width, pet.Height);
        Meter(pet).SpendDistance(capacity - 5, capacity);
        double before = pet.Left;
        Call(pet, "UpdateMovement", DateTime.Now, 0.05);
        Near(5 + 117.5 * (0.05 - 5.0 / 470), pet.Left - before, "Ground exhaustion drops the unused part of its tick.");
        Check((string)Get(pet, "movement") == "walk", "Ground exhaustion falls through the floor.");
        Check((string)typeof(PetWindow).GetProperty("ActiveMovementFrameKey", Private).GetValue(pet, null) == "stroll",
            "Recovery keeps the masked windwheel pose.");
        for (int tick = 0; tick < 30; tick++) Call(pet, "UpdateMovement", DateTime.Now, 0.02);
        Check((string)Get(pet, "movement") != "wallrun", "Empty reservoir can restart super running.");

        DateTime now = new DateTime(2026, 10, 26, 12, 0, 0);
        Call(pet, "UpdateNightsoul", now, 0.01);
        Call(pet, "UpdateNightsoul", now.AddSeconds(59), 0.01);
        Check(Meter(pet).Locked, "Runtime recovery unlocks early.");
        Set(pet, "currentCharacter", "kolo");
        Call(pet, "UpdateNightsoul", now.AddSeconds(60), 0.01);
        Check(Meter(pet).CanSprint, "Switching character prevents the 60-second recovery.");
        Near(1, Meter(pet).Charge, "The independent character reservoir loses its full recharge.");

        // Exhaust while already ascending, well before the ceiling.
        Prepare(pet, work, -1, true);
        while ((string)Get(pet, "movement") != "wallrun") Call(pet, "UpdateMovement", DateTime.Now, 0.01);
        VaresaRunTrack track = (VaresaRunTrack)Get(pet, "varesaRunTrack");
        track.Distance = pet.Width * 0.28 * Math.PI / 2 + 120;
        Call(pet, "UpdateMovement", DateTime.Now, 0.0);
        Meter(pet).SpendDistance(Meter(pet).AvailableDistance(capacity) - 3, capacity);
        Call(pet, "UpdateMovement", DateTime.Now, 0.02);
        Check((string)Get(pet, "movement") == "fall" && ((GlideAnimation)Get(pet, "glideAnimation")).Phase == GlidePhase.Deploy,
            "Depletion halfway up a wall does not release into gliding.");

        // A tiny remaining charge and a very large simulated tick must not
        // leak energy or continue to the end of a long surface track.
        Prepare(pet, work, 1, false);
        Meter(pet).SpendDistance(capacity - 1, capacity);
        Call(pet, "UpdateMovement", DateTime.Now, 0.05);
        Near(0, Meter(pet).Charge, "Energy overshoots below zero.");
        Check(Meter(pet).Locked, "Tiny last charge fails to lock.");

        // Finite window paths must debit only their exact length, then carry
        // the tick's unused time onto the roof. Test both a successful ascent
        // and an energy cutoff during the vertical segment.
        PlatformEdge window = new PlatformEdge { Handle = new IntPtr(900), Left = -500, Right = -100, Y = -520, Bottom = work.Bottom };
        foreach (bool exhaustOnWall in new[] { false, true })
        {
            Prepare(pet, work, 1, false);
            Set(pet, "platformCache", new List<PlatformEdge> { Floor(work), window });
            pet.Left = -850 - pet.Width / 2;
            Point start = new Point(-850, work.Bottom - VaresaRunTrack.ContactRadius(pet.Width, pet.Height));
            VaresaRunTrack ascent = VaresaRunTrack.WindowSide(window, work, start, pet.Width, pet.Height, 1, true);
            Check(ascent != null, "Finite window test did not create an ascent path.");
            Call(pet, "StartVaresaRun", ascent);
            if (exhaustOnWall) Meter(pet).SpendDistance(capacity - 390, capacity);
            Call(pet, "UpdateMovement", DateTime.Now, (ascent.Length + 20) / 470);
            if (exhaustOnWall)
                Check(Meter(pet).Locked && (string)Get(pet, "movement") == "fall" && Get(pet, "varesaRunTrack") == null,
                    "An exhausted finite window track finishes its ascent instead of gliding.");
            else
            {
                Near(1 - (ascent.Length + 20) / capacity, Meter(pet).Charge, "Finite track loses or double charges the roof remainder.");
                Check((string)Get(pet, "movement") == "walk" && (IntPtr)Get(pet, "supportHandle") == window.Handle,
                    "A sufficiently charged ascent fails to land on its window roof.");
            }
        }
    }

    private static void CheckHud(PetWindow pet)
    {
        foreach (Rect work in new[] { new Rect(0, 0, 1920, 1040), new Rect(-1280, -1040, 1280, 984) })
        foreach (double width in new[] { 100.0, 165.0, 450.0 })
        {
            pet.Width = width; pet.Height = width * 225 / 165;
            Prepare(pet, work, 1, false);
            Call(pet, "UpdateMovement", DateTime.Now, 0.001);
            VaresaRunTrack track = (VaresaRunTrack)Get(pet, "varesaRunTrack");
            for (int step = 0; step < 120; step++)
            {
                track.Distance = track.Length * step / 120;
                Call(pet, "UpdateMovement", DateTime.Now, 0.0);
                Call(pet, "UpdateNightsoul", DateTime.Now, 0.0);
                Border hud = (Border)Get(pet, "nightsoulHud");
                double scale = width / 330;
                Rect bounds = new Rect(pet.Left + hud.Margin.Left * scale, pet.Top + hud.Margin.Top * scale,
                    hud.Width * scale, hud.Height * scale);
                Check(bounds.Top >= work.Top - 0.001 && bounds.Bottom <= work.Bottom + 0.001 &&
                    bounds.Left >= work.Left - 0.001 && bounds.Right <= work.Right + 0.001,
                    "Nightsoul HUD clips outside the current monitor at a wall or ceiling.");
                Check(hud.Margin.Top >= 0 && hud.Margin.Top + hud.Height <= 450.001,
                    "Nightsoul HUD clips outside the pet's own transparent window.");
            }
        }
        pet.Width = 165; pet.Height = 225;
    }

    private static void Preview(PetWindow pet)
    {
        string directory = "artifacts/v22/nightsoul-frames"; Directory.CreateDirectory(directory);
        Rect work = new Rect(20, 60, 880, 560);
        Prepare(pet, work, -1, true);
        for (int frame = 0; frame < 240; frame++)
        {
            DateTime now = DateTime.Now;
            Call(pet, "UpdateMovement", now, 0.04);
            Set(pet, "pointerHeld", true); Set(pet, "lastTick", now.AddMilliseconds(-40));
            Set(pet, "nightsoulUpdatedAt", now.AddMilliseconds(-40));
            Call(pet, "OnTick", null, EventArgs.Empty); Set(pet, "pointerHeld", false);
            FrameworkElement content = (FrameworkElement)pet.Content;
            content.Measure(new Size(pet.Width, pet.Height)); content.Arrange(new Rect(0, 0, pet.Width, pet.Height)); content.UpdateLayout();
            RenderTargetBitmap character = new RenderTargetBitmap(165, 225, 96, 96, PixelFormats.Pbgra32); character.Render(content);
            DrawingVisual scene = new DrawingVisual();
            using (DrawingContext dc = scene.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(24, 19, 38)), null, new Rect(0, 0, 920, 640));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(34, 27, 49)),
                    new Pen(new SolidColorBrush(Color.FromRgb(160, 115, 200)), 2), work, 6, 6);
                dc.DrawText(new FormattedText("瓦雷莎 · 夜魂奔跑 / 耗尽后展开滑翔翼", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 20, Brushes.WhiteSmoke, 1.0), new Point(28, 18));
                dc.DrawImage(character, new Rect(pet.Left, pet.Top, pet.Width, pet.Height));
            }
            RenderTargetBitmap output = new RenderTargetBitmap(920, 640, 96, 96, PixelFormats.Pbgra32); output.Render(scene);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(output));
            using (FileStream file = File.Create(Path.Combine(directory, frame.ToString("000") + ".png"))) encoder.Save(file);
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException)
                Console.WriteLine(current.GetType().Name + ": " + current.Message + "\n" + current.StackTrace);
            Environment.ExitCode = 1;
        }
    }

    private static void Run(string[] args)
    {
        CheckReservoir();
        PetWindow pet = new PetWindow(); ((DispatcherTimer)Get(pet, "timer")).Stop();
        try
        {
            CheckRuntime(pet); CheckHud(pet);
            if (Array.IndexOf(args, "--preview") >= 0) Preview(pet);
        }
        finally { pet.Close(); }
        Console.WriteLine("PASS: " + checks + " checks for nightsoul path calibration, actual corner launch, distance cutoff, wing deployment, ordinary gait fallback, 60-second recovery, character switching and unclipped HUD across monitor origins / sizes.");
    }
}
