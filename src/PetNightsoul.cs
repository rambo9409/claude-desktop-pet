using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudePet
{
    // Energy is a fraction of a monitor-sized route, not a frame counter.
    // Emptying the reservoir locks sprint until its entire minute has elapsed.
    internal sealed class NightsoulEnergy
    {
        internal const double RecoverySeconds = 60;
        internal double Charge { get; private set; }
        internal bool Locked { get; private set; }
        internal bool CanSprint { get { return !Locked && Charge > 0; } }
        internal double RecoveryRemaining { get { return (1 - Charge) * RecoverySeconds; } }

        internal NightsoulEnergy() { Charge = 1; }

        internal static double RouteDistance(Rect work, double width, double height)
        {
            double inset = VaresaRunTrack.ContactRadius(width, height);
            double horizontal = Math.Max(0, work.Width - 2 * inset);
            double vertical = Math.Max(0, work.Height - 2 * inset);
            double radius = Math.Min(width * 0.28, Math.Min(horizontal, vertical) / 4);
            // Include the short inward move from the ordinary leftmost floor
            // placement to the tangent at which the rotated body fits.
            double approach = Math.Max(0, inset + radius - width * 0.18);
            return Math.Max(1, approach + vertical - 2 * radius + Math.PI * radius + horizontal / 2 - radius);
        }

        internal double AvailableDistance(double capacity)
        {
            return CanSprint ? Charge * Math.Max(1, capacity) : 0;
        }

        internal double SpendDistance(double requested, double capacity)
        {
            capacity = Math.Max(1, capacity);
            double distance = Math.Min(Math.Max(0, requested), AvailableDistance(capacity));
            if (distance <= 0) return 0;
            Charge = Math.Max(0, Charge - distance / capacity);
            if (Charge < 0.000000001) { Charge = 0; Locked = true; }
            return distance;
        }

        internal void Recover(double seconds, bool sprinting)
        {
            if (sprinting || seconds <= 0) return;
            Charge = Math.Min(1, Charge + seconds / RecoverySeconds);
            if (Charge >= 1 - 0.000000001) { Charge = 1; Locked = false; }
        }
    }

    public sealed partial class PetWindow
    {
        private readonly NightsoulEnergy nightsoul = new NightsoulEnergy();
        private bool nightsoulSpentThisTick;
        private DateTime nightsoulUpdatedAt = DateTime.MinValue;
        private Border nightsoulHud;
        private Border nightsoulFill;
        private TextBlock nightsoulLabel;

        private string ActiveMovementFrameKey
        {
            get { return currentCharacter == "varesa" && activeGait == "run" && !nightsoul.CanSprint ? "stroll" : MovementGait.FrameKey(activeGait); }
        }

        private double CurrentMovementSpeed(double variation)
        {
            string speedCharacter = currentCharacter == "varesa" && !nightsoul.CanSprint ? "kolo" : currentCharacter;
            return MovementGait.Speed(speedCharacter, activeGait, variation, Width);
        }

        private double NightsoulCapacity(Rect work)
        {
            return NightsoulEnergy.RouteDistance(work, Width, Height);
        }

        private double SpendNightsoul(double distance, Rect work)
        {
            double traveled = nightsoul.SpendDistance(distance, NightsoulCapacity(work));
            nightsoulSpentThisTick |= traveled > 0;
            return traveled;
        }

        private void ExhaustNightsoul()
        {
            moveSpeed = targetMoveSpeed = CurrentMovementSpeed(0.5);
            if (movement == "wallrun")
            {
                double radians = varesaRunAngle * Math.PI / 180;
                PlatformEdge support = Math.Cos(radians) > 0.999999 ? FindSupport() : null;
                if (support != null)
                {
                    ClearVaresaRun();
                    movement = "walk";
                    supportHandle = support.Handle;
                    Top = support.Y - Height + FootInset;
                }
                else
                {
                    ReleaseVaresaRun(true);
                    // Enter deployment now; PrepareGlidingFrame advances it once.
                    glideAnimation.Advance(true, 0);
                }
            }
            if (movement == "walk") SetMovementSprite(frames[ActiveMovementFrameKey][0]);
            if (!quietMode) ShowSpeech(movement == "fall" ? "夜魂用完啦，先滑翔休息一下。60 秒后恢复！" :
                "夜魂用完啦，先慢下来。60 秒后恢复！");
        }

        private void InitializeNightsoul(Grid root)
        {
            StackPanel content = new StackPanel();
            nightsoulLabel = new TextBlock { FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 21,
                FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(255, 224, 247)),
                TextAlignment = TextAlignment.Center, Text = "夜魂 100%", Height = 28 };
            content.Children.Add(nightsoulLabel);
            Border track = new Border { Height = 7, CornerRadius = new CornerRadius(3.5),
                Background = new SolidColorBrush(Color.FromRgb(74, 49, 98)), ClipToBounds = true };
            nightsoulFill = new Border { Height = 7, Width = 208, HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(3.5), Background = new LinearGradientBrush(
                    Color.FromRgb(165, 123, 255), Color.FromRgb(255, 146, 213), 0) };
            track.Child = nightsoulFill; content.Children.Add(track);
            nightsoulHud = new Border { Width = 228, Height = 49, Padding = new Thickness(9, 3, 9, 6),
                CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(190, 209, 160, 242)),
                Background = new SolidColorBrush(Color.FromArgb(230, 36, 24, 51)),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false, Visibility = Visibility.Collapsed, Child = content };
            root.Children.Add(nightsoulHud);
        }

        private void UpdateNightsoul(DateTime now, double deltaSeconds)
        {
            bool locked = nightsoul.Locked;
            double elapsed = nightsoulUpdatedAt == DateTime.MinValue ? Math.Max(0, deltaSeconds) :
                Math.Max(0, (now - nightsoulUpdatedAt).TotalSeconds);
            nightsoulUpdatedAt = now;
            nightsoul.Recover(elapsed, nightsoulSpentThisTick);
            nightsoulSpentThisTick = false;
            if (locked && !nightsoul.Locked && currentCharacter == "varesa" && movement == "walk" && activeGait == "run")
                SetActiveGait("run", now);
            if (nightsoulHud == null) return;
            nightsoulHud.Visibility = currentCharacter == "varesa" ? Visibility.Visible : Visibility.Collapsed;
            if (currentCharacter != "varesa") return;
            nightsoulFill.Width = 208 * nightsoul.Charge;
            nightsoulLabel.Text = nightsoul.Locked ? "夜魂恢复 " + Math.Ceiling(nightsoul.RecoveryRemaining).ToString(CultureInfo.InvariantCulture) + "s" :
                "夜魂 " + Math.Round(nightsoul.Charge * 100).ToString(CultureInfo.InvariantCulture) + "%";
            if (Double.IsNaN(Left) || Double.IsNaN(Top))
            {
                nightsoulHud.Margin = new Thickness(51, 128, 0, 0);
                return;
            }
            // The HUD remains horizontal. Along the ceiling, use the free area
            // underneath the inverted body rather than the clipped top margin.
            double scale = Width / 330;
            Rect work = GetWorkAreaForPoint(RunCenter);
            double angle = ((varesaRunAngle % 360) + 360) % 360;
            double desired = movement == "wallrun" && angle > 115 && angle < 245 ? 396 : 128;
            double minimum = Math.Max(2, (work.Top - Top) / scale + 6);
            double maximum = Math.Min(399, (work.Bottom - Top) / scale - 53);
            double top = Math.Max(minimum, Math.Min(maximum, desired));
            nightsoulHud.Margin = new Thickness(51, top, 0, 0);
        }
    }
}
