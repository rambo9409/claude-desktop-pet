using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class DesktopMovementTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Equal(double expected, double actual, string message) { Check(Math.Abs(expected - actual) < 0.001, message); }
    private static void Set(PetWindow pet, string field, object value) { typeof(PetWindow).GetField(field, Private).SetValue(pet, value); }
    private static object Get(PetWindow pet, string field) { return typeof(PetWindow).GetField(field, Private).GetValue(pet); }
    private static object Call(PetWindow pet, string method, params object[] args) { return typeof(PetWindow).GetMethod(method, Private).Invoke(pet, args); }
    private static PlatformEdge Edge(int handle, double left, double right, double y)
    { return new PlatformEdge { Handle = new IntPtr(handle), Left = left, Right = right, Y = y, Bottom = y + 300, IsFloor = handle == 0 }; }
    private static void Snapshot(PetWindow pet, List<PlatformEdge> platforms, List<Rect> monitors)
    {
        Set(pet, "platformCache", platforms); Set(pet, "platformCacheTime", DateTime.MaxValue);
        Set(pet, "monitorWorkAreas", monitors); Set(pet, "monitorsCachedAt", DateTime.MaxValue);
    }
    private static void Position(PetWindow pet, double center, double feet)
    { pet.Left = center - pet.Width * 0.5; pet.Top = feet - pet.Height * 0.962; }

    private static void CheckRunPaths()
    {
        foreach (Rect work in new[] { new Rect(0, 0, 1920, 1040), new Rect(-1280, 80, 1280, 984), new Rect(0, -1080, 1920, 1040) })
        foreach (double width in new[] { 100.0, 165.0, 450.0 })
        foreach (int direction in new[] { -1, 1 })
        {
            double height = width * 225 / 165, radius = VaresaRunTrack.ContactRadius(width, height);
            Point start = new Point(work.Left + work.Width * 0.5, work.Bottom - radius);
            VaresaRunTrack track = VaresaRunTrack.Screen(work, start, width, height, direction);
            Check(track != null, "Usable monitor rejected a wallrun path.");
            double angle;
            Check((track.At(0, out angle) - start).Length < 0.001, "Screen run starts with a teleport.");
            Check((track.At(track.Length, out angle) - start).Length < 0.001, "A full circuit is discontinuous.");
            Point previous = start;
            bool left = false, right = false, ceiling = false;
            for (int step = 1; step <= 800; step++)
            {
                Point current = track.At(track.Length * step / 800, out angle);
                Check((current - previous).Length <= track.Length / 800 + 0.001, "Corner skips unconsumed movement distance.");
                Check(current.X >= work.Left + radius - 0.001 && current.X <= work.Right - radius + 0.001 &&
                    current.Y >= work.Top + radius - 0.001 && current.Y <= work.Bottom - radius + 0.001, "Wall run escapes its display.");
                left |= Math.Abs(current.X - work.Left - radius) < 0.001;
                right |= Math.Abs(current.X - work.Right + radius) < 0.001;
                ceiling |= Math.Abs(current.Y - work.Top - radius) < 0.001;
                double fit = VaresaRunTrack.Fit(angle);
                Check(fit >= 0.83 && fit <= 1, "Corner fitting excessively shrinks the masked runner.");
                previous = current;
            }
            Check(left && right && ceiling, "The run never visits both walls and the ceiling.");
            double a, b;
            Point large = track.Advance(track.Length * 2 + 37.25, out a);
            Point expected = track.At(37.25, out b);
            Check((large - expected).Length < 0.001, "Large steps lose whole circuits or corner remainder.");
        }
        foreach (int direction in new[] { -1, 1 })
        {
            Rect work = new Rect(-1000, -200, 2000, 1200);
            PlatformEdge window = Edge(91, -200, 200, 300); window.Bottom = 1000;
            double radius = VaresaRunTrack.ContactRadius(165, 225), angle;
            Point start = new Point(direction > 0 ? -500 : 500, 1000 - radius);
            VaresaRunTrack track = VaresaRunTrack.WindowSide(window, work, start, 165, 225, direction, true);
            Check(track != null && (track.At(0, out angle) - start).Length < 0.001, "Approaching a window snaps to its wall.");
            Point landing = track.At(track.Length, out angle);
            Equal(window.Y - radius, landing.Y, "Window ascent does not reach its roof.");
            Check(landing.X > window.Left && landing.X < window.Right, "Window ascent ends outside the roof.");
            double normalized = ((angle % 360) + 360) % 360;
            Check(normalized < 0.001 || normalized > 359.999, "Window ascent ends with a rotated run pose.");
            Point last = start;
            for (int step = 1; step <= 300; step++)
            {
                Point at = track.At(track.Length * step / 300, out angle);
                Check((at - last).Length <= track.Length / 300 + 0.001, "Window turn jumps between arc segments.");
                last = at;
            }
        }
    }

    private static void PrepareSprint(PetWindow pet, Rect work, int direction)
    {
        ((NightsoulEnergy)Get(pet, "nightsoul")).Recover(60, false);
        Call(pet, "ClearVaresaRun"); Call(pet, "CancelWings");
        Set(pet, "currentCharacter", "varesa"); Set(pet, "frames", Call(pet, "LoadCharacterFrames", "varesa"));
        Set(pet, "state", "idle"); Set(pet, "stateEnds", DateTime.MaxValue); Set(pet, "movement", "walk");
        Set(pet, "activeGait", "run"); Set(pet, "direction", direction); Set(pet, "pointerHeld", false);
        Set(pet, "autoWalk", false); Set(pet, "quietMode", true); Set(pet, "runPhase", 0.0);
        Set(pet, "moveSpeed", MovementGait.Speed("varesa", "run", 0.5, pet.Width));
        Set(pet, "targetMoveSpeed", Get(pet, "moveSpeed")); Set(pet, "nextSpeedChange", DateTime.MaxValue);
        Set(pet, "walkEnds", DateTime.MaxValue); Set(pet, "supportHandle", IntPtr.Zero); Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
        Position(pet, work.Left + work.Width / 2, work.Bottom);
        Snapshot(pet, new List<PlatformEdge> { Edge(0, work.Left, work.Right, work.Bottom) }, new List<Rect> { work });
        Call(pet, "UpdateMovement", DateTime.Now, 0.01);
        Check((string)Get(pet, "movement") == "wallrun", "Manual sprint does not enter wallrun with auto disabled / quiet enabled.");
    }

    private static void MoveToCeiling(PetWindow pet)
    {
        VaresaRunTrack track = (VaresaRunTrack)Get(pet, "varesaRunTrack");
        double angle, best = Double.MaxValue, selected = 0;
        for (int step = 0; step < 1000; step++)
        {
            double d = track.Length * step / 1000;
            Point at = track.At(d, out angle);
            if (at.Y < best) { best = at.Y; selected = d; }
        }
        track.Distance = selected;
        Call(pet, "UpdateMovement", DateTime.Now, 0.0);
    }

    private static void CheckVaresaRuntime(PetWindow pet)
    {
        Rect work = new Rect(-1280, -1080, 1280, 1040);
        foreach (int facing in new[] { -1, 1 })
        {
            PrepareSprint(pet, work, facing);
            double radius = VaresaRunTrack.ContactRadius(pet.Width, pet.Height);
            bool left = false, right = false, top = false;
            double speed = (double)Get(pet, "moveSpeed");
            for (int step = 0; step < 240; step++)
            {
                // This suite verifies full-circuit geometry. Real finite
                // energy and forced gliding are covered by NightsoulTests.
                ((NightsoulEnergy)Get(pet, "nightsoul")).Recover(60, false);
                Point previous = new Point(pet.Left, pet.Top);
                double oldPhase = (double)Get(pet, "runPhase");
                Call(pet, "UpdateMovement", DateTime.Now, 0.05);
                Check((string)Get(pet, "movement") == "wallrun", "Ordinary gravity/climb interrupted an active screen run.");
                Check((new Point(pet.Left, pet.Top) - previous).Length <= speed * 0.05 + 0.001, "Real screen run teleports at a corner.");
                Equal(MotionCycle.Advance(oldPhase, speed * 0.05, pet.Width * 0.42), (double)Get(pet, "runPhase"), "Run cycle loses traveled distance at a corner.");
                Point center = new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height - pet.Width / 2);
                left |= Math.Abs(center.X - work.Left - radius) < 0.001;
                right |= Math.Abs(center.X - work.Right + radius) < 0.001;
                top |= Math.Abs(center.Y - work.Top - radius) < 0.001;
            }
            Check(left && right && top, "Real movement did not travel both walls and the top on a negative-origin monitor.");
            MoveToCeiling(pet);
            Set(pet, "pointerHeld", true); Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-16));
            Call(pet, "OnTick", null, EventArgs.Empty); Set(pet, "pointerHeld", false);
            Image sprite = (Image)Get(pet, "sprite");
            Dictionary<string, BitmapSource[]> frames = (Dictionary<string, BitmapSource[]>)Get(pet, "frames");
            Check(Array.IndexOf(frames["walk"], sprite.Source) >= 0, "Wall run stopped using Varesa's masked wheel frames.");
            Check(((Image)Get(pet, "wingLayer")).Visibility == Visibility.Collapsed, "Wall run opens falling wings.");
            Check(sprite.RenderTransformOrigin == new Point(0.5, 0.5) && ((TransformGroup)sprite.RenderTransform).Children.Count == 2,
                "Wall pose was not centered and rotated.");
            Call(pet, "CancelAutomaticTraversal");
            Check((string)Get(pet, "movement") == "fall" && Get(pet, "varesaRunTrack") == null, "Turning off automatic motion leaves Varesa hanging on the ceiling.");
            Check(sprite.RenderTransformOrigin == new Point(0.5, 0.82) && pet.Top >= work.Top, "Cancel leaves an inverted/clipped upright sprite.");
        }
        PrepareSprint(pet, work, 1); MoveToCeiling(pet);
        Call(pet, "StartStrolling");
        Check((string)Get(pet, "movement") == "fall" && (string)Get(pet, "activeGait") == "stroll", "Manual stroll does not cancel ceiling sprint safely.");
        string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet");
        string characterPath = Path.Combine(settings, "character.txt"), modePath = MovementGait.PreferencePath;
        byte[] originalCharacter = File.Exists(characterPath) ? File.ReadAllBytes(characterPath) : null;
        byte[] originalMode = File.Exists(modePath) ? File.ReadAllBytes(modePath) : null;
        try
        {
            PrepareSprint(pet, work, 1); MoveToCeiling(pet);
            Call(pet, "ApplyMovementMode", "stroll");
            Check((string)Get(pet, "movement") == "fall" && (string)Get(pet, "activeGait") == "stroll" && Get(pet, "varesaRunTrack") == null,
                "Persisted slow mode leaves a ceiling runner suspended or sprinting.");
            PrepareSprint(pet, work, 1); MoveToCeiling(pet);
            Call(pet, "SwitchCharacter", "hutao");
            Check((string)Get(pet, "movement") == "fall" && Get(pet, "varesaRunTrack") == null && pet.Top >= work.Top,
                "Changing character retains Varesa's ceiling pose or hovers in midair.");
        }
        finally
        {
            if (originalCharacter == null) { if (File.Exists(characterPath)) File.Delete(characterPath); }
            else File.WriteAllBytes(characterPath, originalCharacter);
            if (originalMode == null) { if (File.Exists(modePath)) File.Delete(modePath); }
            else File.WriteAllBytes(modePath, originalMode);
        }
        PrepareSprint(pet, work, 1); MoveToCeiling(pet);
        Call(pet, "OnPointerDown", pet, new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
        Check((string)Get(pet, "movement") == "held" && Get(pet, "varesaRunTrack") == null && (bool)Get(pet, "dragged"),
            "Grabbing a wall runner does not enter the airborne dragging state immediately.");
        Set(pet, "pointerHeld", false); pet.ReleaseMouseCapture();
        PrepareSprint(pet, work, 1); MoveToCeiling(pet);
        Call(pet, "ResizePet", 1.08, false);
        Check((string)Get(pet, "movement") == "fall" && Get(pet, "varesaRunTrack") == null && pet.Top >= work.Top,
            "Resizing retained a stale path or moved the upright character above the display.");
        Call(pet, "ResizePet", 1.0, true);
        PrepareSprint(pet, work, 1); MoveToCeiling(pet);
        Snapshot(pet, new List<PlatformEdge> { Edge(0, 0, 1920, 1040) }, new List<Rect> { new Rect(0, 0, 1920, 1040) });
        Call(pet, "UpdateMovement", DateTime.Now, 0.05);
        Check(Get(pet, "varesaRunTrack") == null && pet.Left >= 0 && pet.Top >= 0, "Removed monitor left the runner offdesktop.");

        Rect desktop = new Rect(0, 0, 1920, 1040);
        PlatformEdge window = Edge(901, 1100, 1600, 400); window.Bottom = 1040;
        PrepareSprint(pet, desktop, 1);
        Snapshot(pet, new List<PlatformEdge> { Edge(0, 0, 1920, 1040), window }, new List<Rect> { desktop });
        bool climbed = false, roof = false;
        for (int step = 0; step < 100; step++)
        {
            Call(pet, "UpdateMovement", DateTime.Now, 0.025);
            VaresaRunTrack track = (VaresaRunTrack)Get(pet, "varesaRunTrack");
            climbed |= track != null && track.Window != null && track.LandsOnWindow;
            if ((string)Get(pet, "movement") == "walk" && (IntPtr)Get(pet, "supportHandle") == window.Handle) { roof = true; break; }
        }
        Check(climbed && roof, "Floor sprint fails to run up a visible window and return to its roof.");
        // Directly leaving either roof edge uses sprint, never the old random
        // drop / ordinary climb branch.
        foreach (int side in new[] { -1, 1 })
        {
            PrepareSprint(pet, desktop, side);
            Snapshot(pet, new List<PlatformEdge> { Edge(0, 0, 1920, 1040), window }, new List<Rect> { desktop });
            Call(pet, "ClearVaresaRun"); Set(pet, "movement", "walk"); Set(pet, "supportHandle", window.Handle);
            Position(pet, side > 0 ? window.Right - pet.Width * 0.38 - 2 : window.Left + pet.Width * 0.38 + 2, window.Y);
            Call(pet, "UpdateMovement", DateTime.Now, 0.05);
            Check((string)Get(pet, "movement") == "wallrun" && ((VaresaRunTrack)Get(pet, "varesaRunTrack")).Window != null,
                "A window edge uses ordinary climb/fall instead of a masked wall run.");
            Snapshot(pet, new List<PlatformEdge> { Edge(0, 0, 1920, 1040) }, new List<Rect> { desktop });
            Call(pet, "UpdateMovement", DateTime.Now, 0.05);
            Check(Get(pet, "varesaRunTrack") == null && (string)Get(pet, "movement") == "fall", "A closed window retained an invisible running wall.");
        }
        foreach (PlatformEdge cramped in new[] { Edge(903, 1500, 1920, 400), Edge(904, 800, 1300, 40) })
        {
            PrepareSprint(pet, desktop, 1); Call(pet, "ClearVaresaRun");
            Set(pet, "movement", "walk"); Set(pet, "supportHandle", cramped.Handle);
            Snapshot(pet, new List<PlatformEdge> { Edge(0, 0, 1920, 1040), cramped }, new List<Rect> { desktop });
            Position(pet, cramped.Right - pet.Width * 0.18 - 1, cramped.Y);
            Call(pet, "UpdateMovement", DateTime.Now, 0.05);
            Check((string)Get(pet, "movement") == "fall", "Cramped window edge reverts to Varesa's ordinary slow climbing gait.");
        }
        PrepareSprint(pet, desktop, 1); Call(pet, "ClearVaresaRun");
        Rect narrow = new Rect(0, 0, 140, 1000);
        Snapshot(pet, new List<PlatformEdge> { Edge(0, 0, 140, 1000) }, new List<Rect> { narrow });
        Set(pet, "movement", "walk"); Set(pet, "supportHandle", IntPtr.Zero); Set(pet, "direction", 1);
        Position(pet, 110, 1000); Call(pet, "UpdateMovement", DateTime.Now, 0.05);
        Check((string)Get(pet, "movement") == "walk" && (int)Get(pet, "direction") == -1,
            "A display too narrow for a rotated body invokes ordinary climbing instead of turning inward.");
    }

    private static BitmapSource CaptureRunner(PetWindow pet)
    {
        Set(pet, "pointerHeld", true); Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-40));
        Call(pet, "OnTick", null, EventArgs.Empty); Set(pet, "pointerHeld", false);
        FrameworkElement content = (FrameworkElement)pet.Content;
        WpfTestRender.Drain();
        content.Measure(new Size(pet.Width, pet.Height)); content.Arrange(new Rect(0, 0, pet.Width, pet.Height)); content.UpdateLayout();
        RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(pet.Width), (int)Math.Ceiling(pet.Height), 96, 96, PixelFormats.Pbgra32);
        WpfTestRender.Render(bitmap, content); bitmap.Freeze();
        return bitmap;
    }

    private static void SaveImage(BitmapSource image, string path)
    {
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (FileStream file = File.Create(path)) encoder.Save(file);
    }

    private static void DrawLabel(DrawingContext dc, string label, double x, double y, double size)
    {
        dc.DrawText(new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), size, Brushes.WhiteSmoke, 1.0), new Point(x, y));
    }

    private static void WriteRunPreview(PetWindow pet)
    {
        string directory = "artifacts/v21/wallrun-frames"; Directory.CreateDirectory(directory);
        Rect work = new Rect(20, 60, 880, 560);
        PrepareSprint(pet, work, 1);
        for (int frame = 0; frame < 140; frame++)
        {
            Call(pet, "UpdateMovement", DateTime.Now, 0.04);
            BitmapSource character = CaptureRunner(pet);
            DrawingVisual scene = new DrawingVisual();
            using (DrawingContext dc = scene.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(24, 19, 38)), null, new Rect(0, 0, 920, 640));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(34, 27, 49)), new Pen(new SolidColorBrush(Color.FromRgb(160, 115, 200)), 2), work, 6, 6);
                DrawLabel(dc, "瓦雷莎 · 无重力奔跑", 28, 18, 20);
                DrawLabel(dc, "面具 / 风火轮 · 4 倍普通跑速", 590, 22, 14);
                dc.DrawImage(character, new Rect(pet.Left, pet.Top, pet.Width, pet.Height));
            }
            RenderTargetBitmap bitmap = new RenderTargetBitmap(920, 640, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(bitmap, scene);
            SaveImage(bitmap, Path.Combine(directory, frame.ToString("000") + ".png"));
        }
        PrepareSprint(pet, work, 1);
        VaresaRunTrack track = (VaresaRunTrack)Get(pet, "varesaRunTrack");
        DrawingVisual contact = new DrawingVisual();
        using (DrawingContext dc = contact.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(34, 27, 49)), null, new Rect(0, 0, 800, 520));
            for (int cell = 0; cell < 8; cell++)
            {
                double wanted = cell * -45, chosen = 0, best = Double.MaxValue;
                for (int step = 0; step < 10000; step++)
                {
                    double angle, distance = track.Length * step / 10000;
                    track.At(distance, out angle);
                    double difference = Math.Abs(Math.IEEERemainder(angle - wanted, 360));
                    if (difference < best) { best = difference; chosen = distance; }
                }
                track.Distance = chosen; Call(pet, "UpdateMovement", DateTime.Now, 0.0);
                BitmapSource bitmap = CaptureRunner(pet);
                int x = cell % 4 * 200, y = cell / 4 * 260;
                dc.DrawImage(bitmap, new Rect(x + 17, y + 27, pet.Width, pet.Height));
                DrawLabel(dc, wanted.ToString("0") + "°  /  " + VaresaRunTrack.Fit(wanted).ToString("0.00") + "×", x + 20, y + 12, 13);
            }
        }
        RenderTargetBitmap sheet = new RenderTargetBitmap(800, 520, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(sheet, contact);
        SaveImage(sheet, "artifacts/v21/wallrun-contact.png");
        Console.WriteLine("Wrote 140 actual WPF frames and the 8-angle contact sheet in artifacts/v21/.");
    }

    [STAThread]
    public static void Main(string[] args)
    {
        CheckRunPaths();
        List<Rect> monitors = new List<Rect> {
            new Rect(0, 0, 1920, 1040), new Rect(-1280, 80, 1280, 984),
            new Rect(1920, -200, 2560, 1400), new Rect(0, -1080, 1920, 1040) };
        Check(DesktopMovement.WorkAreaForPoint(new Point(-600, 500), monitors) == monitors[1], "Left monitor was replaced by primary.");
        Check(DesktopMovement.WorkAreaForPoint(new Point(2500, -100), monitors) == monitors[2], "Right monitor negative y lost.");
        Check(DesktopMovement.WorkAreaForPoint(new Point(700, -500), monitors) == monitors[3], "Upper monitor not selected.");
        Check(DesktopMovement.WorkAreaForPoint(new Point(-1600, 500), monitors) == monitors[1], "Offdesktop point did not use nearest monitor.");
        foreach (double dpi in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            Matrix matrix = new Matrix(1 / dpi, 0, 0, 1 / dpi, 0, 0);
            Rect work = DesktopMovement.FromPixels(new NativeWindow.RECT { Left = -1920, Top = -100, Right = 0, Bottom = 1080 }, matrix);
            Equal(-1920 / dpi, work.Left, "DPI transform lost negative origin.");
            Equal(1180 / dpi, work.Height, "DPI transform changed work area.");
            Point cursor = matrix.Transform(new Point(-900, 300));
            Point drag = DesktopMovement.DragLocation(cursor, matrix.Transform(new Point(200, 100)), new Point(150, 90));
            Equal(150 - 1100 / dpi, drag.X, "Drag delta crossed DPI coordinate spaces.");
            Equal(90 + 200 / dpi, drag.Y, "Drag y delta crossed DPI coordinate spaces.");
        }
        Point click = new Point(500, 400), window = new Point(430, 340);
        Check(DesktopMovement.DragLocation(click, click, window) == window, "Initial grab snaps pet to an arbitrary body anchor.");
        Equal(-1170, DesktopMovement.DragLocation(new Point(-1100, 500), click, window).X, "Cross-monitor drag was clamped.");

        List<Rect> jumpMonitors = new List<Rect> { new Rect(-1200, 0, 2400, 1100) };
        PlatformEdge source = Edge(1, -150, 150, 700), right = Edge(2, 300, 650, 620), left = Edge(3, -650, -300, 640);
        List<PlatformEdge> targets = new List<PlatformEdge> { source, right, left, Edge(0, -1200, 1200, 1100) };
        Point landing;
        PlatformEdge target = DesktopMovement.ChooseJumpTarget(targets, jumpMonitors, source.Handle, new Point(0, 700), 165, 225, 1, out landing);
        Check(target == right, "Rightward reachable window not chosen.");
        Check(landing.X > right.Left && landing.X < right.Right, "Landing was outside top edge.");
        target = DesktopMovement.ChooseJumpTarget(targets, jumpMonitors, source.Handle, new Point(0, 700), 165, 225, -1, out landing);
        Check(target == left, "Return leap to left window unsupported.");
        Check(DesktopMovement.ChooseJumpTarget(new List<PlatformEdge> { source }, jumpMonitors, source.Handle, new Point(0, 700), 165, 225, 1, out landing) == null,
            "A leap selected its current window.");
        foreach (PlatformEdge invalid in new[] { Edge(2, 800, 1100, 600), Edge(2, 250, 600, 180), Edge(2, 300, 350, 620), Edge(0, 300, 600, 800) })
            Check(DesktopMovement.ChooseJumpTarget(new List<PlatformEdge> { invalid }, jumpMonitors, source.Handle, new Point(0, 700), 165, 225, 1, out landing) == null,
                "Impossible landing was accepted (distance, ceiling, width, or floor).");
        Point start = new Point(-400, 700), end = new Point(200, 600);
        Check(DesktopMovement.JumpPosition(start, end, 0, 100) == start, "Jump start is discontinuous.");
        Check(DesktopMovement.JumpPosition(start, end, 1, 100) == end, "Jump end misses window top.");
        Equal(550, DesktopMovement.JumpPosition(start, end, 0.5, 100).Y, "Jump lacks a parabolic rise.");
        Check(DesktopMovement.SameLanding(right, Edge(2, 300, 650, 620), 400), "Stable landing rejected.");
        Check(!DesktopMovement.SameLanding(right, Edge(2, 300, 650, 640), 400), "Moved window accepted stale landing.");
        Check(!DesktopMovement.SameLanding(right, null, 400), "Closed window accepted stale landing.");
        Check(!DesktopMovement.SameLanding(right, Edge(2, 500, 650, 620), 400), "Occluded landing accepted.");
        List<PlatformEdge> visible = DesktopMovement.VisibleTopSegments(Edge(5, 0, 600, 600), new List<Rect> { new Rect(200, 350, 150, 500) }, 200);
        Check(visible.Count == 2 && visible[0].Right == 200 && visible[1].Left == 350, "Foreground window does not clip hidden landing top.");
        Check(DesktopMovement.VisibleTopSegments(Edge(5, 0, 600, 600), new List<Rect> { new Rect(0, 350, 600, 500) }, 200).Count == 0,
            "Fully hidden window is jumpable.");
        List<Rect> staggered = new List<Rect> { new Rect(0, 0, 1920, 1040), new Rect(1920, 400, 1920, 1040) };
        Point staggeredStart = new Point(1500, 1040), staggeredEnd = new Point(2010, 1340);
        double staggeredHeight = DesktopMovement.JumpHeight(staggeredStart, staggeredEnd, 225, staggered);
        Check(DesktopMovement.BodyInsideDesktop(staggeredStart, 165, 225, staggered) &&
            DesktopMovement.BodyInsideDesktop(staggeredEnd, 165, 225, staggered),
            "Staggered-screen regression must have valid endpoints.");
        Check(!DesktopMovement.BodyInsideDesktop(DesktopMovement.JumpPosition(staggeredStart, staggeredEnd, 0.8, staggeredHeight), 165, 225, staggered),
            "Staggered-screen regression no longer crosses the desktop gap.");
        Check(!DesktopMovement.JumpArcInsideDesktop(staggeredStart, staggeredEnd, 165, 225, staggeredHeight, staggered),
            "A leap between visible endpoints crosses the blank area between staggered screens.");
        PlatformEdge unsafeTarget = Edge(10, 1960, 2350, 1340), safeTarget = Edge(11, 900, 1250, 900);
        Check(DesktopMovement.ChooseJumpTarget(new List<PlatformEdge> { unsafeTarget }, staggered, new IntPtr(1), staggeredStart, 165, 225, 1, out landing) == null,
            "Target selection accepts a reachable but offdesktop arc.");
        Check(DesktopMovement.ChooseJumpTarget(new List<PlatformEdge> { unsafeTarget, safeTarget }, staggered, new IntPtr(1), staggeredStart, 165, 225, 1, out landing) == safeTarget,
            "Target selection does not continue to a safe window after rejecting a gap-crossing arc.");
        List<Rect> aligned = new List<Rect> { new Rect(0, 0, 1920, 1040), new Rect(1920, 0, 1920, 1040) };
        Check(DesktopMovement.BodyInsideDesktop(new Point(1920, 900), 165, 225, aligned),
            "A body spanning two adjacent screens is incorrectly considered offdesktop.");
        Point alignedStart = new Point(1600, 900), alignedEnd = new Point(2100, 900);
        Check(DesktopMovement.JumpArcInsideDesktop(alignedStart, alignedEnd, 165, 225,
            DesktopMovement.JumpHeight(alignedStart, alignedEnd, 225, aligned), aligned),
            "Safe cross-screen jumping is incorrectly blocked.");
        Equal(1, DesktopMovement.ClimbSpeed("ganyu", 165) / DesktopMovement.ClimbSpeed("hutao", 165), "Obsolete Ganyu climbing multiplier remains.");
        Equal(30, DesktopMovement.ClimbSpeed("kolo", 165), "Other character climb speed changed.");

        PetWindow pet = new PetWindow();
        ((DispatcherTimer)Get(pet, "timer")).Stop();
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue); Set(pet, "currentCharacter", "ganyu");
        Set(pet, "autoWalk", true); Set(pet, "quietMode", false); Set(pet, "state", "idle"); Set(pet, "movement", "rest");
        Set(pet, "supportHandle", source.Handle); Set(pet, "direction", 1);
        Snapshot(pet, targets, jumpMonitors); Position(pet, 0, 700);
        Check((bool)Call(pet, "TryStartGanyuJump", true), "Runtime cannot start a reachable jump.");
        Check((string)Get(pet, "movement") == "jump", "Runtime did not enter jump state.");
        for (int tick = 0; tick < 100 && (string)Get(pet, "movement") == "jump"; tick++) Call(pet, "UpdateJump", DateTime.Now, 0.02);
        Check((string)Get(pet, "movement") == "rest", "Jump did not stop at landing.");
        Check((IntPtr)Get(pet, "supportHandle") == right.Handle, "Landing did not bind to target window.");
        Equal(620, pet.Top + pet.Height * 0.962, "Runtime landing feet miss top edge.");
        Check((DateTime)Get(pet, "nextGanyuJump") > DateTime.Now.AddSeconds(5), "Landing immediately retriggers a jump.");

        Position(pet, 0, 700); Set(pet, "supportHandle", source.Handle); Set(pet, "movement", "rest");
        Check((bool)Call(pet, "TryStartGanyuJump", false), "Manual jump failed.");
        Snapshot(pet, new List<PlatformEdge> { source, Edge(2, 300, 650, 660), left }, jumpMonitors);
        Call(pet, "UpdateJump", DateTime.Now, 0.02);
        Check((string)Get(pet, "movement") == "fall", "Moved target did not cancel trajectory.");
        Snapshot(pet, targets, jumpMonitors); Position(pet, 0, 700); Set(pet, "movement", "rest"); Set(pet, "supportHandle", source.Handle);
        Check((bool)Call(pet, "TryStartGanyuJump", false), "Second manual jump failed.");
        Call(pet, "UpdateJump", DateTime.Now, (double)Get(pet, "jumpDuration") * 0.05);
        Check(pet.Top + pet.Height * 0.962 < source.Y && pet.Left + pet.Width * 0.5 < source.Right,
            "Cancelled-jump regression must begin airborne above the launch window.");
        Snapshot(pet, new List<PlatformEdge> { source }, jumpMonitors); Call(pet, "UpdateJump", DateTime.Now, 0.02);
        Check((string)Get(pet, "movement") == "fall", "Closed target did not fall safely.");
        for (int tick = 0; tick < 100 && (string)Get(pet, "movement") == "fall"; tick++) Call(pet, "UpdateFall", 0.02);
        Check((string)Get(pet, "movement") == "rest" && (IntPtr)Get(pet, "supportHandle") == source.Handle,
            "Cancelling a leap early falls through its launch window.");
        Equal(source.Y, pet.Top + pet.Height * 0.962, "Cancelled leap did not land back on its launch window.");
        foreach (string departure in new[] { "walk", "climb" })
        {
            Set(pet, "movement", departure); Set(pet, "supportHandle", source.Handle);
            Call(pet, "BeginFall", 20.0);
            Check((IntPtr)Get(pet, "fallFromHandle") == source.Handle,
                "An ordinary " + departure + " departure no longer ignores the window it is descending past.");
        }

        Snapshot(pet, targets, jumpMonitors); Position(pet, 0, 700); Set(pet, "movement", "rest"); Set(pet, "supportHandle", source.Handle);
        Set(pet, "quietMode", true);
        Check(!(bool)Call(pet, "TryStartGanyuJump", true), "Quiet mode initiated an automatic jump.");
        Check((bool)Call(pet, "TryStartGanyuJump", false), "Quiet mode incorrectly blocks requested manual jump.");
        Set(pet, "movement", "rest"); Set(pet, "quietMode", false); Set(pet, "autoWalk", false);
        Check(!(bool)Call(pet, "TryStartGanyuJump", true), "Disabled auto movement initiated a jump.");
        Set(pet, "currentCharacter", "hutao");
        Check(!(bool)Call(pet, "TryStartGanyuJump", false), "Another character receives Ganyu's leap.");

        Set(pet, "currentCharacter", "ganyu"); Set(pet, "movement", "rest"); Position(pet, 0, 700);
        Snapshot(pet, targets, jumpMonitors);
        Call(pet, "StartClimbing", source, 1, false);
        Check((string)Get(pet, "movement") != "climb", "Ganyu still enters legacy window climbing.");
        Set(pet, "movement", "climb"); Call(pet, "ClearGanyuTraversal");
        Call(pet, "UpdateClimb", DateTime.Now.AddSeconds(0.3), 0.1);
        Check((string)Get(pet, "movement") != "climb", "An old Ganyu climb does not migrate to jumping or gliding.");

        // Several visible segments may share one HWND (occlusion or spanning
        // monitors). Stay attached to the nearest original wall, not the first.
        Set(pet, "currentCharacter", "kolo"); Set(pet, "movement", "rest"); Call(pet, "ClearGanyuTraversal");
        Position(pet, 0, 700); Call(pet, "StartClimbing", source, 1, false);
        Snapshot(pet, new List<PlatformEdge> { Edge(1, -1000, -800, 700), source }, jumpMonitors);
        Call(pet, "UpdateClimb", DateTime.Now.AddSeconds(1), 0.02);
        Check(pet.Left > -100, "Climb jumped to another visible segment of the same window.");

        List<PlatformEdge> floors = new List<PlatformEdge> { Edge(0, 0, 1920, 1040), Edge(0, -1280, 0, 1064) };
        Set(pet, "currentCharacter", "kolo"); Set(pet, "autoWalk", false); Set(pet, "movement", "rest"); Set(pet, "supportHandle", IntPtr.Zero);
        Snapshot(pet, floors, monitors); Position(pet, -600, 1064);
        PlatformEdge support = (PlatformEdge)Call(pet, "FindSupport");
        Check(support != null && support.Left == -1280 && support.Y == 1064, "Secondary floor lookup matched primary's shared zero handle.");
        Position(pet, -600, 950); Set(pet, "movement", "fall"); Set(pet, "fallVelocityX", 0.0); Set(pet, "fallVelocityY", 300.0);
        for (int tick = 0; tick < 40 && (string)Get(pet, "movement") == "fall"; tick++) Call(pet, "UpdateFall", 0.02);
        Equal(1064, pet.Top + pet.Height * 0.962, "Released pet did not land on secondary taskbar.");
        Check(pet.Left < 0, "Released pet snapped back to primary monitor.");
        Position(pet, -600, 1300); Set(pet, "movement", "fall"); Set(pet, "fallVelocityY", 30.0);
        Call(pet, "UpdateFall", 0.02);
        Equal(1064, pet.Top + pet.Height * 0.962, "Below-floor release disappears off monitor.");
        Position(pet, -600, 1064); double footBefore = pet.Top + pet.Height * 0.962;
        Call(pet, "ResizePet", 1.08, false);
        Equal(footBefore, pet.Top + pet.Height * 0.962, "Resize shifts feet off secondary floor.");
        Check(pet.Left < 0, "Resize moved pet to primary monitor.");
        CheckVaresaRuntime(pet);
        if (Array.IndexOf(args, "--preview") >= 0) WriteRunPreview(pet);
        pet.Close();
        Console.WriteLine("PASS: " + checks + " checks for multi-monitor/DPI/drag, jump/climb, Varesa screen/window wall runs, ceiling loops, continuous corners, exact distance, renderer, cancellation and disappearing surfaces.");
    }
}
