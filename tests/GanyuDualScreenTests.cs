using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class GanyuDualScreenTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(PetWindow pet, string name, object value) { typeof(PetWindow).GetField(name, Private).SetValue(pet, value); }
    private static object Get(PetWindow pet, string name) { return typeof(PetWindow).GetField(name, Private).GetValue(pet); }
    private static object Call(PetWindow pet, string name, params object[] args) { return typeof(PetWindow).GetMethod(name, Private).Invoke(pet, args); }

    private sealed class Scenario
    {
        internal string Name;
        internal List<Rect> Monitors;
        internal PlatformEdge Roof;
        internal Point Start;
        internal double Width = 165, Height = 225;
        internal List<GanyuSceneWindow> Scene { get { return new List<GanyuSceneWindow> { new GanyuSceneWindow {
            Handle = Roof.Handle, Bounds = new Rect(Roof.Left, Roof.Y, Roof.Right - Roof.Left, Roof.Bottom - Roof.Y), CanBounce = true } }; } }
        internal List<PlatformEdge> Platforms
        {
            get
            {
                List<PlatformEdge> list = new List<PlatformEdge>();
                foreach (Rect monitor in Monitors) list.Add(new PlatformEdge { IsFloor = true, Left = monitor.Left, Right = monitor.Right, Y = monitor.Bottom, Bottom = monitor.Bottom });
                list.Add(Roof); return list;
            }
        }
    }

    // Geometry comes from the reported two-display arrangement. Apply the
    // same device-to-DIP conversion to the monitor, window and pet geometry.
    private static Scenario Make(string name, double dpi, Point offset, Point start, Rect window, bool staggered)
    {
        double factor = 1 / dpi;
        Func<Rect, Rect> convert = delegate(Rect value) { return new Rect((value.X + offset.X) * factor, (value.Y + offset.Y) * factor, value.Width * factor, value.Height * factor); };
        Rect roof = convert(window);
        return new Scenario { Name = name + " @ " + (dpi * 100).ToString("0", CultureInfo.InvariantCulture) + "% " + offset,
            Width = 165 * factor, Height = 225 * factor,
            Monitors = new List<Rect> { convert(new Rect(0, 0, 2560, 1400)), convert(new Rect(2560, staggered ? 200 : 0, 1920, 1160)) },
            Roof = new PlatformEdge { Handle = new IntPtr(701), Left = roof.Left, Right = roof.Right, Y = roof.Top, Bottom = roof.Bottom },
            Start = new Point((start.X + offset.X) * factor, (start.Y + offset.Y) * factor) };
    }

    private static List<Scenario> Scenarios()
    {
        List<Scenario> scenarios = new List<Scenario>();
        foreach (double dpi in new[] { 1.0, 1.25, 1.5 })
        foreach (Point origin in new[] { new Point(0, 0), new Point(-2560, -180) })
        {
            scenarios.Add(Make("main-display seam side", dpi, origin, new Point(2420, 1400), new Rect(1940, 560, 370, 840), false));
            scenarios.Add(Make("second-display seam side", dpi, origin, new Point(2700, 1160), new Rect(2820, 460, 550, 700), false));
            scenarios.Add(Make("one reachable window face", dpi, origin, new Point(1638.95, 1400), new Rect(1700, 600, 550, 800), false));
            scenarios.Add(Make("thirty pixels from contact", dpi, origin, new Point(2008.95, 1400), new Rect(2100, 600, 450, 800), false));
            scenarios.Add(Make("floating window bottom", dpi, origin, new Point(2008.95, 1400), new Rect(2100, 500, 450, 640), false));
            scenarios.Add(Make("step up to shorter display", dpi, origin, new Point(2420, 1400), new Rect(2800, 500, 550, 620), false));
            scenarios.Add(Make("vertically offset displays", dpi, origin, new Point(2420, 1400), new Rect(2800, 560, 550, 720), true));
        }
        return scenarios;
    }

    private static GanyuTraversal Plan(Scenario scenario)
    { return GanyuTraversal.Plan(scenario.Platforms, scenario.Scene, scenario.Monitors, IntPtr.Zero, scenario.Start, scenario.Width, scenario.Height, 1); }

    private static void CheckRoute(Scenario scenario, GanyuTraversal route)
    {
        Check(route != null, "No accessible window route: " + scenario.Name);
        Check(route.Hops.Count > 0, "No jump segments: " + scenario.Name);
        Point previous = scenario.Start;
        foreach (GanyuHop hop in route.Hops)
        {
            Check((hop.Start - previous).Length < 0.001, "Route teleports between segments: " + scenario.Name);
            Check(GanyuTraversal.ArcClear(hop, scenario.Width, scenario.Height, scenario.Scene, scenario.Monitors), "Route crosses a window or screen gap: " + scenario.Name);
            double minX = Double.MaxValue, maxX = Double.MinValue;
            for (int tick = 0; tick <= 120; tick++)
            {
                Point point = hop.At(tick / 120.0);
                Check(GanyuTraversal.InsideDesktop(GanyuTraversal.Body(point, scenario.Width, scenario.Height), scenario.Monitors), "Visible sprite leaves the desktop: " + scenario.Name);
                minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
                if (tick > 0) Check((point - hop.At((tick - 1) / 120.0)).Length < scenario.Width * 0.25, "Motion contains a position discontinuity: " + scenario.Name);
            }
            Check(maxX - minX > scenario.Width * 0.16, "Wall ascent looks like vertical climbing instead of a visible leap: " + scenario.Name);
            previous = hop.End;
        }
        Check(route.Landing.Handle == scenario.Roof.Handle && Math.Abs(previous.Y - scenario.Roof.Y) < 0.001 &&
            previous.X > scenario.Roof.Left && previous.X < scenario.Roof.Right, "Route does not finish on the reachable window roof: " + scenario.Name);
    }

    private static int Baseline()
    {
        int failed = 0;
        foreach (Scenario scenario in Scenarios())
        {
            GanyuTraversal route = Plan(scenario);
            Console.WriteLine((route == null ? "REJECTED " : "ACCEPTED ") + scenario.Name);
            if (route == null) failed++;
        }
        Console.WriteLine("V2.4 reproduction: " + failed + " / " + Scenarios().Count + " accessible scenarios rejected.");
        return 0;
    }

    private static void Prepare(PetWindow pet, Scenario scenario)
    {
        Call(pet, "ClearGanyuTraversal"); Call(pet, "CancelWings");
        Set(pet, "currentCharacter", "ganyu"); Set(pet, "frames", Call(pet, "LoadCharacterFrames", "ganyu"));
        Set(pet, "state", "idle"); Set(pet, "stateEnds", DateTime.MaxValue); Set(pet, "movement", "rest");
        Set(pet, "quietMode", true); Set(pet, "autoWalk", false); Set(pet, "pointerHeld", false);
        Set(pet, "direction", 1); Set(pet, "supportHandle", IntPtr.Zero);
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue); Set(pet, "nextWalk", DateTime.MaxValue);
        Set(pet, "platformCache", scenario.Platforms); Set(pet, "platformCacheTime", DateTime.MaxValue);
        Set(pet, "monitorWorkAreas", scenario.Monitors); Set(pet, "monitorsCachedAt", DateTime.MaxValue);
        Set(pet, "ganyuSceneWindows", scenario.Scene); Set(pet, "ganyuSceneTimestamp", DateTime.MaxValue);
        pet.Width = scenario.Width; pet.Height = scenario.Height;
        pet.Left = scenario.Start.X - pet.Width / 2; pet.Top = scenario.Start.Y - pet.Height * 0.962;
    }

    private static void RuntimeChecks(PetWindow pet)
    {
        foreach (Scenario scenario in Scenarios())
        {
            Prepare(pet, scenario);
            Check((bool)Call(pet, "TryStartGanyuJump", false), "The production entry rejects an accessible scene: " + scenario.Name);
            DateTime now = DateTime.Now;
            Check((string)Get(pet, "movement") != "jump" || (bool)Call(pet, "GanyuRouteStillValid", now),
                "A newly accepted route cancels before the first tick: " + scenario.Name);
            int airborne = 0;
            for (int frame = 0; frame < 1200; frame++)
            {
                Point before = new Point(pet.Left, pet.Top);
                Call(pet, "UpdateMovement", now.AddMilliseconds(frame * 16), 0.016);
                string movement = (string)Get(pet, "movement");
                Check(movement != "climb", "Production movement falls back to climbing: " + scenario.Name);
                Check((new Point(pet.Left, pet.Top) - before).Length < scenario.Width * 0.3, "Production movement snaps during a route: " + scenario.Name);
                Point foot = new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height * 0.962);
                Check(GanyuTraversal.InsideDesktop(GanyuTraversal.Body(foot, pet.Width, pet.Height), scenario.Monitors), "Production movement leaves the desktop: " + scenario.Name);
                Rect body = GanyuTraversal.Body(foot, pet.Width, pet.Height);
                Rect obstacle = scenario.Scene[0].Bounds, hit = Rect.Intersect(body, obstacle);
                Check(hit.IsEmpty || hit.Width <= 0.5 || hit.Height <= 0.5, "Production movement clips through the target window: " + scenario.Name);
                if (movement == "jump") airborne++;
                if (movement == "rest" && (IntPtr)Get(pet, "supportHandle") == scenario.Roof.Handle) break;
            }
            Check(airborne > 10, "The production entry never performs a visible jump: " + scenario.Name + "; movement=" + Get(pet, "movement") + ", foot=" +
                new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height * 0.962) + ", support=" + Get(pet, "supportHandle"));
            Check((string)Get(pet, "movement") == "rest" && (IntPtr)Get(pet, "supportHandle") == scenario.Roof.Handle,
                "Production movement never reaches its window roof: " + scenario.Name);
            Check(Math.Abs(pet.Top + pet.Height * 0.962 - scenario.Roof.Y) < 0.001, "Production landing misses the target height: " + scenario.Name);
        }
    }

    private static void ApproachAndSeamChecks(PetWindow pet)
    {
        Scenario far = Make("approach from the desktop centre", 1, new Point(0, 0), new Point(1500, 1400), new Rect(2800, 500, 550, 620), false);
        Check(Plan(far) == null, "The approach fixture should begin outside direct jump range.");
        Prepare(pet, far);
        Check((bool)Call(pet, "TryStartGanyuJump", false) && (bool)Get(pet, "ganyuApproaching"), "A distant but reachable window does not start a short run-up.");
        DateTime now = DateTime.Now; int approachFrames = 0, jumpFrames = 0;
        for (int tick = 0; tick < 1500; tick++)
        {
            Point before = new Point(pet.Left, pet.Top);
            bool walking = (bool)Get(pet, "ganyuApproaching");
            Call(pet, "UpdateMovement", now.AddMilliseconds(tick * 16), 0.016);
            Check((new Point(pet.Left, pet.Top) - before).Length < 30, "Run-up or its transition snaps to the jump start.");
            if (walking)
            {
                approachFrames++;
                Check(Math.Abs(pet.Top + pet.Height * 0.962 - far.Start.Y) < 0.001, "Run-up crosses the missing floor below the shorter display.");
            }
            if ((string)Get(pet, "movement") == "jump") jumpFrames++;
            if ((string)Get(pet, "movement") == "rest" && (IntPtr)Get(pet, "supportHandle") == far.Roof.Handle) break;
        }
        Check(approachFrames > 20 && jumpFrames > 20, "The complete approach did not contain both visible running and wall jumping.");
        Check((string)Get(pet, "movement") == "rest" && (IntPtr)Get(pet, "supportHandle") == far.Roof.Handle,
            "Run-up does not finish by landing on the destination window.");

        Prepare(pet, far);
        Check((bool)Call(pet, "TryStartGanyuJump", false) && (bool)Get(pet, "ganyuApproaching"), "Cancellation fixture cannot start its run-up.");
        Call(pet, "UpdateMovement", now, 0.016);
        List<PlatformEdge> floors = far.Platforms; floors.RemoveAt(floors.Count - 1);
        Set(pet, "platformCache", floors); Set(pet, "ganyuSceneWindows", new List<GanyuSceneWindow>());
        Point whenClosed = new Point(pet.Left, pet.Top);
        Call(pet, "UpdateMovement", now.AddMilliseconds(16), 0.016);
        Check(!(bool)Get(pet, "ganyuApproaching") && Get(pet, "ganyuApproachRoute") == null && (string)Get(pet, "movement") == "rest",
            "Closing the target window does not cancel the run-up.");
        Check((new Point(pet.Left, pet.Top) - whenClosed).Length < 0.001, "A cancelled run-up keeps advancing toward a disappeared window.");

        Prepare(pet, far);
        Check((bool)Call(pet, "TryStartGanyuJump", false) && (bool)Get(pet, "ganyuApproaching"), "Movement-mode cancellation fixture cannot start.");
        string preferencePath = MovementGait.PreferencePath;
        byte[] originalPreference = File.Exists(preferencePath) ? File.ReadAllBytes(preferencePath) : null;
        try
        {
            Call(pet, "ApplyMovementMode", "stroll");
            Check(!(bool)Get(pet, "ganyuApproaching") && (string)Get(pet, "movement") == "rest", "Changing to slow walking leaves a hidden run-up active.");
        }
        finally
        {
            if (originalPreference == null) { if (File.Exists(preferencePath)) File.Delete(preferencePath); }
            else File.WriteAllBytes(preferencePath, originalPreference);
        }

        Scenario split = Make("continuous split roof", 1, new Point(0, 0), new Point(2420, 1400), new Rect(2800, 500, 550, 620), false);
        Prepare(pet, split);
        Check((bool)Call(pet, "TryStartGanyuJump", false), "Split-roof fixture cannot launch.");
        GanyuTraversal splitRoute = (GanyuTraversal)Get(pet, "ganyuTraversal");
        double cut = splitRoute.Hops[splitRoute.Hops.Count - 1].End.X;
        List<PlatformEdge> partitioned = split.Platforms; partitioned.RemoveAt(partitioned.Count - 1);
        PlatformEdge firstHalf = new PlatformEdge { Handle = split.Roof.Handle, Left = split.Roof.Left, Right = cut, Y = split.Roof.Y, Bottom = split.Roof.Bottom };
        PlatformEdge secondHalf = new PlatformEdge { Handle = split.Roof.Handle, Left = cut, Right = split.Roof.Right, Y = split.Roof.Y, Bottom = split.Roof.Bottom };
        partitioned.Add(firstHalf); partitioned.Add(secondHalf); Set(pet, "platformCache", partitioned);
        PlatformEdge merged = (PlatformEdge)Call(pet, "GanyuWalkingSupport", firstHalf);
        Check(merged.Left == split.Roof.Left && merged.Right == split.Roof.Right, "Continuous pieces of one cross-display roof do not merge for running.");
        Check((bool)Call(pet, "GanyuRouteStillValid", now), "A harmless repartition of one continuous roof cancels an in-flight landing.");
        secondHalf.Left += 25;
        PlatformEdge interrupted = (PlatformEdge)Call(pet, "GanyuWalkingSupport", firstHalf);
        Check(interrupted.Right == firstHalf.Right, "An occluded gap in a roof is merged into a walkable surface.");
        Check(!(bool)Call(pet, "GanyuRouteStillValid", now.AddMilliseconds(200)), "An actual gap through the intended landing remains accepted.");

        // A seam is an optional jump contact, not a compulsory barrier when
        // no window is available. Walk straight across in both directions.
        foreach (int direction in new[] { -1, 1 })
        {
            Scenario crossing = Make("walk across a same-height seam", 1, new Point(-2560, 100), new Point(2560 - direction * 20, 1400), new Rect(2800, 500, 550, 620), false);
            crossing.Monitors[1] = new Rect(crossing.Monitors[1].Left, crossing.Monitors[1].Top, 1920, 1400);
            Prepare(pet, crossing);
            List<PlatformEdge> emptyFloors = crossing.Platforms; emptyFloors.RemoveAt(emptyFloors.Count - 1);
            Set(pet, "platformCache", emptyFloors); Set(pet, "ganyuSceneWindows", new List<GanyuSceneWindow>());
            Set(pet, "movement", "walk"); Set(pet, "direction", direction);
            Set(pet, "walkEnds", DateTime.MaxValue); Set(pet, "nextSpeedChange", DateTime.MaxValue);
            Set(pet, "moveSpeed", 100.0); Set(pet, "targetMoveSpeed", 100.0); Set(pet, "nextGanyuJump", DateTime.MaxValue);
            double originalY = pet.Top;
            for (int tick = 0; tick < 100; tick++)
            {
                double originalX = pet.Left;
                Call(pet, "UpdateMovement", now.AddMilliseconds(tick * 16), 0.016);
                Check((string)Get(pet, "movement") == "walk" && (int)Get(pet, "direction") == direction, "An empty monitor seam stops or reverses ordinary walking.");
                Check(Math.Abs(pet.Top - originalY) < 0.001 && Math.Abs(pet.Left - originalX - direction * 1.6) < 0.001,
                    "An empty monitor seam changes the walk height or cadence.");
            }
            Check((pet.Left + pet.Width / 2) * direction > 100, "Ordinary walking does not pass the monitor seam.");
        }

        // WPF keeps the configured pet size in DIPs as device DPI changes.
        // Check that realistic fixed logical sizes work too, in addition to
        // the scaled sprite/layout matrix used above.
        foreach (double dpi in new[] { 1.25, 1.5 })
        {
            Scenario fixedDip = Make("fixed-DIP pet at a scaled seam", dpi, new Point(-2560, -180), new Point(2420, 1400), new Rect(1940, 560, 370, 840), false);
            fixedDip.Width = 165; fixedDip.Height = 225;
            CheckRoute(fixedDip, Plan(fixedDip));
        }
    }

    private static BitmapSource CapturePet(PetWindow pet)
    {
        // Advance the real production renderer without another movement tick;
        // the route was advanced explicitly at a fixed timestep above.
        Set(pet, "pointerHeld", true); Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-16));
        Call(pet, "OnTick", null, EventArgs.Empty); Set(pet, "pointerHeld", false);
        FrameworkElement content = (FrameworkElement)pet.Content;
        WpfTestRender.Drain();
        content.Measure(new Size(pet.Width, pet.Height)); content.Arrange(new Rect(0, 0, pet.Width, pet.Height)); content.UpdateLayout();
        RenderTargetBitmap image = new RenderTargetBitmap((int)Math.Ceiling(pet.Width), (int)Math.Ceiling(pet.Height), 96, 96, PixelFormats.Pbgra32);
        WpfTestRender.Render(image, content); image.Freeze(); return image;
    }

    private static void Label(DrawingContext dc, string text, Point point, double size, Brush brush)
    { dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), size, brush, 1), point); }
    private static void Save(BitmapSource bitmap, string path)
    { PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (FileStream file = File.Create(path)) encoder.Save(file); }

    private static void Preview(PetWindow pet)
    {
        Scenario scenario = Make("cross-display preview", 1, new Point(0, 0), new Point(2420, 1400), new Rect(2800, 500, 550, 620), false);
        Prepare(pet, scenario);
        Check((bool)Call(pet, "TryStartGanyuJump", false), "The dual-display preview cannot start.");
        string directory = "artifacts/v25/dual-screen-jump-frames"; Directory.CreateDirectory(directory);
        GanyuTraversal route = (GanyuTraversal)Get(pet, "ganyuTraversal");
        DateTime now = DateTime.Now; int frames = 0, rested = 0;
        while (frames < 500 && rested < 36)
        {
            if ((string)Get(pet, "movement") == "jump") Call(pet, "UpdateMovement", now.AddMilliseconds(frames * 32), 0.032);
            else rested++;
            BitmapSource character = CapturePet(pet);
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                Brush ink = new SolidColorBrush(Color.FromRgb(35, 65, 97));
                Brush muted = new SolidColorBrush(Color.FromRgb(98, 132, 162));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(240, 247, 254)), null, new Rect(0, 0, 1120, 760));
                Label(dc, "甘雨 · 双屏中部蹬跳", new Point(28, 19), 26, ink);
                Label(dc, "真实移动逻辑与角色帧 · 接缝借力 → 跨屏 → 窗口顶部", new Point(30, 55), 15, muted);
                dc.PushClip(new RectangleGeometry(new Rect(20, 90, 1080, 620)));
                TransformGroup world = new TransformGroup(); world.Children.Add(new ScaleTransform(0.55, 0.55));
                world.Children.Add(new TranslateTransform(20 - 1600 * 0.55, 90 - 280 * 0.55)); dc.PushTransform(world);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(225, 238, 250)), new Pen(new SolidColorBrush(Color.FromRgb(137, 170, 198)), 3), scenario.Monitors[0]);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(235, 229, 253)), new Pen(new SolidColorBrush(Color.FromRgb(164, 151, 197)), 3), scenario.Monitors[1]);
                Pen seam = new Pen(new SolidColorBrush(Color.FromRgb(92, 151, 208)), 3); seam.DashStyle = DashStyles.Dash;
                dc.DrawLine(seam, new Point(2560, 280), new Point(2560, 1400));
                Rect window = scenario.Scene[0].Bounds;
                dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(97, 143, 189)), 3), window, 16, 16);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(207, 224, 243)), null, new Rect(window.Left + 2, window.Top + 2, window.Width - 4, 62), 14, 14);
                Label(dc, "可落脚的窗口顶部", new Point(window.Left + 30, window.Top + 17), 24, ink);
                Label(dc, "跨屏时先抬高，避开较高的副屏底边", new Point(window.Left + 26, window.Top + 95), 20, muted);
                Pen path = new Pen(new SolidColorBrush(Color.FromArgb(65, 69, 134, 191)), 2); path.DashStyle = DashStyles.Dot;
                foreach (GanyuHop hop in route.Hops)
                    for (int tick = 1; tick <= 48; tick++) dc.DrawLine(path, hop.At((tick - 1) / 48.0), hop.At(tick / 48.0));
                if ((double)Get(pet, "ganyuContactRemaining") > 0)
                {
                    int index = (int)Get(pet, "ganyuHopIndex"); GanyuWall wall = route.Hops[index].Contact;
                    Point touch = new Point(wall.X, pet.Top + pet.Height * 0.8);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(130, 135, 222, 255)), new Pen(Brushes.White, 3), touch, 23, 23);
                }
                dc.DrawImage(character, new Rect(pet.Left, pet.Top, pet.Width, pet.Height));
                dc.Pop(); dc.Pop();
                Label(dc, "主屏 · 2560 × 1400", new Point(42, 105), 17, ink);
                Label(dc, "副屏 · 1920 × 1160", new Point(820, 105), 17, ink);
                Label(dc, "屏幕交界", new Point(568, 120), 14, muted);
                Label(dc, rested > 0 ? "已落到副屏窗口顶部" : (double)Get(pet, "ganyuContactRemaining") > 0 ? "压腿借力，换向蹬离" : "屏幕中间也可以起跳，连续向上借力", new Point(30, 725), 17, ink);
            }
            RenderTargetBitmap bitmap = new RenderTargetBitmap(1120, 760, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(bitmap, visual);
            Save(bitmap, Path.Combine(directory, frames.ToString("000") + ".png")); frames++;
        }
        Check((IntPtr)Get(pet, "supportHandle") == scenario.Roof.Handle, "The preview never lands on its roof.");
        Console.WriteLine("Wrote " + frames + " real WPF frames to " + directory);
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (Array.IndexOf(args, "--baseline") >= 0) return Baseline();
            Stopwatch watch = Stopwatch.StartNew();
            foreach (Scenario scenario in Scenarios()) CheckRoute(scenario, Plan(scenario));
            PetWindow pet = new PetWindow(); ((DispatcherTimer)Get(pet, "timer")).Stop();
            try
            {
                RuntimeChecks(pet);
                ApproachAndSeamChecks(pet);
                if (Array.IndexOf(args, "--preview") >= 0) Preview(pet);
            }
            finally { pet.Close(); }
            Console.WriteLine("PASS: " + checks + " dual-display checks; " + Scenarios().Count + " real-layout, DPI, seam, single-wall, close-start, floating-window and staggered cases in " + watch.ElapsedMilliseconds + " ms.");
            return 0;
        }
        catch (Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException) Console.Error.WriteLine(current.Message + "\n" + current.StackTrace);
            return 1;
        }
    }
}
