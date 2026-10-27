using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace ClaudePet
{
    internal sealed class GanyuSceneWindow
    {
        internal IntPtr Handle;
        internal Rect Bounds;
        internal bool CanBounce;
        internal bool IsBackdrop;
    }

    internal sealed class GanyuWall
    {
        internal IntPtr Handle;
        internal double X, Top, Bottom;
        // The free side of the wall: a jump leaves in this direction.
        internal int Normal;
        internal Rect Monitor;
        internal bool IsSeam;
        internal bool IsScreen { get { return Handle == IntPtr.Zero; } }
    }

    internal sealed class GanyuHop
    {
        internal Point Start, End;
        internal double Lift, Duration, SideExcursion;
        internal GanyuWall Contact;
        internal Point At(double progress)
        {
            Point point = DesktopMovement.JumpPosition(Start, End, progress, Lift);
            point.X += SideExcursion * Math.Sin(Math.PI * progress);
            return point;
        }
        internal int FacingAt(double progress)
        {
            double velocity = End.X - Start.X + SideExcursion * Math.PI * Math.Cos(Math.PI * progress);
            return velocity >= 0 ? 1 : -1;
        }
        internal int Facing { get { return FacingAt(0); } }
    }

    // Plans complete routes before launch. A contact can push toward another
    // surface or visibly kick outward and return higher on the same wall.
    internal sealed class GanyuTraversal
    {
        internal readonly List<GanyuHop> Hops = new List<GanyuHop>();
        internal readonly List<GanyuSceneWindow> RequiredWindows = new List<GanyuSceneWindow>();
        internal PlatformEdge Landing;
        internal List<Rect> Monitors;

        private sealed class SearchNode
        {
            internal Point Foot;
            internal GanyuWall Wall;
            internal SearchNode Previous;
            internal GanyuHop Hop;
            internal int Depth;
        }

        internal static double SideInset(double width) { return width * 0.37; }
        internal static Rect Body(Point foot, double width, double height)
        {
            // Union of the four actual jump sprites, including the small
            // stretch on takeoff. Transparent space above the head is free.
            return new Rect(foot.X - SideInset(width), foot.Y - width * 0.79,
                SideInset(width) * 2, width * 0.79);
        }

        internal static bool InsideDesktop(Rect body, IList<Rect> monitors)
        {
            List<Rect> pieces = new List<Rect> { body };
            foreach (Rect monitor in monitors)
            {
                List<Rect> next = new List<Rect>();
                foreach (Rect piece in pieces)
                {
                    Rect cut = Rect.Intersect(piece, monitor);
                    if (cut.IsEmpty || cut.Width < 0.00001 || cut.Height < 0.00001) { next.Add(piece); continue; }
                    AddRect(next, piece.Left, piece.Top, cut.Left - piece.Left, piece.Height);
                    AddRect(next, cut.Right, piece.Top, piece.Right - cut.Right, piece.Height);
                    AddRect(next, cut.Left, piece.Top, cut.Width, cut.Top - piece.Top);
                    AddRect(next, cut.Left, cut.Bottom, cut.Width, piece.Bottom - cut.Bottom);
                }
                pieces = next;
                if (pieces.Count == 0) return true;
            }
            return pieces.Count == 0;
        }

        private static void AddRect(List<Rect> list, double x, double y, double w, double h)
        { if (w > 0.001 && h > 0.001) list.Add(new Rect(x, y, w, h)); }

        internal static bool ArcClear(GanyuHop hop, double width, double height,
            IList<GanyuSceneWindow> windows, IList<Rect> monitors)
        {
            int steps = Math.Max(40, (int)Math.Ceiling(((hop.End - hop.Start).Length + hop.Lift * 2 + Math.Abs(hop.SideExcursion) * 2) / 5));
            for (int i = 0; i <= steps; i++)
            {
                Rect body = Body(hop.At((double)i / steps), width, height);
                if (!InsideDesktop(body, monitors)) return false;
                foreach (GanyuSceneWindow window in windows)
                {
                    // A maximized background is the canvas behind the visible
                    // floating windows, not a solid box filling all air space.
                    // It still occludes any walls behind it in Walls().
                    if (window.IsBackdrop) continue;
                    Rect intersection = Rect.Intersect(body, window.Bounds);
                    if (!intersection.IsEmpty && intersection.Width > 0.5 && intersection.Height > 0.5) return false;
                }
            }
            return true;
        }

        internal static List<GanyuWall> Walls(IList<GanyuSceneWindow> windows, IList<Rect> monitors)
        {
            List<GanyuWall> walls = new List<GanyuWall>();
            foreach (Rect monitor in monitors)
            foreach (int side in new[] { -1, 1 })
            {
                double x = side < 0 ? monitor.Left : monitor.Right;
                List<Rect> pieces = new List<Rect> { new Rect(x - 0.1, monitor.Top, 0.2, monitor.Height) };
                // The join remains open space for walking and crossing arcs.
                // It is also a voluntary kick-off line on either visible side.
                foreach (Rect other in monitors)
                {
                    if (other == monitor || !(side < 0 ? other.Left < x && other.Right >= x : other.Right > x && other.Left <= x)) continue;
                    if (Math.Abs(side < 0 ? other.Right - x : other.Left - x) < 1)
                    {
                        double top = Math.Max(monitor.Top, other.Top), bottom = Math.Min(monitor.Bottom, other.Bottom);
                        if (bottom > top) walls.Add(new GanyuWall { X = x, Top = top, Bottom = bottom,
                            Normal = -side, Monitor = monitor, IsSeam = true });
                    }
                    pieces = CutVertical(pieces, other.Top, other.Bottom);
                }
                foreach (Rect piece in pieces) walls.Add(new GanyuWall { X = x, Top = piece.Top,
                    Bottom = piece.Bottom, Normal = -side, Monitor = monitor });
            }
            // Front windows are first, matching EnumWindows z order. A wall
            // cannot be attached to through a foreground window or dialog.
            for (int i = 0; i < windows.Count; i++)
            {
                GanyuSceneWindow window = windows[i];
                if (!window.CanBounce) continue;
                foreach (int side in new[] { -1, 1 })
                {
                    double x = side < 0 ? window.Bounds.Left : window.Bounds.Right;
                    List<Rect> pieces = new List<Rect> { new Rect(x - 0.1, window.Bounds.Top, 0.2, window.Bounds.Height) };
                    for (int front = 0; front < i; front++)
                    {
                        Rect cover = windows[front].Bounds;
                        if (cover.Left <= x + 1 && cover.Right >= x - 1) pieces = CutVertical(pieces, cover.Top, cover.Bottom);
                    }
                    foreach (Rect piece in pieces) walls.Add(new GanyuWall { Handle = window.Handle,
                        X = x, Top = piece.Top, Bottom = piece.Bottom, Normal = side });
                }
            }
            return walls;
        }

        private static List<Rect> CutVertical(List<Rect> pieces, double top, double bottom)
        {
            List<Rect> result = new List<Rect>();
            foreach (Rect piece in pieces)
            {
                if (bottom <= piece.Top || top >= piece.Bottom) { result.Add(piece); continue; }
                AddRect(result, piece.Left, piece.Top, piece.Width, Math.Min(top, piece.Bottom) - piece.Top);
                AddRect(result, piece.Left, Math.Max(bottom, piece.Top), piece.Width, piece.Bottom - Math.Max(bottom, piece.Top));
            }
            return result;
        }

        private static GanyuHop Hop(Point start, Point end, GanyuWall contact, double scale)
        {
            double dx = Math.Abs(end.X - start.X), rise = start.Y - end.Y;
            double lift = Math.Max(48 * scale, Math.Max(dx * 0.16, rise * 0.40));
            return new GanyuHop { Start = start, End = end, Lift = lift, Contact = contact,
                Duration = Math.Max(0.52, Math.Min(1.18, (dx + Math.Max(0, rise) * 0.50) / (500 * scale))) };
        }

        private static bool TryLanding(SearchNode node, PlatformEdge target, double width, double height,
            IList<GanyuSceneWindow> windows, IList<Rect> monitors, int preferredDirection, out GanyuHop hop)
        {
            hop = null;
            double scale = Math.Max(0.6, width / 165), inset = SideInset(width) + 3;
            if (target.Right - target.Left < inset * 2 || target.Y > node.Foot.Y + 300 * scale ||
                target.Y < node.Foot.Y - 325 * scale) return false;
            double near = Math.Max(target.Left + inset, Math.Min(target.Right - inset, node.Foot.X));
            foreach (double x in new[] { near, target.Left + inset, target.Right - inset, (target.Left + target.Right) * 0.5 })
            {
                double dx = x - node.Foot.X;
                if (Math.Abs(dx) > 720 * scale) continue;
                GanyuHop candidate = Hop(node.Foot, new Point(x, target.Y), null, scale);
                // From the same side as the roof, first kick out into the free
                // space and curl back above the corner. Never cut through it.
                if (node.Wall != null && (Math.Sign(dx) != node.Wall.Normal || Math.Abs(dx) < width * 0.3))
                {
                    candidate.SideExcursion = node.Wall.Normal * (Math.Abs(dx) / Math.PI + 105 * scale);
                    candidate.Duration = Math.Max(0.70, candidate.Duration);
                }
                foreach (double extra in new[] { 0.0, 55 * scale, 110 * scale })
                {
                    candidate.Lift = Math.Max(65 * scale, Math.Max(Math.Abs(dx) * 0.24,
                        (node.Foot.Y - target.Y) * 0.82)) + extra;
                    if (ArcClear(candidate, width, height, windows, monitors)) { hop = candidate; return true; }
                }
            }
            return false;
        }

        internal static GanyuTraversal Plan(IList<PlatformEdge> platforms, IList<GanyuSceneWindow> windows,
            IList<Rect> monitors, IntPtr support, Point start, double width, double height, int direction)
        {
            return PlanWithinBudget(platforms, windows, monitors, support, start, width, height, direction,
                Stopwatch.StartNew(), 80);
        }

        private static GanyuTraversal PlanWithinBudget(IList<PlatformEdge> platforms, IList<GanyuSceneWindow> windows,
            IList<Rect> monitors, IntPtr support, Point start, double width, double height, int direction,
            Stopwatch watch, long budgetMilliseconds)
        {
            if (width <= 0 || height <= 0 || monitors.Count == 0 || !InsideDesktop(Body(start, width, height), monitors)) return null;
            double scale = Math.Max(0.6, width / 165);
            List<PlatformEdge> targets = new List<PlatformEdge>();
            foreach (PlatformEdge platform in platforms)
            {
                if (!platform.IsFloor && platform.Handle != support && platform.Right - platform.Left >= width * 0.78 &&
                    Math.Abs(Math.Max(platform.Left, Math.Min(platform.Right, start.X)) - start.X) <= 1600 * scale &&
                    platform.Y <= start.Y + 300 * scale)
                {
                    List<Rect> above = new List<Rect>();
                    bool found = false;
                    foreach (GanyuSceneWindow window in windows)
                    {
                        if (window.Handle == platform.Handle) { found = true; break; }
                        above.Add(window.Bounds);
                    }
                    if (!found) continue;
                    foreach (PlatformEdge exposed in DesktopMovement.VisibleTopSegments(platform, above, width * 0.79))
                        if (exposed.Right - exposed.Left >= width * 0.78) targets.Add(exposed);
                }
            }
            targets.Sort(delegate(PlatformEdge a, PlatformEdge b) {
                return Score(a, start, direction, width).CompareTo(Score(b, start, direction, width)); });
            List<GanyuWall> walls = Walls(windows, monitors);
            GanyuTraversal best = null;
            double bestScore = Double.MaxValue;
            for (int targetIndex = 0; targetIndex < Math.Min(6, targets.Count); targetIndex++)
            {
                PlatformEdge target = targets[targetIndex];
                Queue<SearchNode> queue = new Queue<SearchNode>();
                GanyuWall startingWall = null;
                foreach (GanyuWall wall in walls)
                    if (Math.Abs(start.X - wall.X - wall.Normal * SideInset(width)) < 3 &&
                        start.Y >= wall.Top + width * 0.13 && start.Y <= wall.Bottom + 2) { startingWall = wall; break; }
                queue.Enqueue(new SearchNode { Foot = start, Wall = startingWall });
                HashSet<string> visited = new HashSet<string>();
                int searched = 0;
                while (queue.Count > 0 && searched++ < 260)
                {
                    if (watch.ElapsedMilliseconds >= budgetMilliseconds) return best;
                    SearchNode node = queue.Dequeue();
                    GanyuHop landing;
                    if (TryLanding(node, target, width, height, windows, monitors, direction, out landing))
                    {
                        GanyuTraversal route = Finish(node, landing, target, windows, monitors);
                        double travel = 0;
                        foreach (GanyuHop part in route.Hops) travel += (part.End - part.Start).Length + Math.Abs(part.SideExcursion);
                        double score = Score(target, start, direction, width) + route.Hops.Count * 90 * scale + travel * 0.12;
                        if (score < bestScore) { best = route; bestScore = score; }
                        break;
                    }
                    if (node.Depth >= 9) continue;
                    // Opposing surfaces are considered before a same-wall curl.
                    // Both are genuine contacts; the latter frees a lone window
                    // from requiring a second wall at one exact distance.
                    for (int repeat = 0; repeat <= 1; repeat++)
                    for (int w = 0; w < walls.Count; w++)
                    {
                        GanyuWall wall = walls[w];
                        // A stepped display edge has an outer segment below
                        // its shorter neighbour and a seam above it. These
                        // segments are one continuous physical kick-off line.
                        bool same = node.Wall != null && Math.Abs(node.Wall.X - wall.X) < 0.01 && node.Wall.Normal == wall.Normal;
                        if (same != (repeat == 1)) continue;
                        double x = wall.X + wall.Normal * SideInset(width), dx = x - node.Foot.X;
                        if (!same && (Math.Sign(dx) != -wall.Normal || Math.Abs(dx) < width * 0.12 || Math.Abs(dx) > 720 * scale ||
                            (node.Wall != null && Math.Sign(dx) != node.Wall.Normal))) continue;
                        // Include a contact just below the chosen roof as well
                        // as regular steps. Arbitrary window heights need not
                        // happen to align with a fixed rise lattice.
                        double roofRise = node.Foot.Y - (target.Y + width * 0.22);
                        foreach (double rise in new[] { 245 * scale, 275 * scale, 175 * scale, 105 * scale, roofRise, 55 * scale })
                        {
                            if (rise < 35 * scale || rise > 275 * scale) continue;
                            double y = node.Foot.Y - rise;
                            if (y < wall.Top + width * 0.13 || y > wall.Bottom + 2 || y < target.Y - 90 * scale) continue;
                            string key = w + ":" + Math.Round(y / (24 * scale)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            if (visited.Contains(key)) continue;
                            GanyuHop hop = Hop(node.Foot, new Point(x, y), wall, scale);
                            if (same)
                            {
                                hop.SideExcursion = wall.Normal * 118 * scale;
                                hop.Duration = Math.Max(0.72, hop.Duration);
                                hop.Lift = Math.Max(hop.Lift, 70 * scale);
                            }
                            if (!ArcClear(hop, width, height, windows, monitors)) continue;
                            visited.Add(key);
                            queue.Enqueue(new SearchNode { Foot = hop.End, Wall = wall, Previous = node,
                                Hop = hop, Depth = node.Depth + 1 });
                        }
                    }
                }
            }
            return best;
        }

        internal static bool FindApproach(IList<PlatformEdge> platforms, IList<GanyuSceneWindow> windows,
            IList<Rect> monitors, IntPtr support, Point start, double width, double height, int direction,
            out Point approach, out GanyuTraversal route)
        {
            approach = start; route = null;
            Stopwatch watch = Stopwatch.StartNew();
            double scale = Math.Max(0.6, width / 165), inset = SideInset(width);
            // Walking stays on the current continuous, equal-height support.
            // In particular a shorter second display cannot be approached by
            // walking horizontally into its missing bottom strip.
            List<PlatformEdge> sameLevel = new List<PlatformEdge>();
            foreach (PlatformEdge platform in platforms)
                if ((support == IntPtr.Zero ? platform.IsFloor : platform.Handle == support && !platform.IsFloor) &&
                    Math.Abs(platform.Y - start.Y) <= 3) sameLevel.Add(platform);
            double left = start.X, right = start.X;
            bool foundSupport = false;
            foreach (PlatformEdge platform in sameLevel)
                if (platform.Left <= start.X && platform.Right >= start.X)
                { left = platform.Left; right = platform.Right; foundSupport = true; break; }
            if (!foundSupport) return false;
            bool expanded;
            do
            {
                expanded = false;
                foreach (PlatformEdge platform in sameLevel)
                    if (platform.Left <= right + 1 && platform.Right >= left - 1 && (platform.Left < left || platform.Right > right))
                    { left = Math.Min(left, platform.Left); right = Math.Max(right, platform.Right); expanded = true; }
            } while (expanded);
            left += inset; right -= inset;
            if (right < left) return false;
            List<double> candidates = new List<double>();
            foreach (GanyuWall wall in Walls(windows, monitors))
            {
                candidates.Add(wall.X + wall.Normal * inset);
                candidates.Add(wall.X + wall.Normal * (inset + 100 * scale));
                candidates.Add(wall.X + wall.Normal * (inset + 260 * scale));
            }
            candidates.Sort(delegate(double a, double b) { return Math.Abs(a - start.X).CompareTo(Math.Abs(b - start.X)); });
            List<double> considered = new List<double>();
            int tried = 0;
            foreach (double candidate in candidates)
            {
                if (watch.ElapsedMilliseconds >= 110) break;
                double x = Math.Max(left, Math.Min(right, candidate)), dx = x - start.X;
                if (Math.Abs(dx) < 12 * scale || Math.Abs(dx) > 780 * scale) continue;
                bool duplicate = false;
                foreach (double old in considered) if (Math.Abs(old - x) < 20 * scale) { duplicate = true; break; }
                if (duplicate) continue;
                considered.Add(x);
                bool walkClear = true;
                int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(dx) / (12 * scale)));
                for (int i = 0; i <= steps && walkClear; i++)
                {
                    Rect body = Body(new Point(start.X + dx * i / steps, start.Y), width, height);
                    walkClear = InsideDesktop(body, monitors);
                    foreach (GanyuSceneWindow window in windows)
                    {
                        if (window.IsBackdrop || window.Handle == support) continue;
                        Rect hit = Rect.Intersect(body, window.Bounds);
                        if (!hit.IsEmpty && hit.Width > 0.5 && hit.Height > 0.5) { walkClear = false; break; }
                    }
                }
                if (!walkClear) continue;
                if (++tried > 6) break;
                Point point = new Point(x, start.Y);
                GanyuTraversal planned = PlanWithinBudget(platforms, windows, monitors, support, point, width, height, direction, watch, 110);
                if (planned != null) { approach = point; route = planned; return true; }
            }
            return false;
        }

        private static double Score(PlatformEdge target, Point start, int direction, double width)
        {
            double x = Math.Max(target.Left, Math.Min(target.Right, start.X)), dx = x - start.X;
            return Math.Abs(dx) + Math.Abs(target.Y - start.Y) * 0.25 + (Math.Sign(dx) == direction ? 0 : width * 0.65);
        }

        private static GanyuTraversal Finish(SearchNode node, GanyuHop landing, PlatformEdge target,
            IList<GanyuSceneWindow> windows, IList<Rect> monitors)
        {
            GanyuTraversal route = new GanyuTraversal { Landing = new PlatformEdge { Handle = target.Handle,
                Left = target.Left, Right = target.Right, Y = target.Y, Bottom = target.Bottom }, Monitors = new List<Rect>(monitors) };
            for (SearchNode n = node; n != null && n.Hop != null; n = n.Previous) route.Hops.Insert(0, n.Hop);
            route.Hops.Add(landing);
            foreach (GanyuSceneWindow window in windows)
            {
                bool used = window.Handle == target.Handle;
                foreach (GanyuHop hop in route.Hops) used |= hop.Contact != null && hop.Contact.Handle == window.Handle;
                if (used) route.RequiredWindows.Add(new GanyuSceneWindow { Handle = window.Handle, Bounds = window.Bounds,
                    CanBounce = window.CanBounce });
            }
            return route;
        }
    }

    public sealed partial class PetWindow
    {
        private List<GanyuSceneWindow> ganyuSceneWindows = new List<GanyuSceneWindow>();
        private DateTime ganyuSceneTimestamp = DateTime.MinValue;
        private GanyuTraversal ganyuTraversal;
        private int ganyuHopIndex;
        private double ganyuContactRemaining;
        private double ganyuLaunchPose;
        private DateTime nextGanyuSceneCheck;
        private bool ganyuApproaching;
        private Point ganyuApproachPoint;
        private GanyuTraversal ganyuApproachRoute;
        private DateTime ganyuApproachStarted;
        private const double GanyuContactSeconds = 0.09;

        private List<GanyuSceneWindow> GetGanyuScene(List<PlatformEdge> platforms)
        {
            if (ganyuSceneTimestamp == platformCacheTime) return ganyuSceneWindows;
            // Geometry-only callers (including offline simulation) can supply
            // the same platform snapshots without any native windows.
            List<GanyuSceneWindow> scene = new List<GanyuSceneWindow>();
            foreach (PlatformEdge platform in platforms)
            {
                if (platform.IsFloor) continue;
                bool found = false;
                foreach (GanyuSceneWindow window in scene) if (window.Handle == platform.Handle) { found = true; break; }
                if (!found) scene.Add(new GanyuSceneWindow { Handle = platform.Handle, CanBounce = true,
                    Bounds = new Rect(platform.Left, platform.Y, platform.Right - platform.Left, Math.Max(1, platform.Bottom - platform.Y)) });
            }
            return scene;
        }

        private static bool IsGanyuBackdrop(Rect bounds, IList<Rect> monitors)
        {
            foreach (Rect monitor in monitors)
                if (bounds.Left <= monitor.Left + 12 && bounds.Top <= monitor.Top + 12 &&
                    bounds.Right >= monitor.Right - 12 && bounds.Bottom >= monitor.Bottom - 12) return true;
            return false;
        }

        private void ClearGanyuTraversal()
        {
            ganyuApproaching = false;
            ganyuApproachRoute = null;
            ganyuTraversal = null;
            ganyuHopIndex = 0;
            ganyuContactRemaining = 0;
            ganyuLaunchPose = 0;
        }

        private bool StartGanyuTraversal(bool automatic)
        {
            List<PlatformEdge> platforms = GetPlatforms(false);
            List<GanyuSceneWindow> scene = GetGanyuScene(platforms);
            List<Rect> monitors = GetMonitorWorkAreas();
            GanyuTraversal route = GanyuTraversal.Plan(platforms, scene, monitors,
                supportHandle, new Point(CenterX, FootY), Width, Height, direction);
            if (route != null) { BeginGanyuRoute(route, automatic); return true; }
            Point approach;
            if (!GanyuTraversal.FindApproach(platforms, scene, monitors, supportHandle,
                new Point(CenterX, FootY), Width, Height, direction, out approach, out route)) return false;
            ClearGanyuTraversal();
            ganyuApproaching = true;
            ganyuApproachPoint = approach;
            ganyuApproachRoute = route;
            ganyuApproachStarted = DateTime.Now;
            jumpIsAutomatic = automatic;
            movement = "walk";
            movementStarted = ganyuApproachStarted;
            direction = approach.X >= CenterX ? 1 : -1;
            SetActiveGait("run", movementStarted);
            CancelWings();
            if (!quietMode) ShowSpeech("小跑过去，找个地方蹬上窗台。");
            return true;
        }

        private void BeginGanyuRoute(GanyuTraversal route, bool automatic)
        {
            ClearGanyuTraversal();
            ganyuTraversal = route;
            jumpIsAutomatic = automatic;
            movement = "jump";
            movementStarted = DateTime.Now;
            nextGanyuSceneCheck = DateTime.MinValue;
            jumpTarget = route.Landing;
            CancelWings();
            SetGanyuHop();
            if (!quietMode) ShowSpeech(route.Hops.Count > 1 ? "借一下力，蹬上窗台去。" : "轻轻一跃，就到另一扇窗口啦。");
        }

        private PlatformEdge GanyuWalkingSupport(PlatformEdge support)
        {
            if (support == null) return null;
            PlatformEdge span = new PlatformEdge { Handle = support.Handle, Left = support.Left, Right = support.Right,
                Y = support.Y, Bottom = support.Bottom, IsFloor = support.IsFloor };
            bool expanded;
            do
            {
                expanded = false;
                foreach (PlatformEdge part in GetPlatforms(false))
                    if (part.IsFloor == span.IsFloor && part.Handle == span.Handle && Math.Abs(part.Y - span.Y) < 1 &&
                        part.Left <= span.Right + 1 && part.Right >= span.Left - 1 &&
                        (part.Left < span.Left || part.Right > span.Right))
                    { span.Left = Math.Min(span.Left, part.Left); span.Right = Math.Max(span.Right, part.Right); expanded = true; }
            } while (expanded);
            return span;
        }

        private void UpdateGanyuApproach(DateTime now, double seconds)
        {
            PlatformEdge support = GanyuWalkingSupport(FindSupport());
            if (support == null || Math.Abs(support.Y - FootY) > 3)
            { BeginFall(0); return; }
            List<PlatformEdge> platforms = GetPlatforms(false);
            List<GanyuSceneWindow> scene = GetGanyuScene(platforms);
            if (currentCharacter != "ganyu" || state != "idle" || pointerHeld ||
                (jumpIsAutomatic && (!autoWalk || quietMode)) || (now - ganyuApproachStarted).TotalSeconds > 12 ||
                !GanyuRequiredWindowsValid(ganyuApproachRoute, scene) ||
                ganyuApproachPoint.X < support.Left + GanyuTraversal.SideInset(Width) - 0.01 ||
                ganyuApproachPoint.X > support.Right - GanyuTraversal.SideInset(Width) + 0.01)
            { StopWalking(); nextGanyuJump = now.AddSeconds(1.2); return; }
            double dx = ganyuApproachPoint.X - CenterX;
            direction = dx >= 0 ? 1 : -1;
            double step = Math.Min(Math.Abs(dx), moveSpeed * Math.Max(0, seconds));
            Point foot = new Point(CenterX + direction * step, FootY);
            Rect body = GanyuTraversal.Body(foot, Width, Height);
            bool clear = GanyuTraversal.InsideDesktop(body, GetMonitorWorkAreas());
            foreach (GanyuSceneWindow window in scene)
            {
                if (window.IsBackdrop || window.Handle == support.Handle) continue;
                Rect overlap = Rect.Intersect(body, window.Bounds);
                if (!overlap.IsEmpty && overlap.Width > 0.5 && overlap.Height > 0.5) { clear = false; break; }
            }
            if (!clear) { StopWalking(); nextGanyuJump = now.AddSeconds(1.2); return; }
            Left = foot.X - Width / 2;
            runPhase = MotionCycle.Advance(runPhase, step, Width * (activeGait == "stroll" ? 0.30 : 0.42));
            if (Math.Abs(foot.X - ganyuApproachPoint.X) > 0.01) return;
            bool automatic = jumpIsAutomatic;
            GanyuTraversal route = GanyuTraversal.Plan(platforms, scene, GetMonitorWorkAreas(), supportHandle,
                foot, Width, Height, direction);
            if (route != null) BeginGanyuRoute(route, automatic);
            else { StopWalking(); nextGanyuJump = now.AddSeconds(1.2); }
        }

        private void SetGanyuHop()
        {
            GanyuHop hop = ganyuTraversal.Hops[ganyuHopIndex];
            jumpStart = hop.Start; jumpEnd = hop.End; jumpHeight = hop.Lift;
            jumpDuration = hop.Duration; jumpProgress = 0; direction = hop.FacingAt(0);
            ganyuLaunchPose = ganyuHopIndex > 0 ? 1 : 0;
        }

        private bool GanyuRouteStillValid(DateTime now)
        {
            List<PlatformEdge> platforms = GetPlatforms(false);
            List<GanyuSceneWindow> scene = GetGanyuScene(platforms);
            IList<Rect> monitors = GetMonitorWorkAreas();
            bool landingFound = false;
            GanyuHop last = ganyuTraversal.Hops[ganyuTraversal.Hops.Count - 1];
            foreach (PlatformEdge platform in platforms)
            {
                if (!platform.IsFloor && platform.Handle == ganyuTraversal.Landing.Handle &&
                    Math.Abs(platform.Y - ganyuTraversal.Landing.Y) <= 3)
                {
                    PlatformEdge span = GanyuWalkingSupport(platform);
                    if (last.End.X - GanyuTraversal.SideInset(Width) - 3 >= span.Left - 0.01 &&
                        last.End.X + GanyuTraversal.SideInset(Width) + 3 <= span.Right + 0.01)
                    { landingFound = true; break; }
                }
            }
            if (!landingFound || !GanyuRequiredWindowsValid(ganyuTraversal, scene)) return false;
            if (now < nextGanyuSceneCheck) return true;
            nextGanyuSceneCheck = now.AddMilliseconds(100);
            for (int i = ganyuHopIndex; i < ganyuTraversal.Hops.Count; i++)
            {
                GanyuHop hop = ganyuTraversal.Hops[i];
                if (hop.Contact != null && hop.Contact.IsScreen && !monitors.Contains(hop.Contact.Monitor)) return false;
                if (!GanyuTraversal.ArcClear(hop, Width, Height, scene, monitors)) return false;
            }
            return true;
        }

        private bool GanyuRequiredWindowsValid(GanyuTraversal route, IList<GanyuSceneWindow> scene)
        {
            if (route == null) return false;
            foreach (GanyuSceneWindow expected in route.RequiredWindows)
            {
                bool found = false;
                foreach (GanyuSceneWindow current in scene)
                    if (current.Handle == expected.Handle && CloseBounds(current.Bounds, expected.Bounds)) { found = true; break; }
                if (!found) return false;
                if (ownHandle != IntPtr.Zero)
                {
                    NativeWindow.RECT raw;
                    int cloaked;
                    if (!NativeWindow.IsWindowVisible(expected.Handle) || NativeWindow.IsIconic(expected.Handle) ||
                        !NativeWindow.GetWindowRect(expected.Handle, out raw) ||
                        !CloseBounds(DesktopMovement.FromPixels(raw, fromDevice), expected.Bounds)) return false;
                    try { if (NativeWindow.DwmGetWindowAttribute(expected.Handle, 14, out cloaked, 4) == 0 && cloaked != 0) return false; }
                    catch (DllNotFoundException) { }
                }
            }
            return true;
        }

        private static bool CloseBounds(Rect a, Rect b)
        { return Math.Abs(a.Left - b.Left) <= 2 && Math.Abs(a.Top - b.Top) <= 2 && Math.Abs(a.Right - b.Right) <= 2 && Math.Abs(a.Bottom - b.Bottom) <= 2; }

        private void UpdateGanyuTraversal(DateTime now, double seconds)
        {
            if (ganyuTraversal == null || currentCharacter != "ganyu" ||
                (jumpIsAutomatic && (!autoWalk || quietMode)) || !GanyuRouteStillValid(now))
            { BeginFall(direction * 40 * Math.Max(0.6, Width / 165)); return; }
            double remaining = Math.Max(0, seconds);
            while (remaining > 0 && ganyuTraversal != null)
            {
                if (ganyuContactRemaining > 0)
                {
                    double spent = Math.Min(remaining, ganyuContactRemaining);
                    ganyuContactRemaining -= spent; remaining -= spent;
                    if (ganyuContactRemaining > 0.000001) return;
                    ganyuContactRemaining = 0;
                    ganyuHopIndex++;
                    SetGanyuHop();
                }
                double duration = Math.Min(remaining, (1 - jumpProgress) * jumpDuration);
                jumpProgress = Math.Min(1, jumpProgress + duration / jumpDuration);
                remaining -= duration;
                GanyuHop hop = ganyuTraversal.Hops[ganyuHopIndex];
                Point foot = hop.At(jumpProgress);
                direction = hop.FacingAt(jumpProgress);
                Left = foot.X - Width * 0.5;
                Top = foot.Y - Height + FootInset;
                ganyuLaunchPose = Math.Max(0, 1 - jumpProgress / 0.22);
                if (jumpProgress < 1 - 0.000001) return;
                if (ganyuHopIndex == ganyuTraversal.Hops.Count - 1)
                {
                    supportHandle = ganyuTraversal.Landing.Handle;
                    nextGanyuJump = now.AddSeconds(8 + random.Next(7));
                    StopWalking();
                    return;
                }
                // Hold the same point for a brief compression. Turning occurs
                // at takeoff, never by sliding vertically along the surface.
                ganyuContactRemaining = GanyuContactSeconds;
            }
        }

        private int GanyuJumpFrame(int count)
        {
            if (count <= 1) return 0;
            if (currentCharacter != "ganyu" || ganyuTraversal == null)
                return Math.Min(count - 1, (int)(jumpProgress * count));
            if (ganyuContactRemaining > 0) return 0;
            if (jumpProgress < 0.16) return Math.Min(1, count - 1);
            if (jumpProgress < 0.63) return Math.Min(2, count - 1);
            return count - 1;
        }

        private void ApplyGanyuJumpPose(TransformGroup transforms)
        {
            if (currentCharacter != "ganyu" || ganyuTraversal == null || movement != "jump") return;
            double contact = ganyuContactRemaining > 0 ? Math.Sin(Math.PI * (1 - ganyuContactRemaining / GanyuContactSeconds)) : 0;
            double launch = ganyuContactRemaining > 0 ? 0 : ganyuLaunchPose;
            // Local sprite units (330 square in the root Viewbox), scaled by
            // WPF with the pet. The push stays readable at every pet size.
            transforms.Children.Add(new ScaleTransform(1 + contact * 0.07 - launch * 0.025, 1 - contact * 0.09 + launch * 0.035));
            transforms.Children.Add(new RotateTransform(direction * (-launch * 8 + contact * 4)));
            transforms.Children.Add(new TranslateTransform(direction * contact * 18, contact * 4));
        }
    }
}
