using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudePet
{
    internal enum KoloPortalPhase { None, Steady, Opening, Entering, Emerging, Closing }

    internal sealed class KoloPortalState
    {
        internal const double SteadyEnd = .85, OpenEnd = 1.35, EntryEnd = 1.90, EmergeEnd = 2.55, Duration = 2.95;
        internal bool Active { get; private set; }
        internal double Elapsed { get; private set; }
        internal double VelocityX { get; private set; }
        internal double VelocityY { get; private set; }
        internal bool AtDestination { get { return Elapsed >= EntryEnd; } }
        internal KoloPortalPhase Phase {
            get {
                if (!Active) return KoloPortalPhase.None;
                if (Elapsed < SteadyEnd) return KoloPortalPhase.Steady;
                if (Elapsed < OpenEnd) return KoloPortalPhase.Opening;
                if (Elapsed < EntryEnd) return KoloPortalPhase.Entering;
                return Elapsed < EmergeEnd ? KoloPortalPhase.Emerging : KoloPortalPhase.Closing;
            }
        }
        private static double Ease(double value)
        { value = Math.Max(0, Math.Min(1, value)); return value * value * (3 - 2 * value); }
        internal double BodyReveal {
            get {
                if (Phase == KoloPortalPhase.Entering) return 1 - .96 * Ease((Elapsed - OpenEnd) / (EntryEnd - OpenEnd));
                if (Phase == KoloPortalPhase.Emerging) return .04 + .96 * Ease((Elapsed - EntryEnd) / (EmergeEnd - EntryEnd));
                return 1;
            }
        }
        internal double BodyOpacity {
            get {
                if (Phase == KoloPortalPhase.Entering) return 1 - Ease((Elapsed - OpenEnd) / (EntryEnd - OpenEnd));
                if (Phase == KoloPortalPhase.Emerging) return Ease((Elapsed - EntryEnd) / (EmergeEnd - EntryEnd));
                return 1;
            }
        }
        internal double Opening {
            get {
                if (!Active || Elapsed < SteadyEnd) return 0;
                if (Elapsed < OpenEnd) return Ease((Elapsed - SteadyEnd) / (OpenEnd - SteadyEnd));
                if (Elapsed < EmergeEnd) return 1;
                return 1 - Ease((Elapsed - EmergeEnd) / (Duration - EmergeEnd));
            }
        }
        internal void Begin(double horizontal, double vertical, double width)
        {
            double scale = Math.Max(.6, width / 165);
            VelocityX = Math.Max(-40 * scale, Math.Min(40 * scale, horizontal));
            VelocityY = Math.Max(-15 * scale, Math.Min(42 * scale, vertical));
            Active = true; Elapsed = 0;
        }
        // Returns the single invisible relocation boundary; drawing never advances time.
        internal bool Advance(double seconds, out Vector drift)
        {
            drift = new Vector();
            if (!Active || Double.IsNaN(seconds) || seconds <= 0) return false;
            seconds = Math.Min(.05, seconds);
            double before = Elapsed, settling = Math.Min(seconds, Math.Max(0, SteadyEnd - before));
            if (settling > 0) {
                double xDecay = Math.Exp(-5 * settling), yDecay = Math.Exp(-4.2 * settling);
                drift = new Vector(VelocityX * (1 - xDecay) / 5, VelocityY * (1 - yDecay) / 4.2);
                VelocityX *= xDecay; VelocityY *= yDecay;
            }
            Elapsed = Math.Min(Duration, Elapsed + seconds);
            if (Elapsed >= Duration) Active = false;
            return before < EntryEnd && Elapsed >= EntryEnd;
        }
        internal void Cancel() { Active = false; Elapsed = 0; VelocityX = 0; VelocityY = 0; }
    }

    internal static class KoloPortalGeometry
    {
        internal static Point FloorLocation(Point anchor, Size pet, IList<Rect> monitors)
        {
            Rect work = DesktopMovement.WorkAreaForPoint(anchor, monitors);
            double left = Math.Max(work.Left, Math.Min(work.Right - pet.Width, anchor.X - pet.Width / 2));
            return new Point(left, work.Bottom - pet.Height + pet.Height * .038);
        }
        internal static Point ContainAir(Point location, Size pet, Rect work)
        {
            return new Point(Math.Max(work.Left, Math.Min(work.Right - pet.Width, location.X)),
                Math.Max(work.Top, Math.Min(work.Bottom - pet.Height + pet.Height * .038, location.Y)));
        }
    }

    internal sealed class KoloPortalVisual : FrameworkElement
    {
        private double opening, phase;
        internal void Update(double amount, double time) { opening = amount; phase = time; InvalidateVisual(); }
        protected override void OnRender(DrawingContext draw)
        {
            base.OnRender(draw);
            if (opening <= .001) return;
            Point foot = new Point(165, 432);
            double width = 80 * opening, height = 15 * opening;
            draw.DrawEllipse(new SolidColorBrush(Color.FromArgb(48, 130, 102, 166)), null, foot, width + 5, height + 4);
            draw.DrawEllipse(new SolidColorBrush(Color.FromRgb(3, 2, 6)),
                new Pen(new SolidColorBrush(Color.FromRgb(151, 125, 186)), 1.8), foot, width, height);
            draw.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(140, 75, 57, 100)), 1), foot, width * .77, height * .67);
            for (int i = 0; i < 6; i++) {
                double angle = phase * 1.8 + i * Math.PI / 3;
                Point p = new Point(foot.X + Math.Cos(angle) * width, foot.Y + Math.Sin(angle) * height);
                draw.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(180 * opening), 211, 193, 235)), null, p, 1.4, 1.4);
            }
        }
    }

    public sealed partial class PetWindow
    {
        private readonly KoloPortalState koloPortal = new KoloPortalState();
        private KoloPortalVisual koloPortalVisual;
        private Point koloPortalAnchor;
        private bool IsKoloPortalActive { get { return koloPortal.Active; } }

        private void InitializeKoloPortal(Grid root)
        {
            koloPortalVisual = new KoloPortalVisual { IsHitTestVisible = false };
            root.Children.Add(koloPortalVisual);
            StateChanged += delegate { if (WindowState == WindowState.Minimized) CancelKoloPortal(); };
        }
        private bool TryBeginKoloPortal(double horizontal, double vertical)
        {
            if (currentCharacter != "kolo") return false;
            Point anchor = new Point(CenterX, Top + Height / 2);
            Rect work = GetWorkAreaForPoint(anchor);
            if (work.Bottom - FootY <= Math.Max(28, Height * .2)) return false;
            CancelKoloEasterEgg(); CancelWindowAbility(); ClearGanyuTraversal(); ClearVaresaRun(); CancelButterflyEffects(); CancelWings();
            koloPortalAnchor = anchor;
            koloPortal.Begin(horizontal, vertical, Width);
            movement = "portal"; state = "idle"; stateEnds = DateTime.MaxValue;
            supportHandle = IntPtr.Zero; fallVelocityX = 0; fallVelocityY = 0; heldTilt = 0;
            movementStarted = DateTime.Now;
            Point safe = KoloPortalGeometry.ContainAir(new Point(Left, Top), new Size(Width, Height), work);
            Left = safe.X; Top = safe.Y;
            return true;
        }
        private void PlaceAtKoloPortalDestination(bool refresh)
        {
            // Force a fresh native monitor snapshot at the invisible jump and
            // at completion. Unshown tests retain their injected monitor list.
            if (refresh && ownHandle != IntPtr.Zero) monitorsCachedAt = DateTime.MinValue;
            Point destination = KoloPortalGeometry.FloorLocation(koloPortalAnchor, new Size(Width, Height), GetMonitorWorkAreas());
            Left = destination.X; Top = destination.Y;
        }
        private void UpdateKoloPortal(double seconds)
        {
            if (!IsKoloPortalActive) return;
            if (currentCharacter != "kolo" || pointerHeld) { CancelKoloPortal(); return; }
            Vector drift;
            bool relocate = koloPortal.Advance(seconds, out drift);
            if (relocate) PlaceAtKoloPortalDestination(true);
            else if (koloPortal.AtDestination) PlaceAtKoloPortalDestination(false);
            else {
                Rect work = GetWorkAreaForPoint(new Point(CenterX, Top + Height / 2));
                Point safe = KoloPortalGeometry.ContainAir(new Point(Left + drift.X, Top + drift.Y), new Size(Width, Height), work);
                Left = safe.X; Top = safe.Y;
                koloPortalAnchor = new Point(CenterX, Top + Height / 2);
            }
            if (!IsKoloPortalActive) {
                PlaceAtKoloPortalDestination(true);
                movement = "rest"; supportHandle = IntPtr.Zero; fallVelocityX = 0; fallVelocityY = 0;
                landedFromFall = true; movementStarted = DateTime.Now;
                RestoreKoloPortalDrawing(); ScheduleNextWalk(DateTime.Now);
                ShowSpeech("传送完成，稳稳回到底部啦。");
            }
        }
        private void RestoreKoloPortalDrawing()
        {
            if (sprite != null) {
                sprite.Opacity = 1; sprite.OpacityMask = null; sprite.Clip = null; sprite.RenderTransform = Transform.Identity;
                sprite.RenderTransformOrigin = new Point(.5, .82);
            }
            if (koloPortalVisual != null) koloPortalVisual.Update(0, 0);
        }
        private void CancelKoloPortal()
        {
            if (!IsKoloPortalActive && movement != "portal") return;
            koloPortal.Cancel(); RestoreKoloPortalDrawing();
            if (movement == "portal") {
                movement = "fall"; movementStarted = DateTime.Now; supportHandle = IntPtr.Zero;
                fallFromHandle = IntPtr.Zero; fallVelocityX = 0; fallVelocityY = 16 * Math.Max(.6, Width / 165);
                landedFromFall = false;
            }
        }
        // Call after the regular sprite/effect renderers. Slide the intact
        // artwork through a fixed shoe-height plane instead of squashing it.
        private void RenderKoloPortal()
        {
            if (!IsKoloPortalActive) return;
            double width = Math.Max(1, sprite.ActualWidth), height = Math.Max(1, sprite.ActualHeight);
            double travel = (1 - koloPortal.BodyReveal) * width;
            double foot = height - width * (26.0 / 512);
            sprite.RenderTransformOrigin = new Point(.5, .82);
            sprite.RenderTransform = new TranslateTransform(0, travel);
            sprite.Clip = travel > .001 ? new RectangleGeometry(new Rect(0, 0, width, Math.Max(0, foot - travel))) : null;
            sprite.Opacity = koloPortal.BodyOpacity;
            if (koloPortalVisual != null) koloPortalVisual.Update(koloPortal.Opening, koloPortal.Elapsed);
        }
    }
}
