using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    internal static class GlidePhysics
    {
        internal const double CruisingFallSpeed = 88;

        // Wings catch the air immediately on release, then settle to a slow
        // descent. Drag momentum cannot turn an open glider into a free fall.
        internal static double Step(ref double horizontal, ref double vertical,
            double openness, double width, double seconds)
        {
            double dt = Math.Max(0, Math.Min(0.05, seconds));
            if (dt == 0) return 0;
            double scale = Math.Max(0.6, width / 165.0);
            double open = GlideAnimation.Ease(openness);
            double limit = (140 - 52 * open) * scale;
            vertical = Math.Max(-90 * scale, Math.Min(limit, vertical));
            double before = vertical;
            double target = CruisingFallSpeed * scale;
            if (vertical < 0) vertical = Math.Min(0, vertical + 160 * scale * dt);
            else vertical = target + (vertical - target) * Math.Exp(-3.5 * dt);
            horizontal = Math.Max(-110 * scale, Math.Min(110 * scale, horizontal));
            horizontal *= Math.Exp(-0.45 * dt);
            return (before + vertical) * 0.5 * dt;
        }
    }

    internal enum GlidePhase { Hidden, Deploy, Glide, Retract, Land }

    // One reversible clock owns both the person and the two wing halves. A
    // held-to-fall change keeps the same airborne episode and pose cadence.
    internal sealed class GlideAnimation
    {
        internal const double DeployDuration = 0.56;
        internal const double RetractDuration = 0.24;
        internal const double LandingDuration = 0.20;
        internal const double GlideFrameDuration = 0.24;
        internal GlidePhase Phase { get; private set; }
        internal double Openness { get; private set; }
        internal double Elapsed { get; private set; }
        internal double AirTime { get; private set; }
        private double closingStart;

        internal int BodyFrame
        {
            get
            {
                switch (Phase)
                {
                    case GlidePhase.Deploy: return Math.Min(2, (int)(Openness * 3));
                    case GlidePhase.Glide:
                        int step = (int)(Elapsed / GlideFrameDuration) % 4;
                        // Neutral -> left balance -> neutral -> right balance.
                        return step == 1 ? 4 : step == 3 ? 5 : 3;
                    case GlidePhase.Retract: return 6;
                    case GlidePhase.Land: return 7;
                    default: return -1;
                }
            }
        }

        internal void Advance(bool airborne, double seconds)
        {
            double dt = Math.Max(0, Math.Min(0.05, seconds));
            if (airborne)
            {
                if (Phase == GlidePhase.Hidden || Phase == GlidePhase.Retract || Phase == GlidePhase.Land)
                {
                    Phase = GlidePhase.Deploy;
                    Elapsed = Openness * DeployDuration;
                }
                AirTime += dt;
                Elapsed += dt;
                if (Phase == GlidePhase.Deploy)
                {
                    Openness = Math.Min(1, Elapsed / DeployDuration);
                    if (Openness >= 1)
                    {
                        Phase = GlidePhase.Glide;
                        Elapsed -= DeployDuration;
                    }
                }
                return;
            }

            if (Phase == GlidePhase.Deploy || Phase == GlidePhase.Glide)
            {
                closingStart = Openness;
                Phase = GlidePhase.Retract;
                Elapsed = 0;
            }
            if (Phase == GlidePhase.Retract)
            {
                Elapsed += dt;
                Openness = Math.Max(0, closingStart - Elapsed / RetractDuration);
                if (Openness <= 0)
                {
                    double remainder = Math.Max(0, Elapsed - closingStart * RetractDuration);
                    Phase = GlidePhase.Land;
                    Elapsed = remainder;
                }
            }
            else if (Phase == GlidePhase.Land) Elapsed += dt;
            if (Phase == GlidePhase.Land && Elapsed >= LandingDuration) Reset();
        }

        internal void Reset()
        {
            Phase = GlidePhase.Hidden;
            Openness = Elapsed = AirTime = closingStart = 0;
        }

        internal static double Ease(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }
    }

    public sealed partial class PetWindow
    {
        private Image wingLayer;
        private Image rightWingLayer;
        private string wingsCharacter;
        private readonly GlideAnimation glideAnimation = new GlideAnimation();
        private BitmapSource wingBoundsSource;
        private Rect wingSourceBounds;
        private const double WingRootY = 340.0 / 512.0;

        private bool HasGlider
        {
            get { return currentCharacter != "kolo" && frames.ContainsKey("glide") && frames.ContainsKey("glider"); }
        }

        private double GlidingTopInset()
        {
            double firstPixel = 1;
            foreach (string key in new[] { "glide", "glider" })
            foreach (BitmapSource frame in frames[key])
            {
                Rect visible;
                if (!visiblePixelCache.TryGetValue(frame, out visible))
                {
                    visible = SpeechWindow.FindVisiblePixels(frame);
                    visiblePixelCache[frame] = visible;
                }
                firstPixel = Math.Min(firstPixel, visible.Top);
            }
            // The square sprite sits at the bottom of a taller transparent
            // window. Constrain painted pixels, including a tilt allowance,
            // rather than pushing that empty band below the screen edge.
            return Math.Max(0, Height - Width + Width * (firstPixel - 0.06));
        }

        private void InitializeWings(Grid root)
        {
            wingLayer = CreateWingHalf();
            rightWingLayer = CreateWingHalf();
            root.Children.Add(wingLayer);
            root.Children.Add(rightWingLayer);
            wingsCharacter = currentCharacter;
        }

        private Image CreateWingHalf()
        {
            return new Image {
                Stretch = sprite.Stretch,
                HorizontalAlignment = sprite.HorizontalAlignment,
                VerticalAlignment = sprite.VerticalAlignment,
                Margin = sprite.Margin,
                RenderTransformOrigin = sprite.RenderTransformOrigin,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
        }

        private void HideWingLayers()
        {
            foreach (Image layer in new[] { wingLayer, rightWingLayer })
            {
                if (layer == null) continue;
                layer.Visibility = Visibility.Collapsed;
                layer.Source = null;
                layer.RenderTransform = Transform.Identity;
                layer.Opacity = 0;
            }
        }

        private void CancelWings()
        {
            glideAnimation.Reset();
            wingsCharacter = currentCharacter;
            HideWingLayers();
        }

        // Called once by OnTick, before the effects renderer consumes the body.
        private BitmapSource PrepareGlidingFrame(BitmapSource requested, double seconds)
        {
            if (wingsCharacter != currentCharacter) CancelWings();
            BitmapSource[] poses, glider;
            if (currentCharacter == "kolo" || state != "idle" || (IsLoaded && !IsVisible) ||
                !frames.TryGetValue("glide", out poses) || poses.Length != 8 ||
                !frames.TryGetValue("glider", out glider) || glider.Length == 0)
            {
                CancelWings();
                return requested;
            }

            bool airborne = movement == "held" || movement == "fall";
            // A fresh walking/climbing/jumping command takes precedence over
            // landing. Only actual ground rest plays the retract/landing poses.
            if (!airborne && movement != "rest")
            {
                CancelWings();
                return requested;
            }
            glideAnimation.Advance(airborne, seconds);
            int frame = glideAnimation.BodyFrame;
            return frame < 0 ? requested : poses[frame];
        }

        private void RenderWings(Transform pose)
        {
            BitmapSource[] glider;
            if (wingLayer == null || glideAnimation.Openness <= 0 ||
                !frames.TryGetValue("glider", out glider) || glider.Length == 0)
            {
                HideWingLayers();
                return;
            }

            double p = glideAnimation.Openness;
            // The spars reach their full span before the final body pose, then
            // flex a little under load and settle into the gliding posture.
            double open = GlideAnimation.Ease(p / 0.84);
            double settle = glideAnimation.Phase == GlidePhase.Deploy ?
                Math.Sin(Math.Max(0, (p - 0.55) / 0.45) * Math.PI) * 0.04 : 0;
            double breeze = glideAnimation.Phase == GlidePhase.Glide ?
                Math.Sin(glideAnimation.AirTime * 3.2) : 0;
            double spread = 0.24 + 0.76 * open + settle + 0.008 * breeze;
            double rise = 0.86 + 0.14 * open;
            double fold = 58 * (1 - open) - 2.5 * settle + 0.65 * breeze;
            double opacity = GlideAnimation.Ease(Math.Min(1, p * 5));

            double height = sprite.ActualHeight > 0 ? sprite.ActualHeight : 330;
            double width = sprite.ActualWidth > 0 ? sprite.ActualWidth : 330;
            double rootOffsetY = height * (WingRootY - sprite.RenderTransformOrigin.Y);
            double attachmentOffset = height * (WingAttachmentY() / 512.0 - WingRootY);
            if (!ReferenceEquals(wingBoundsSource, glider[0]))
            {
                wingBoundsSource = glider[0];
                wingSourceBounds = SpeechWindow.FindVisiblePixels(glider[0]);
            }

            // Each half turns around the same shoulder root, like two rigid
            // glider spars opening. It is deliberately not a bird flap cycle.
            TransformGroup left = FoldTransform(spread, rise, fold, rootOffsetY);
            TransformGroup right = FoldTransform(spread, rise, -fold, rootOffsetY);
            double fit = FitWingSpread(pose, width, height, rootOffsetY, attachmentOffset, left, right);
            Image[] layers = { wingLayer, rightWingLayer };
            TransformGroup[] folds = { left, right };
            for (int half = 0; half < 2; half++)
            {
                Image layer = layers[half];
                layer.Source = glider[0];
                layer.Clip = new RectangleGeometry(new Rect(half == 0 ? 0 : width / 2,
                    0, width / 2, height));
                layer.Visibility = Visibility.Visible;
                layer.Opacity = opacity;
                TransformGroup combined = folds[half];
                combined.Children.Add(new ScaleTransform(fit, fit, 0, rootOffsetY));
                // The painted landing preparation straightens the legs and
                // raises the shoulders; the back attachment follows that pose.
                combined.Children.Add(new TranslateTransform(0, attachmentOffset));
                combined.Children.Add(pose);
                layer.RenderTransform = combined;
            }
        }

        private double WingAttachmentY()
        {
            if (glideAnimation.Phase != GlidePhase.Retract) return 340;
            return currentCharacter == "hutao" ? 324 : currentCharacter == "ganyu" ? 323 : 316;
        }

        private static TransformGroup FoldTransform(double spread, double rise, double angle, double rootY)
        {
            TransformGroup fold = new TransformGroup();
            fold.Children.Add(new ScaleTransform(spread, rise, 0, rootY));
            fold.Children.Add(new RotateTransform(angle, 0, rootY));
            return fold;
        }

        private double FitWingSpread(Transform pose, double width, double height,
            double rootOffsetY, double attachmentOffset, Transform left, Transform right)
        {
            Point root = new Point(0, rootOffsetY);
            Point placedRoot = pose.Transform(new Point(root.X, root.Y + attachmentOffset));
            double screenRootX = placedRoot.X + width * sprite.RenderTransformOrigin.X;
            double screenRootY = placedRoot.Y + height * sprite.RenderTransformOrigin.Y;
            double fit = 1;
            for (int half = 0; half < 2; half++)
            {
                Rect bounds = wingSourceBounds;
                bounds.Intersect(new Rect(half * 0.5, 0, 0.5, 1));
                if (bounds.IsEmpty) continue;
                Transform fold = half == 0 ? left : right;
                foreach (Point corner in new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomLeft, bounds.BottomRight })
                {
                    Point point = new Point((corner.X - 0.5) * width,
                        (corner.Y - sprite.RenderTransformOrigin.Y) * height);
                    Point folded = fold.Transform(point);
                    Point offset = pose.Transform(new Point(folded.X, folded.Y + attachmentOffset));
                    double dx = offset.X - placedRoot.X, dy = offset.Y - placedRoot.Y;
                    if (dx < -0.001) fit = Math.Min(fit, (screenRootX - 4) / -dx);
                    if (dx > 0.001) fit = Math.Min(fit, (width - 4 - screenRootX) / dx);
                    if (dy < -0.001) fit = Math.Min(fit, (screenRootY - 4) / -dy);
                    if (dy > 0.001) fit = Math.Min(fit, (height - 4 - screenRootY) / dy);
                }
            }
            return Math.Max(0, Math.Min(1, fit));
        }
    }
}
