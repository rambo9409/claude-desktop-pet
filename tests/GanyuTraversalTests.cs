using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class GanyuTraversalTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(PetWindow pet, string name, object value) { typeof(PetWindow).GetField(name, Private).SetValue(pet, value); }
    private static object Get(PetWindow pet, string name) { return typeof(PetWindow).GetField(name, Private).GetValue(pet); }
    private static object Call(PetWindow pet, string name, params object[] args) { return typeof(PetWindow).GetMethod(name, Private).Invoke(pet, args); }
    private static PlatformEdge Edge(int handle, double x, double right, double y, double bottom)
    { return new PlatformEdge { Handle = new IntPtr(handle), Left = x, Right = right, Y = y, Bottom = bottom, IsFloor = handle == 0 }; }
    private static GanyuSceneWindow Window(PlatformEdge edge)
    { return new GanyuSceneWindow { Handle = edge.Handle, Bounds = new Rect(edge.Left, edge.Y, edge.Right - edge.Left, edge.Bottom - edge.Y), CanBounce = true }; }
    private static void Snapshot(PetWindow pet, List<PlatformEdge> platforms, List<Rect> monitors, List<GanyuSceneWindow> scene)
    {
        Set(pet, "platformCache", platforms); Set(pet, "platformCacheTime", DateTime.MaxValue);
        Set(pet, "monitorWorkAreas", monitors); Set(pet, "monitorsCachedAt", DateTime.MaxValue);
        Set(pet, "ganyuSceneWindows", scene); Set(pet, "ganyuSceneTimestamp", DateTime.MaxValue);
    }
    private static void Position(PetWindow pet, Point foot)
    { pet.Left = foot.X - pet.Width / 2; pet.Top = foot.Y - pet.Height * 0.962; }

    private static void RouteChecks()
    {
        Stopwatch watch = Stopwatch.StartNew();
        foreach (double scale in new[] { 0.65, 1.0, 1.5 })
        foreach (Point origin in new[] { new Point(0, 0), new Point(-1920, 120), new Point(-900, -1100) })
        foreach (int side in new[] { -1, 1 })
        {
            double width = 165 * scale, height = 225 * scale;
            Rect work = new Rect(origin.X, origin.Y, 1400 * scale, 1040 * scale);
            double left = work.Left + 500 * scale, right = work.Right - 500 * scale;
            PlatformEdge roof = Edge(11, left, right, work.Top + 420 * scale, work.Bottom);
            PlatformEdge floor = Edge(0, work.Left, work.Right, work.Bottom, work.Bottom);
            List<PlatformEdge> platforms = new List<PlatformEdge> { floor, roof };
            List<GanyuSceneWindow> scene = new List<GanyuSceneWindow> { Window(roof) };
            List<Rect> monitors = new List<Rect> { work };
            Point start = new Point(side < 0 ? work.Left + GanyuTraversal.SideInset(width) : work.Right - GanyuTraversal.SideInset(width), work.Bottom);
            GanyuTraversal route = GanyuTraversal.Plan(platforms, scene, monitors, IntPtr.Zero, start, width, height, -side);
            Check(route != null, "A usable screen/window corridor was rejected.");
            Check(route.Hops.Count >= 3, "A corridor was replaced by a direct roof shortcut.");
            Check(route.Hops[0].Start == start, "The first wall jump teleports to a wall.");
            int screen = 0, window = 0, previousFacing = 0;
            Point previous = start;
            foreach (GanyuHop hop in route.Hops)
            {
                Check(hop.Start == previous, "Successive wall jumps are discontinuous.");
                Check(Math.Abs(hop.End.X - hop.Start.X) >= width * 0.28, "A hop slides vertically along one wall.");
                Check((hop.At(0) - hop.Start).Length < 0.000001 && (hop.At(1) - hop.End).Length < 0.000001, "Parabola misses its contact points.");
                Check(GanyuTraversal.ArcClear(hop, width, height, scene, monitors), "A planned hop intersects a window or desktop gap.");
                if (hop.Contact != null)
                {
                    if (hop.Contact.IsScreen) screen++; else window++;
                    Check(hop.End.Y < hop.Start.Y - 60 * scale, "A contact does not advance upward.");
                    Check(previousFacing == 0 || previousFacing != hop.Facing, "Consecutive contacts fail to reverse direction.");
                    previousFacing = hop.Facing;
                    Check(Math.Abs(hop.End.X - hop.Contact.X - hop.Contact.Normal * GanyuTraversal.SideInset(width)) < 0.001,
                        "A contact floats away from its wall.");
                }
                for (int i = 1; i <= 60; i++)
                {
                    Point a = hop.At((i - 1) / 60.0), b = hop.At(i / 60.0);
                    Check((b - a).Length < 35 * scale, "A parabolic tick jumps discontinuously.");
                }
                previous = hop.End;
            }
            Check(screen > 0 && window > 0, "Ascent does not bounce between the screen and a window.");
            Check(Math.Abs(previous.Y - roof.Y) < 0.001 && previous.X > roof.Left && previous.X < roof.Right,
                "Wall jumping never lands on the visible window roof.");
        }
        Console.WriteLine("18 scaled / negative-monitor routes: " + watch.ElapsedMilliseconds + " ms");

        Rect desktop = new Rect(0, 0, 1800, 1100);
        PlatformEdge leftRoof = Edge(21, 150, 600, 380, 1100), rightRoof = Edge(22, 1100, 1600, 450, 1100);
        List<GanyuSceneWindow> corridor = new List<GanyuSceneWindow> { Window(leftRoof), Window(rightRoof) };
        GanyuTraversal betweenWindows = GanyuTraversal.Plan(new List<PlatformEdge> { leftRoof, rightRoof }, corridor,
            new List<Rect> { desktop }, IntPtr.Zero, new Point(850, 1100), 165, 225, 1);
        Check(betweenWindows != null && betweenWindows.Hops.Count >= 3, "Two opposing window walls cannot form a route.");
        int windowContacts = 0;
        foreach (GanyuHop hop in betweenWindows.Hops) if (hop.Contact != null && !hop.Contact.IsScreen) windowContacts++;
        Check(windowContacts >= 2, "A two-window route does not bounce between its windows.");
        corridor.Add(new GanyuSceneWindow { Handle = new IntPtr(23), Bounds = desktop, IsBackdrop = true });
        Check(GanyuTraversal.Plan(new List<PlatformEdge> { leftRoof, rightRoof }, corridor,
            new List<Rect> { desktop }, IntPtr.Zero, new Point(850, 1100), 165, 225, 1) != null,
            "A maximized background window fills the air between visible foreground windows.");
        GanyuSceneWindow backdrop = corridor[2]; corridor.RemoveAt(2); corridor.Insert(0, backdrop);
        Check(GanyuTraversal.Plan(new List<PlatformEdge> { leftRoof, rightRoof }, corridor,
            new List<Rect> { desktop }, IntPtr.Zero, new Point(850, 1100), 165, 225, 1) == null,
            "A foreground maximized window allows jumping to a roof hidden behind it.");

        PlatformEdge blocked = Edge(31, 130, 600, 320, 1100);
        Check(GanyuTraversal.Plan(new List<PlatformEdge> { blocked }, new List<GanyuSceneWindow> { Window(blocked) },
            new List<Rect> { desktop }, IntPtr.Zero, new Point(61.05, 1100), 165, 225, 1) == null,
            "A corridor narrower than the sprite accepts a route.");
        List<Rect> adjacent = new List<Rect> { new Rect(-1280, 0, 1280, 1040), new Rect(0, 0, 1920, 1040) };
        int seams = 0, seamNormals = 0;
        foreach (GanyuWall wall in GanyuTraversal.Walls(new List<GanyuSceneWindow>(), adjacent))
            if (Math.Abs(wall.X) < 0.01)
            {
                Check(wall.IsSeam && wall.Top == 0 && wall.Bottom == 1040, "Shared monitor edge is not marked as a voluntary kick line.");
                seams++; seamNormals += wall.Normal;
            }
        Check(seams == 2 && seamNormals == 0, "A shared monitor edge cannot be kicked from both sides.");
        GanyuHop seamCrossing = new GanyuHop { Start = new Point(-300, 800), End = new Point(300, 800), Lift = 100, Duration = 1 };
        Check(GanyuTraversal.ArcClear(seamCrossing, 165, 225, new List<GanyuSceneWindow>(), adjacent),
            "The voluntary kick line blocks a normal cross-monitor arc.");
        List<Rect> staggered = new List<Rect> { new Rect(0, 0, 1920, 1040), new Rect(1920, 400, 1920, 1040) };
        GanyuHop gap = new GanyuHop { Start = new Point(1500, 900), End = new Point(2120, 800), Lift = 450, Duration = 1 };
        Check(!GanyuTraversal.ArcClear(gap, 165, 225, new List<GanyuSceneWindow>(), staggered), "A hop crosses a staggered-monitor gap.");
        foreach (GanyuWall wall in GanyuTraversal.Walls(new List<GanyuSceneWindow>(), staggered))
            if (wall.IsSeam) Check(wall.Top == 400 && wall.Bottom == 1040, "A staggered monitor seam extends into invisible desktop space.");
        GanyuSceneWindow covered = new GanyuSceneWindow { Handle = new IntPtr(51), Bounds = new Rect(450, 200, 200, 800), CanBounce = false };
        GanyuSceneWindow behind = new GanyuSceneWindow { Handle = new IntPtr(52), Bounds = new Rect(500, 300, 500, 700), CanBounce = true };
        foreach (GanyuWall wall in GanyuTraversal.Walls(new List<GanyuSceneWindow> { covered, behind }, new List<Rect> { desktop }))
            Check(wall.Handle != behind.Handle || wall.Normal != -1, "A foreground dialog leaves an attachable hidden wall.");

        // A standalone tall window supplies its own repeated foothold. Every
        // loop must leave the wall, reverse in the air and return to contact.
        Rect wideWork = new Rect(0, 0, 3440, 1440);
        PlatformEdge lone = Edge(71, 1900, 2350, 500, 1440);
        List<GanyuSceneWindow> loneScene = new List<GanyuSceneWindow> { Window(lone) };
        List<PlatformEdge> lonePlatforms = new List<PlatformEdge> { Edge(0, 0, 3440, 1440, 1440), lone };
        GanyuTraversal loneRoute = GanyuTraversal.Plan(lonePlatforms, loneScene, new List<Rect> { wideWork },
            IntPtr.Zero, new Point(1900 - GanyuTraversal.SideInset(165), 1440), 165, 225, 1);
        Check(loneRoute != null, "A lone window needs a second distant wall to become reachable.");
        int curls = 0;
        foreach (GanyuHop hop in loneRoute.Hops)
        {
            if (hop.Contact == null || Math.Abs(hop.End.X - hop.Start.X) > 1) continue;
            curls++;
            Check(Math.Abs(hop.At(0.5).X - hop.Start.X) >= 90, "Same-wall ascent never visibly kicks outward.");
            Check(hop.FacingAt(0) != hop.FacingAt(1), "A returning wall kick never turns back toward its contact.");
            Check(GanyuTraversal.ArcClear(hop, 165, 225, loneScene, new List<Rect> { wideWork }), "Same-wall loop intersects its support.");
        }
        Check(curls > 0, "Lone-wall fixture never exercises an outward-and-return kick.");
        Point approach; GanyuTraversal approachRoute;
        Point farStart = new Point(900, 1440);
        Check(GanyuTraversal.Plan(lonePlatforms, loneScene, new List<Rect> { wideWork }, IntPtr.Zero, farStart, 165, 225, 1) == null,
            "The far-start fixture is already within one jump of a real surface.");
        Stopwatch approachWatch = Stopwatch.StartNew();
        Check(GanyuTraversal.FindApproach(lonePlatforms, loneScene, new List<Rect> { wideWork }, IntPtr.Zero,
            farStart, 165, 225, 1, out approach, out approachRoute), "A safe short approach to the lone window was not found.");
        Check(approach.Y == farStart.Y && Math.Abs(approach.X - farStart.X) <= 780 && approachRoute != null,
            "Approach changes floors, takes too long or lacks a complete landing route.");
        Check(approachRoute.Hops[0].Start == approach, "Approach route starts at another point.");
        Check(approachWatch.ElapsedMilliseconds < 250, "Approach search stalls the UI.");
        Console.WriteLine("Safe approach search: " + approachWatch.ElapsedMilliseconds + " ms");
        List<Rect> stepMonitors = new List<Rect> { new Rect(0, 0, 1400, 1440), new Rect(1400, 0, 1920, 1160) };
        PlatformEdge stepRoof = Edge(72, 1900, 2350, 500, 1160);
        List<PlatformEdge> stepPlatforms = new List<PlatformEdge> { Edge(0, 0, 1400, 1440, 1440), Edge(0, 1400, 3320, 1160, 1160), stepRoof };
        Check(GanyuTraversal.FindApproach(stepPlatforms, new List<GanyuSceneWindow> { Window(stepRoof) }, stepMonitors,
            IntPtr.Zero, new Point(900, 1440), 165, 225, 1, out approach, out approachRoute),
            "An approach cannot transfer from the lower outer edge to the shared monitor seam.");
        Check(approach.X <= 1400 - GanyuTraversal.SideInset(165) && approach.Y == 1440,
            "A horizontal approach walks through the missing strip below the shorter display.");
        foreach (GanyuHop hop in approachRoute.Hops)
            Check(GanyuTraversal.ArcClear(hop, 165, 225, new List<GanyuSceneWindow> { Window(stepRoof) }, stepMonitors),
                "The approach's planned jump cuts across the missing desktop strip.");

        List<GanyuSceneWindow> busyScene = new List<GanyuSceneWindow>();
        List<PlatformEdge> busyPlatforms = new List<PlatformEdge>();
        for (int i = 0; i < 16; i++)
        {
            PlatformEdge edge = Edge(200 + i, 650 + i % 4 * 650, 1000 + i % 4 * 650,
                250 + i / 4 * 330, 480 + i / 4 * 330);
            busyPlatforms.Add(edge); busyScene.Add(Window(edge));
        }
        Stopwatch busyWatch = Stopwatch.StartNew();
        GanyuTraversal.Plan(busyPlatforms, busyScene, new List<Rect> { new Rect(0, 0, 3440, 1440) },
            IntPtr.Zero, new Point(100, 1440), 165, 225, 1);
        Console.WriteLine("16-window route search: " + busyWatch.ElapsedMilliseconds + " ms");
        Check(busyWatch.ElapsedMilliseconds < 250, "A crowded desktop stalls route planning.");
    }

    private static void Prepare(PetWindow pet, out List<PlatformEdge> platforms, out List<Rect> monitors, out List<GanyuSceneWindow> scene)
    {
        Call(pet, "ClearGanyuTraversal"); Call(pet, "CancelWings");
        Set(pet, "currentCharacter", "ganyu"); Set(pet, "frames", Call(pet, "LoadCharacterFrames", "ganyu"));
        Set(pet, "state", "idle"); Set(pet, "stateEnds", DateTime.MaxValue); Set(pet, "movement", "rest");
        Set(pet, "autoWalk", true); Set(pet, "quietMode", false); Set(pet, "pointerHeld", false);
        Set(pet, "direction", 1); Set(pet, "supportHandle", IntPtr.Zero);
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue); Set(pet, "nextWalk", DateTime.MaxValue);
        Rect work = new Rect(-900, -200, 1400, 1040);
        PlatformEdge window = Edge(101, -400, 0, 220, 840);
        platforms = new List<PlatformEdge> { Edge(0, -900, 500, 840, 840), window };
        monitors = new List<Rect> { work }; scene = new List<GanyuSceneWindow> { Window(window) };
        Snapshot(pet, platforms, monitors, scene);
        Position(pet, new Point(work.Left + GanyuTraversal.SideInset(pet.Width), work.Bottom));
    }

    private static void RuntimeChecks(PetWindow pet)
    {
        List<PlatformEdge> platforms; List<Rect> monitors; List<GanyuSceneWindow> scene;
        Prepare(pet, out platforms, out monitors, out scene);
        Check((bool)Call(pet, "TryStartGanyuJump", true), "Automatic traversal cannot start.");
        int contacts = 0, lastIndex = 0; DateTime now = DateTime.Now;
        for (int frame = 0; frame < 600 && (string)Get(pet, "movement") == "jump"; frame++)
        {
            Point before = new Point(pet.Left, pet.Top);
            Call(pet, "UpdateMovement", now.AddMilliseconds(frame * 16), 0.016);
            Check((string)Get(pet, "movement") != "climb", "A hop became legacy wall climbing.");
            Check((new Point(pet.Left, pet.Top) - before).Length < 25, "A hop transition teleports.");
            if ((double)Get(pet, "ganyuContactRemaining") > 0)
            {
                double y = pet.Top;
                Call(pet, "UpdateMovement", now.AddMilliseconds(frame * 16), 0.001);
                Check(Math.Abs(y - pet.Top) < 0.001, "Wall contact slides vertically instead of pushing away.");
            }
            int index = (int)Get(pet, "ganyuHopIndex");
            if (index > lastIndex) contacts++;
            lastIndex = index;
        }
        Check(contacts >= 2, "Runtime never completes alternating contacts.");
        Check((string)Get(pet, "movement") == "rest" && (IntPtr)Get(pet, "supportHandle") == new IntPtr(101), "Runtime does not land on its selected roof.");
        Check(Math.Abs(pet.Top + pet.Height * 0.962 - 220) < 0.001, "Landing height misses window top.");
        Check(Get(pet, "ganyuTraversal") == null, "Completed route remains active.");

        // Exercise the real walk-to-screen-edge entry, not just a planner
        // call positioned at an ideal contact point.
        foreach (int side in new[] { -1, 1 })
        {
            Prepare(pet, out platforms, out monitors, out scene);
            Rect work = monitors[0];
            Set(pet, "movement", "walk"); Set(pet, "direction", side);
            Set(pet, "nextGanyuJump", DateTime.MaxValue);
            Set(pet, "walkEnds", DateTime.MaxValue); Set(pet, "nextSpeedChange", DateTime.MaxValue);
            Set(pet, "moveSpeed", 100.0); Set(pet, "targetMoveSpeed", 100.0);
            Position(pet, new Point(side < 0 ? work.Left + GanyuTraversal.SideInset(pet.Width) + 2 :
                work.Right - GanyuTraversal.SideInset(pet.Width) - 2, work.Bottom));
            Point before = new Point(pet.Left, pet.Top);
            Call(pet, "UpdateMovement", now, 0.05);
            Check((string)Get(pet, "movement") == "jump", "Walking to a real screen edge cannot launch a safe Ganyu route.");
            Check((new Point(pet.Left, pet.Top) - before).Length <= 5.001, "Preparing a screen-edge route snaps to the wall.");
            GanyuTraversal edgeRoute = (GanyuTraversal)Get(pet, "ganyuTraversal");
            Check(GanyuTraversal.InsideDesktop(GanyuTraversal.Body(edgeRoute.Hops[0].Start, pet.Width, pet.Height), monitors),
                "The walk endpoint starts a jump outside the usable display.");
        }

        foreach (string invalidation in new[] { "move", "closed", "occlude", "monitor", "quiet", "disabled", "character" })
        {
            Prepare(pet, out platforms, out monitors, out scene);
            Check((bool)Call(pet, "TryStartGanyuJump", true), "Cancellation fixture cannot launch.");
            Call(pet, "UpdateMovement", now, 0.2);
            if (invalidation == "move") scene[0].Bounds = new Rect(-390, 220, 400, 620);
            if (invalidation == "closed") { scene.Clear(); platforms.RemoveAt(1); }
            if (invalidation == "occlude") scene.Insert(0, new GanyuSceneWindow { Handle = new IntPtr(202), Bounds = new Rect(-800, 100, 380, 650) });
            if (invalidation == "monitor") monitors.Clear();
            if (invalidation == "quiet") Set(pet, "quietMode", true);
            if (invalidation == "disabled") Set(pet, "autoWalk", false);
            if (invalidation == "character") Set(pet, "currentCharacter", "hutao");
            Call(pet, "UpdateMovement", now.AddSeconds(1), 0.016);
            Check((string)Get(pet, "movement") == "fall" && Get(pet, "ganyuTraversal") == null,
                "Unsafe route does not cancel to gliding: " + invalidation);
            Check((IntPtr)Get(pet, "fallFromHandle") == IntPtr.Zero, "A cancelled leap falls through its launch roof.");
        }
        Prepare(pet, out platforms, out monitors, out scene);
        Set(pet, "quietMode", true); Set(pet, "autoWalk", false);
        Check(!(bool)Call(pet, "TryStartGanyuJump", true), "Quiet mode accepts an automatic traversal.");
        Check((bool)Call(pet, "TryStartGanyuJump", false), "Manual traversal is blocked by quiet mode.");
        Call(pet, "UpdateMovement", DateTime.Now, 0.016);
        Check((string)Get(pet, "movement") == "jump", "Quiet mode cancels an explicitly requested manual traversal.");
        Call(pet, "ClearGanyuTraversal");
        Call(pet, "UpdateMovement", DateTime.Now, 0.016);
        Check((string)Get(pet, "movement") == "fall", "A cleared/hidden route resumes the obsolete single-hop path toward the wrong roof.");
        Prepare(pet, out platforms, out monitors, out scene);
        Call(pet, "StartClimbing", platforms[0], -1, true);
        Check((string)Get(pet, "movement") == "jump", "Screen-edge climb entry does not become a wall jump.");
        Set(pet, "movement", "climb"); Call(pet, "ClearGanyuTraversal");
        Call(pet, "UpdateClimb", DateTime.Now, 0.016);
        Check((string)Get(pet, "movement") != "climb", "A legacy Ganyu climb persists for another tick.");
        Prepare(pet, out platforms, out monitors, out scene); scene.Clear(); platforms.RemoveAt(1);
        Call(pet, "StartClimbing", platforms[0], -1, true);
        Check((string)Get(pet, "movement") == "rest", "No-roof screen edge enters climbing or a pointless jump.");
        foreach (string character in new[] { "kolo", "hutao", "varesa" })
        {
            Set(pet, "currentCharacter", character); Set(pet, "movement", "rest");
            Check(!(bool)Call(pet, "TryStartGanyuJump", false), "Another character receives Ganyu traversal.");
        }
    }

    private static BitmapSource CapturePet(PetWindow pet)
    {
        Set(pet, "pointerHeld", true); Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-16));
        Call(pet, "OnTick", null, EventArgs.Empty); Set(pet, "pointerHeld", false);
        FrameworkElement content = (FrameworkElement)pet.Content;
        WpfTestRender.Drain();
        content.Measure(new Size(pet.Width, pet.Height)); content.Arrange(new Rect(0, 0, pet.Width, pet.Height)); content.UpdateLayout();
        RenderTargetBitmap image = new RenderTargetBitmap((int)Math.Ceiling(pet.Width), (int)Math.Ceiling(pet.Height), 96, 96, PixelFormats.Pbgra32);
        WpfTestRender.Render(image, content); image.Freeze(); return image;
    }
    private static void Label(DrawingContext dc, string text, Point point, double size, Brush brush)
    { dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface("Microsoft YaHei UI"), size, brush, 1), point); }
    private static void Save(BitmapSource bitmap, string path)
    { PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (FileStream file = File.Create(path)) encoder.Save(file); }

    private static void Preview(PetWindow pet)
    {
        string directory = "artifacts/v24/ganyu-walljump-frames"; Directory.CreateDirectory(directory);
        List<PlatformEdge> unused; List<Rect> unusedMonitors; List<GanyuSceneWindow> unusedScene;
        Prepare(pet, out unused, out unusedMonitors, out unusedScene);
        pet.Width = 110; pet.Height = 150;
        Rect work = new Rect(20, 72, 880, 580);
        PlatformEdge roof = Edge(301, 370, 755, 282, work.Bottom);
        Snapshot(pet, new List<PlatformEdge> { Edge(0, work.Left, work.Right, work.Bottom, work.Bottom), roof },
            new List<Rect> { work }, new List<GanyuSceneWindow> { Window(roof) });
        Position(pet, new Point(work.Left + GanyuTraversal.SideInset(pet.Width), work.Bottom));
        Check((bool)Call(pet, "TryStartGanyuJump", false), "Preview cannot start screen/window route.");
        GanyuTraversal route = (GanyuTraversal)Get(pet, "ganyuTraversal");
        DateTime now = DateTime.Now; int frames = 0, rested = 0;
        while (frames < 240 && rested < 25)
        {
            if ((string)Get(pet, "movement") == "jump") Call(pet, "UpdateMovement", now.AddMilliseconds(frames * 32), 0.032);
            else rested++;
            BitmapSource character = CapturePet(pet);
            DrawingVisual scene = new DrawingVisual();
            using (DrawingContext dc = scene.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(234, 245, 255)), null, new Rect(0, 0, 920, 680));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(218, 236, 250)), new Pen(Brushes.SteelBlue, 2), work, 10, 10);
                dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(104, 154, 193)), 2),
                    new Rect(roof.Left, roof.Y, roof.Right - roof.Left, roof.Bottom - roof.Y), 10, 10);
                Label(dc, "甘雨 · 蹬墙连跳", new Point(28, 20), 23, Brushes.MidnightBlue);
                Label(dc, "屏幕边 ⇄ 窗口侧边 → 窗口顶部", new Point(475, 27), 17, Brushes.SteelBlue);
                Label(dc, "可落脚的窗口", new Point(roof.Left + 22, roof.Y + 20), 16, Brushes.SlateGray);
                Pen pathPen = new Pen(new SolidColorBrush(Color.FromArgb(65, 64, 133, 177)), 1.4); pathPen.DashStyle = DashStyles.Dot;
                foreach (GanyuHop hop in route.Hops)
                    for (int i = 1; i <= 32; i++) dc.DrawLine(pathPen, hop.At((i - 1) / 32.0), hop.At(i / 32.0));
                if ((double)Get(pet, "ganyuContactRemaining") > 0)
                {
                    int index = (int)Get(pet, "ganyuHopIndex"); GanyuWall wall = route.Hops[index].Contact;
                    Point touch = new Point(wall.X, pet.Top + pet.Height * 0.80);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(110, 159, 224, 255)), new Pen(Brushes.White, 2), touch, 15, 15);
                }
                dc.DrawImage(character, new Rect(pet.Left, pet.Top, pet.Width, pet.Height));
                Label(dc, rested > 0 ? "稳稳落在窗口顶部" : (double)Get(pet, "ganyuContactRemaining") > 0 ? "碰壁压腿 · 换向蹬离" : "借力跳跃 · 连续上升",
                    new Point(44, 622), 16, Brushes.SteelBlue);
            }
            RenderTargetBitmap bitmap = new RenderTargetBitmap(920, 680, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(bitmap, scene);
            Save(bitmap, Path.Combine(directory, frames.ToString("000") + ".png")); frames++;
        }
        Console.WriteLine("Wrote " + frames + " actual WPF frames to " + directory);
    }

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            RouteChecks();
            PetWindow pet = new PetWindow(); ((DispatcherTimer)Get(pet, "timer")).Stop();
            RuntimeChecks(pet);
            if (Array.IndexOf(args, "--preview") >= 0) Preview(pet);
            pet.Close();
            Console.WriteLine("PASS: " + checks + " Ganyu wall-jump checks: alternating contacts, screen/window and window/window routes, scaling, negative monitors, seams, occlusion, continuous motion, landing, cancellation and no climbing.");
        }
        catch (Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException) Console.Error.WriteLine(current.Message + "\n" + current.StackTrace);
            Environment.ExitCode = 1;
        }
    }
}
