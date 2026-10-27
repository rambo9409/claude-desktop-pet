using System;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ClaudePet
{
    // Render-only scene: preview tests use exactly this drawing without creating
    // an overlay HWND or touching any real application window.
    internal sealed class AbilityEffectVisual : FrameworkElement
    {
        private WindowAbilityController ability;
        private Rect scene;
        private static readonly Color Gold = Color.FromRgb(255, 202, 111);
        private static readonly Color Orange = Color.FromRgb(255, 143, 69);

        internal void Update(WindowAbilityController controller, Rect area)
        {
            ability = controller; scene = area;
            InvalidateVisual();
        }

        private static SolidColorBrush Brush(Color color, double opacity)
        {
            return new SolidColorBrush(Color.FromArgb((byte)(Math.Max(0, Math.Min(1, opacity)) * 255), color.R, color.G, color.B));
        }

        private Rect Local(Rect bounds)
        {
            bounds.Offset(-scene.X, -scene.Y); return bounds;
        }

        internal static Point HandPoint(Rect pet, int bodyFrame)
        {
            // The 512px ability atlas always faces front, even when the last
            // walk direction was left. Match the actual hand in each pose.
            Point uv = bodyFrame == 0 ? new Point(0.34, 0.68) : bodyFrame == 1 ? new Point(0.26, 0.49) :
                bodyFrame == 2 ? new Point(0.36, 0.25) : new Point(0.40, 0.58);
            return new Point(pet.Left + pet.Width * uv.X, pet.Bottom - pet.Width + pet.Width * uv.Y);
        }

        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);
            if (ability == null || !ability.Active) return;
            double time = ability.TotalElapsed;
            bool ending = ability.Phase == WindowAbilityPhase.LowerHand;
            double fade = ending ? Math.Max(0, 1 - ability.PhaseElapsed / WindowAbilityController.LowerSeconds) : 1;
            double charge = Math.Min(1, time / 0.65);
            Rect target = Local(ability.CurrentBounds);
            Rect goal = Local(ability.TargetBounds);
            Rect origin = Local(ability.StartingBounds);
            Point hand = HandPoint(ability.PetBounds, ability.BodyFrame);
            hand.Offset(-scene.X, -scene.Y);
            double radius = Math.Max(18, Math.Min(42, ability.PetBounds.Width * 0.135));

            drawing.PushOpacity(fade);
            if (ability.Phase == WindowAbilityPhase.Animate || ability.Phase == WindowAbilityPhase.Verify || ending)
            {
                Pen ghost = new Pen(Brush(Gold, 0.27), 1.5) { DashStyle = DashStyles.Dash };
                drawing.DrawRoundedRectangle(null, ghost, origin, 9, 9);
                drawing.DrawRoundedRectangle(null, new Pen(Brush(Gold, 0.32), 1) { DashStyle = DashStyles.Dot }, goal, 9, 9);
            }
            drawing.DrawRoundedRectangle(null, new Pen(Brush(Orange, 0.10 + 0.08 * charge), 13), target, 10, 10);
            drawing.DrawRoundedRectangle(null, new Pen(Brush(Orange, 0.65 * charge), 4), target, 8, 8);
            drawing.DrawRoundedRectangle(null, new Pen(Brush(Gold, 0.95 * charge), 1.4), target, 8, 8);

            Point tetherEnd = new Point(Math.Max(target.Left + 14, Math.Min(target.Right - 14, hand.X)),
                Math.Max(target.Top + 14, Math.Min(target.Bottom - 14, hand.Y)));
            // End on the nearest frame edge instead of covering the window's text.
            if (hand.X < target.Left) tetherEnd.X = target.Left;
            else if (hand.X > target.Right) tetherEnd.X = target.Right;
            else tetherEnd.Y = hand.Y > target.Top + target.Height / 2 ? target.Bottom : target.Top;
            Point control = new Point((hand.X + tetherEnd.X) / 2, Math.Min(hand.Y, tetherEnd.Y) - 70);
            StreamGeometry ribbon = new StreamGeometry();
            using (StreamGeometryContext path = ribbon.Open())
            {
                path.BeginFigure(hand, false, false); path.QuadraticBezierTo(control, tetherEnd, true, false);
            }
            drawing.DrawGeometry(null, new Pen(Brush(Orange, 0.13 * charge), 9), ribbon);
            drawing.DrawGeometry(null, new Pen(Brush(Gold, 0.72 * charge), 1.6), ribbon);
            for (int i = 0; i < 5; i++)
            {
                double t = (time * 0.70 + i / 5.0) % 1;
                Point point = Quadratic(hand, control, tetherEnd, t);
                DrawStar(drawing, point, 3.2 + Math.Sin(time * 7 + i) * 0.7, Brush(Gold, charge * (0.45 + t * 0.5)));
            }

            for (int i = 3; i >= 1; i--)
                drawing.DrawEllipse(Brush(Orange, charge * 0.035), null, hand, radius * (1 + i * 0.31), radius * (1 + i * 0.31));
            drawing.DrawEllipse(Brush(Gold, 0.08 * charge), new Pen(Brush(Gold, charge * 0.95), 1.8), hand, radius, radius);
            drawing.DrawEllipse(null, new Pen(Brush(Orange, charge * 0.78), 1.4), hand, radius * 0.71, radius * 0.71);
            for (int i = 0; i < 8; i++)
            {
                double angle = time * 1.3 + i * Math.PI / 4;
                Point point = new Point(hand.X + Math.Cos(angle) * radius, hand.Y + Math.Sin(angle) * radius);
                DrawStar(drawing, point, i % 2 == 0 ? 3.7 : 2.2, Brush(Gold, charge));
            }
            DrawStar(drawing, hand, radius * (0.30 + Math.Sin(time * 8) * 0.035), Brush(Gold, charge));
            if (ability.Action == WindowAbilityAction.Shrink)
            {
                double offset = 10 + Math.Sin(time * 5) * 3;
                DrawArrow(drawing, new Point(target.Left + offset, target.Top + offset), new Point(target.Left + 28, target.Top + 28), charge);
                DrawArrow(drawing, new Point(target.Right - offset, target.Bottom - offset), new Point(target.Right - 28, target.Bottom - 28), charge);
            }
            string label = ending ? (ability.Succeeded ? "施法成功" : "窗口未响应 · 稍后重试") :
                ability.Phase == WindowAbilityPhase.Verify ? "确认窗口变化…" :
                ability.Action == WindowAbilityAction.Shrink ? "缩小术 · 金色标记的窗口" : "牵引术 · 跟随星光移动";
            FormattedText text = new FormattedText(label, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), 13, Brush(Gold, 1), 1.0);
            double labelX = Math.Max(8, Math.Min(scene.Width - text.Width - 30, target.Left + 14));
            double labelY = Math.Max(8, Math.Min(scene.Height - 37, target.Top + 10));
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(238, 48, 31, 23)), new Pen(Brush(Gold, 0.7), 1),
                new Rect(labelX, labelY, text.Width + 24, 29), 8, 8);
            drawing.DrawText(text, new Point(labelX + 12, labelY + 5));
            drawing.Pop();
        }

        private static Point Quadratic(Point a, Point b, Point c, double t)
        {
            double r = 1 - t;
            return new Point(r * r * a.X + 2 * r * t * b.X + t * t * c.X,
                r * r * a.Y + 2 * r * t * b.Y + t * t * c.Y);
        }

        private static void DrawStar(DrawingContext drawing, Point center, double radius, Brush brush)
        {
            StreamGeometry star = new StreamGeometry();
            using (StreamGeometryContext path = star.Open())
            {
                path.BeginFigure(new Point(center.X, center.Y - radius), true, true);
                path.LineTo(new Point(center.X + radius * 0.3, center.Y - radius * 0.3), true, false);
                path.LineTo(new Point(center.X + radius, center.Y), true, false);
                path.LineTo(new Point(center.X + radius * 0.3, center.Y + radius * 0.3), true, false);
                path.LineTo(new Point(center.X, center.Y + radius), true, false);
                path.LineTo(new Point(center.X - radius * 0.3, center.Y + radius * 0.3), true, false);
                path.LineTo(new Point(center.X - radius, center.Y), true, false);
                path.LineTo(new Point(center.X - radius * 0.3, center.Y - radius * 0.3), true, false);
            }
            drawing.DrawGeometry(brush, null, star);
        }

        private static void DrawArrow(DrawingContext drawing, Point from, Point to, double opacity)
        {
            Pen pen = new Pen(Brush(Gold, opacity * 0.85), 2);
            drawing.DrawLine(pen, from, to);
            Vector axis = from - to; axis.Normalize();
            Vector normal = new Vector(-axis.Y, axis.X);
            drawing.DrawLine(pen, to, to + axis * 8 + normal * 4);
            drawing.DrawLine(pen, to, to + axis * 8 - normal * 4);
        }
    }

    internal sealed class AbilityEffectWindow : Window
    {
        internal readonly AbilityEffectVisual Surface = new AbilityEffectVisual();

        internal AbilityEffectWindow()
        {
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ShowActivated = false;
            Focusable = false; IsHitTestVisible = false;
            Content = Surface;
            SourceInitialized += delegate {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                NativeWindow.SetWindowLong(handle, -20, NativeWindow.GetWindowLong(handle, -20) |
                    0x08000000 | 0x00000020 | 0x00000080);
                HwndSource source = HwndSource.FromHwnd(handle);
                if (source != null) source.AddHook(delegate(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) {
                    if (message == 0x0084) { handled = true; return new IntPtr(-1); }
                    if (message == 0x0021) { handled = true; return new IntPtr(3); }
                    return IntPtr.Zero;
                });
            };
        }

        internal void Update(WindowAbilityController ability)
        {
            Rect area = ability.EffectArea;
            Left = area.Left; Top = area.Top; Width = area.Width; Height = area.Height;
            Surface.Update(ability, area);
        }
    }

    public sealed partial class PetWindow : Window
    {
        private AbilityEffectWindow abilityEffectWindow;

        private void UpdateAbilityEffects()
        {
            if (!IsWindowAbilityActive || !IsVisible || Opacity <= 0.001)
            {
                CloseAbilityEffects(); return;
            }
            if (abilityEffectWindow == null)
            {
                abilityEffectWindow = new AbilityEffectWindow { Owner = this };
                abilityEffectWindow.Update(windowAbility);
                abilityEffectWindow.Show();
            }
            else abilityEffectWindow.Update(windowAbility);
        }

        private void CloseAbilityEffects()
        {
            if (abilityEffectWindow == null) return;
            abilityEffectWindow.Close(); abilityEffectWindow = null;
        }
    }
}
