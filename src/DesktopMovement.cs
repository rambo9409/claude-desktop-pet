using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ClaudePet
{
    // Varesa follows an explicit arc-length path. Corners consume their share
    // of this tick's distance, so a fast step never drops time or teleports.
    internal sealed class VaresaRunTrack
    {
        private sealed class Segment
        {
            internal Point Start, End, Center;
            internal double Radius, StartAngle, Sweep, Length;
            internal Point At(double t, out double angle)
            {
                if (Radius == 0)
                {
                    angle = Math.Atan2(End.Y - Start.Y, End.X - Start.X) * 180 / Math.PI;
                    return Start + (End - Start) * t;
                }
                double degrees = StartAngle + Sweep * t;
                double radians = degrees * Math.PI / 180;
                angle = degrees + (Sweep > 0 ? 90 : -90);
                return Center + new Vector(Math.Cos(radians) * Radius, Math.Sin(radians) * Radius);
            }
        }
        private readonly List<Segment> segments = new List<Segment>();
        internal double Length { get; private set; }
        internal double Distance;
        internal bool Loop;
        internal int Facing;
        internal Rect Work;
        internal PlatformEdge Window;
        internal bool LandsOnWindow;

        private void Line(Point start, Point end)
        {
            double length = (end - start).Length;
            if (length < 0.000001) return;
            segments.Add(new Segment { Start = start, End = end, Length = length });
            Length += length;
        }
        private void Arc(Point center, double radius, double angle, double sweep)
        {
            double length = radius * Math.Abs(sweep) * Math.PI / 180;
            segments.Add(new Segment { Center = center, Radius = radius, StartAngle = angle, Sweep = sweep, Length = length });
            Length += length;
        }
        internal Point At(double distance, out double rotation)
        {
            if (Loop) distance = (distance % Length + Length) % Length;
            else distance = Math.Max(0, Math.Min(Length, distance));
            Segment selected = segments[segments.Count - 1];
            foreach (Segment segment in segments)
            {
                selected = segment;
                if (distance <= segment.Length) break;
                distance -= segment.Length;
            }
            double angle;
            Point point = selected.At(Math.Max(0, Math.Min(1, distance / selected.Length)), out angle);
            rotation = angle - (Facing < 0 ? 180 : 0);
            return point;
        }
        internal Point Advance(double distance, out double rotation)
        {
            Distance += Math.Max(0, distance);
            return At(Distance, out rotation);
        }
        internal static double ContactRadius(double width, double height) { return width / 2 - height * 0.038; }
        internal static double Fit(double degrees)
        {
            // Union of the four masked sprint frames, alpha >= 16:
            // (56,106)-(456,486), relative to their fixed (256,256) pivot.
            // A shared bound prevents per-frame size jitter; two pixels allow
            // filtering at the outer edge of the transparent window.
            double angle = degrees * Math.PI / 180;
            double c = Math.Abs(Math.Cos(angle)), s = Math.Abs(Math.Sin(angle));
            return Math.Min(1, 254 / Math.Max(200 * c + 230 * s, 230 * c + 200 * s));
        }
        internal static VaresaRunTrack Screen(Rect work, Point start, double width, double height, int facing)
        {
            double inset = ContactRadius(width, height), radius = width * 0.28;
            double l = work.Left + inset, r = work.Right - inset;
            double t = work.Top + inset, b = work.Bottom - inset;
            if (r - l <= 1 || b - t <= 1) return null;
            radius = Math.Min(radius, Math.Min(r - l, b - t) / 4);
            if (start.X < l + radius || start.X > r - radius) return null;
            VaresaRunTrack path = new VaresaRunTrack { Work = work, Facing = facing, Loop = true };
            double x = facing > 0 ? start.X : work.Left + work.Right - start.X;
            // Author the inside perimeter counterclockwise, then reflect its
            // x coordinates for a leftward launch without changing distances.
            path.Line(new Point(x, b), new Point(r - radius, b));
            path.Arc(new Point(r - radius, b - radius), radius, 90, -90);
            path.Line(new Point(r, b - radius), new Point(r, t + radius));
            path.Arc(new Point(r - radius, t + radius), radius, 0, -90);
            path.Line(new Point(r - radius, t), new Point(l + radius, t));
            path.Arc(new Point(l + radius, t + radius), radius, -90, -90);
            path.Line(new Point(l, t + radius), new Point(l, b - radius));
            path.Arc(new Point(l + radius, b - radius), radius, -180, -90);
            path.Line(new Point(l + radius, b), new Point(x, b));
            if (facing < 0) path.Reflect(work.Left + work.Right);
            return path;
        }
        private void Reflect(double axis)
        {
            foreach (Segment segment in segments)
            {
                segment.Start.X = axis - segment.Start.X;
                segment.End.X = axis - segment.End.X;
                segment.Center.X = axis - segment.Center.X;
                segment.StartAngle = 180 - segment.StartAngle;
                segment.Sweep = -segment.Sweep;
            }
        }
        internal static VaresaRunTrack WindowSide(PlatformEdge window, Rect work, Point start,
            double width, double height, int facing, bool ascending)
        {
            double inset = ContactRadius(width, height), turn = width * 0.28;
            // The side approached from the ground is opposite the side left
            // from the roof. Author a rightward route and reflect for the left.
            double near = facing > 0 ? window.Left : -window.Right;
            double far = facing > 0 ? window.Right : -window.Left;
            double x = start.X * facing, y = start.Y, roof = window.Y - inset;
            double min = facing > 0 ? work.Left : -work.Right;
            double max = facing > 0 ? work.Right : -work.Left;
            if (roof < work.Top + inset || far - near < width * 0.7) return null;
            VaresaRunTrack path = new VaresaRunTrack { Work = work, Facing = facing,
                Window = new PlatformEdge { Handle = window.Handle, Left = window.Left, Right = window.Right,
                    Y = window.Y, Bottom = window.Bottom }, LandsOnWindow = ascending };
            if (ascending)
            {
                double side = near - inset;
                if (side < min + inset || x > side - turn || y < window.Y + turn) return null;
                path.Line(new Point(x, y), new Point(side - turn, y));
                path.Arc(new Point(side - turn, y - turn), turn, 90, -90);
                path.Line(new Point(side, y - turn), new Point(side, window.Y));
                path.Arc(new Point(near, window.Y), inset, 180, 90);
                path.Line(new Point(near, roof), new Point(Math.Min(near + width * 0.45, far - width * 0.35), roof));
            }
            else
            {
                double side = far + inset;
                double bottom = Math.Min(window.Bottom, work.Bottom - inset);
                if (side > max - inset || x > far || bottom < window.Y + turn) return null;
                path.Line(new Point(x, y), new Point(far, roof));
                path.Arc(new Point(far, window.Y), inset, -90, 90);
                path.Line(new Point(side, window.Y), new Point(side, bottom));
            }
            if (facing < 0) path.Reflect(0);
            return path;
        }
    }

    internal static class DesktopMovement
    {
        internal static Rect WorkAreaForPoint(Point point, IList<Rect> monitors)
        {
            Rect closest = monitors.Count == 0 ? new Rect(0, 0, 1920, 1080) : monitors[0];
            double best = Double.MaxValue;
            foreach (Rect work in monitors)
            {
                if (work.Contains(point)) return work;
                double dx = Math.Max(work.Left - point.X, Math.Max(0, point.X - work.Right));
                double dy = Math.Max(work.Top - point.Y, Math.Max(0, point.Y - work.Bottom));
                double distance = dx * dx + dy * dy;
                if (distance < best) { best = distance; closest = work; }
            }
            return closest;
        }

        internal static Rect FromPixels(NativeWindow.RECT rectangle, Matrix fromDevice)
        {
            return new Rect(fromDevice.Transform(new Point(rectangle.Left, rectangle.Top)),
                fromDevice.Transform(new Point(rectangle.Right, rectangle.Bottom)));
        }

        internal static Point DragLocation(Point cursor, Point pointerOrigin, Point windowOrigin)
        {
            return windowOrigin + (cursor - pointerOrigin);
        }

        internal static Point JumpPosition(Point start, Point end, double progress, double height)
        {
            double t = Math.Max(0, Math.Min(1, progress));
            return new Point(start.X + (end.X - start.X) * t,
                start.Y + (end.Y - start.Y) * t - 4 * height * t * (1 - t));
        }

        internal static double ClimbSpeed(string character, double width)
        {
            return 30 * Math.Max(0.6, width / 165.0);
        }

        internal static double JumpHeight(Point start, Point end, double height, IList<Rect> monitors)
        {
            Rect startWork = WorkAreaForPoint(start, monitors);
            Rect endWork = WorkAreaForPoint(end, monitors);
            double available = Math.Min(start.Y, end.Y) - height * 0.962 - Math.Max(startWork.Top, endWork.Top) - 8;
            return Math.Min(available, Math.Max(height * 0.28, Math.Abs(end.X - start.X) * 0.2));
        }

        internal static bool BodyInsideDesktop(Point foot, double width, double height, IList<Rect> monitors)
        {
            // Require the main body, rather than the transparent sprite canvas,
            // to be covered by the union of usable display rectangles. Merely
            // checking the center misses gaps at staggered monitor boundaries.
            List<Rect> uncovered = new List<Rect> {
                new Rect(foot.X - width * 0.32, foot.Y - height * 0.86, width * 0.64, height * 0.86) };
            foreach (Rect work in monitors)
            {
                List<Rect> remaining = new List<Rect>();
                foreach (Rect piece in uncovered)
                {
                    Rect overlap = Rect.Intersect(piece, work);
                    if (overlap.IsEmpty || overlap.Width <= 0 || overlap.Height <= 0) { remaining.Add(piece); continue; }
                    if (overlap.Top > piece.Top) remaining.Add(new Rect(piece.Left, piece.Top, piece.Width, overlap.Top - piece.Top));
                    if (overlap.Bottom < piece.Bottom) remaining.Add(new Rect(piece.Left, overlap.Bottom, piece.Width, piece.Bottom - overlap.Bottom));
                    if (overlap.Left > piece.Left) remaining.Add(new Rect(piece.Left, overlap.Top, overlap.Left - piece.Left, overlap.Height));
                    if (overlap.Right < piece.Right) remaining.Add(new Rect(overlap.Right, overlap.Top, piece.Right - overlap.Right, overlap.Height));
                }
                uncovered = remaining;
                if (uncovered.Count == 0) return true;
            }
            return false;
        }

        internal static bool JumpArcInsideDesktop(Point start, Point end, double width, double height, double arcHeight, IList<Rect> monitors)
        {
            if (arcHeight < 24) return false;
            for (int step = 0; step <= 32; step++)
                if (!BodyInsideDesktop(JumpPosition(start, end, step / 32.0, arcHeight), width, height, monitors)) return false;
            return true;
        }

        internal static bool SameLanding(PlatformEdge expected, PlatformEdge current, double center)
        {
            return current != null && !current.IsFloor && current.Handle == expected.Handle &&
                Math.Abs(current.Y - expected.Y) <= 3 && Math.Abs(current.Left - expected.Left) <= 4 &&
                Math.Abs(current.Right - expected.Right) <= 4 && center >= current.Left && center <= current.Right;
        }

        internal static PlatformEdge ChooseJumpTarget(IList<PlatformEdge> platforms, IList<Rect> monitors,
            IntPtr sourceHandle, Point foot, double width, double height, int preferredDirection, out Point landing)
        {
            landing = foot;
            PlatformEdge selected = null;
            double best = Double.MaxValue;
            double scale = Math.Max(0.6, width / 165.0);
            foreach (PlatformEdge target in platforms)
            {
                if (target.IsFloor || target.Handle == sourceHandle || target.Right - target.Left < width * 0.68) continue;
                double margin = width * 0.34;
                double x = Math.Max(target.Left + margin, Math.Min(target.Right - margin, foot.X));
                // Avoid purely vertical teleports onto an overlapping window.
                if (Math.Abs(x - foot.X) < width * 0.28)
                    x = Math.Max(target.Left + margin, Math.Min(target.Right - margin,
                        foot.X + preferredDirection * width * 0.9));
                double dx = x - foot.X, dy = target.Y - foot.Y;
                if (Math.Abs(dx) < width * 0.28 || Math.Abs(dx) > 580 * scale || dy < -240 * scale || dy > 300 * scale) continue;
                Rect work = WorkAreaForPoint(new Point(x, target.Y - 1), monitors);
                if (target.Y - height * 0.962 < work.Top + 22 || x < work.Left + width * 0.34 || x > work.Right - width * 0.34) continue;
                double headroom = Math.Min(foot.Y, target.Y) - height * 0.962 - work.Top - 8;
                if (headroom < 28 * scale) continue;
                Point end = new Point(x, target.Y);
                if (!JumpArcInsideDesktop(foot, end, width, height, JumpHeight(foot, end, height, monitors), monitors)) continue;
                double score = Math.Abs(dx) + Math.Abs(dy) * 0.65 + (Math.Sign(dx) == preferredDirection ? 0 : width * 0.65);
                if (score < best) { best = score; selected = target; landing = new Point(x, target.Y); }
            }
            return selected;
        }

        internal static List<PlatformEdge> VisibleTopSegments(PlatformEdge edge, IList<Rect> occluders, double bodyHeight)
        {
            List<PlatformEdge> segments = new List<PlatformEdge> { edge };
            foreach (Rect cover in occluders)
            {
                if (cover.Bottom <= edge.Y - bodyHeight || cover.Top >= edge.Y + 3) continue;
                List<PlatformEdge> remaining = new List<PlatformEdge>();
                foreach (PlatformEdge segment in segments)
                {
                    if (cover.Right <= segment.Left || cover.Left >= segment.Right) { remaining.Add(segment); continue; }
                    if (cover.Left > segment.Left) remaining.Add(new PlatformEdge { Handle = edge.Handle, Left = segment.Left,
                        Right = cover.Left, Y = edge.Y, Bottom = edge.Bottom });
                    if (cover.Right < segment.Right) remaining.Add(new PlatformEdge { Handle = edge.Handle, Left = cover.Right,
                        Right = segment.Right, Y = edge.Y, Bottom = edge.Bottom });
                }
                segments = remaining;
            }
            return segments;
        }
    }

}
