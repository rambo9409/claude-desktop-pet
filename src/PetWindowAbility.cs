using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    internal enum WindowAbilityAction { Shrink, Pull }
    internal enum WindowAbilityPhase { Ready, RaiseHand, Restore, Animate, Verify, LowerHand }

    internal sealed class AbilityWindow
    {
        internal IntPtr Handle;
        internal uint ProcessId;
        internal Rect Bounds;
        internal Rect WorkArea;
        internal bool Maximized;
        internal bool Resizable;
    }

    internal interface IWindowAbilityDesktop
    {
        List<AbilityWindow> Candidates();
        AbilityWindow Refresh(AbilityWindow expected);
        bool RestoreWithoutActivation(AbilityWindow window);
        bool SetBounds(AbilityWindow window, Rect bounds);
    }

    // This clock uses wall time, including time spent on another character or
    // with the pet hidden. Tests pass explicit UTC values instead of sleeping.
    internal sealed class WindowAbilityClock
    {
        internal static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);
        internal static readonly TimeSpan DoubleClickReduction = TimeSpan.FromMinutes(1);
        internal DateTime NextUseUtc { get; private set; }

        internal WindowAbilityClock(DateTime nowUtc, DateTime savedNextUseUtc)
        {
            DateTime startup = nowUtc.AddSeconds(20);
            // A damaged preference or a clock correction cannot lock the
            // ability for longer than one complete cooldown.
            DateTime saved = savedNextUseUtc > nowUtc.Add(Cooldown) ? nowUtc.Add(Cooldown) : savedNextUseUtc;
            NextUseUtc = saved > startup ? saved : startup;
        }

        internal bool IsReady(DateTime nowUtc) { return nowUtc >= NextUseUtc; }
        internal void Use(DateTime nowUtc) { NextUseUtc = nowUtc.Add(Cooldown); }
        internal void RetryLater(DateTime nowUtc) { NextUseUtc = nowUtc.AddSeconds(15); }

        internal TimeSpan Remaining(DateTime nowUtc)
        {
            return NextUseUtc > nowUtc ? NextUseUtc - nowUtc : TimeSpan.Zero;
        }

        internal TimeSpan ReduceCooldown(DateTime nowUtc)
        {
            TimeSpan remaining = Remaining(nowUtc);
            remaining = remaining > DoubleClickReduction ? remaining - DoubleClickReduction : TimeSpan.Zero;
            // Ready clicks cannot bank a reduction for the next spell.
            NextUseUtc = nowUtc.Add(remaining);
            return remaining;
        }
    }

    internal static class WindowAbilityPlacement
    {
        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        internal static bool CanShrink(AbilityWindow window)
        {
            return window.Resizable && (window.Bounds.Width > 321 || window.Bounds.Height > 241);
        }

        internal static double Difference(Rect a, Rect b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) +
                Math.Abs(a.Width - b.Width) + Math.Abs(a.Height - b.Height);
        }

        internal static bool IsMeaningful(Rect start, Rect actual, WindowAbilityAction action)
        {
            if (action == WindowAbilityAction.Shrink)
                return start.Width - actual.Width >= Math.Min(40, start.Width * 0.08) ||
                    start.Height - actual.Height >= Math.Min(40, start.Height * 0.08);
            return (new Point(actual.X, actual.Y) - new Point(start.X, start.Y)).Length >= 48;
        }

        internal static bool IsOnMonitor(AbilityWindow window, Rect work)
        {
            Rect visible = Rect.Intersect(window.Bounds, work);
            return !visible.IsEmpty && visible.Width >= 120 && visible.Height >= 90;
        }

        internal static Rect Target(AbilityWindow window, WindowAbilityAction action, Rect pet, Rect petWork)
        {
            Rect work = action == WindowAbilityAction.Shrink ? window.WorkArea : petWork;
            if (work.IsEmpty || work.Width <= 0 || work.Height <= 0) work = window.WorkArea;
            double margin = Math.Min(10, Math.Min(work.Width, work.Height) / 8);
            double availableWidth = Math.Max(1, work.Width - margin * 2);
            double availableHeight = Math.Max(1, work.Height - margin * 2);
            double width = window.Bounds.Width;
            double height = window.Bounds.Height;
            double x, y;
            if (action == WindowAbilityAction.Shrink && window.Resizable)
            {
                width = Math.Min(width, Math.Max(320, width * 0.65));
                height = Math.Min(height, Math.Max(240, height * 0.65));
                x = window.Bounds.Left + (window.Bounds.Width - width) / 2;
                y = window.Bounds.Top + (window.Bounds.Height - height) / 2;
            }
            else
            {
                // Prefer a complete window beside the pet. If neither side has
                // room, keep the title bar and all window edges on the monitor.
                width = Math.Min(width, availableWidth);
                height = Math.Min(height, availableHeight);
                x = pet.Left - width - 22;
                if (x < work.Left + margin) x = pet.Right + 22;
                if (x + width > work.Right - margin) x = pet.Left + pet.Width / 2 - width / 2;
                y = pet.Bottom - height * 0.78;
            }
            width = Math.Min(width, availableWidth);
            height = Math.Min(height, availableHeight);
            x = Clamp(x, work.Left + margin, work.Right - margin - width);
            y = Clamp(y, work.Top + margin, work.Bottom - margin - height);
            return new Rect(x, y, width, height);
        }

        internal static Rect Between(Rect start, Rect target, double progress)
        {
            double p = Math.Max(0, Math.Min(1, progress));
            p = p * p * (3 - 2 * p);
            return new Rect(start.X + (target.X - start.X) * p,
                start.Y + (target.Y - start.Y) * p,
                start.Width + (target.Width - start.Width) * p,
                start.Height + (target.Height - start.Height) * p);
        }
    }

    internal sealed class WindowAbilityController
    {
        internal const double RaiseSeconds = 1.0;
        internal const double AnimateSeconds = 0.95;
        internal const double LowerSeconds = 0.72;
        private readonly IWindowAbilityDesktop desktop;
        private readonly Random random;
        private readonly Action<DateTime> saveCooldown;
        private AbilityWindow targetWindow;
        private Rect petBounds, petWorkArea, startingBounds, targetBounds;
        private double elapsed, totalElapsed, stableSeconds;
        private DateTime startedUtc;
        private Rect currentBounds, previousBounds;
        internal readonly WindowAbilityClock Clock;
        internal WindowAbilityPhase Phase { get; private set; }
        internal WindowAbilityAction Action { get; private set; }
        internal bool Active { get { return Phase != WindowAbilityPhase.Ready; } }
        internal bool Succeeded { get; private set; }
        internal double PhaseElapsed { get { return elapsed; } }
        internal double TotalElapsed { get { return totalElapsed; } }
        internal Rect StartingBounds { get { return startingBounds; } }
        internal Rect TargetBounds { get { return targetBounds; } }
        internal Rect CurrentBounds { get { return currentBounds; } }
        internal Rect PetBounds { get { return petBounds; } }
        internal Rect EffectArea { get { return targetWindow == null ? petWorkArea : Rect.Union(petWorkArea, targetWindow.WorkArea); } }

        internal WindowAbilityController(IWindowAbilityDesktop desktop, WindowAbilityClock clock,
            Random random, Action<DateTime> saveCooldown)
        {
            this.desktop = desktop; Clock = clock; this.random = random;
            this.saveCooldown = saveCooldown; Phase = WindowAbilityPhase.Ready;
        }

        internal TimeSpan ReduceCooldown(DateTime nowUtc)
        {
            TimeSpan remaining = Clock.ReduceCooldown(nowUtc);
            if (saveCooldown != null) saveCooldown(Clock.NextUseUtc);
            return remaining;
        }

        internal bool TryStart(DateTime nowUtc, Rect pet, Rect work)
        {
            if (Active || !Clock.IsReady(nowUtc)) return false;
            List<AbilityWindow> candidates;
            try { candidates = desktop.Candidates(); }
            catch { Clock.RetryLater(nowUtc); return false; }
            if (candidates == null || candidates.Count == 0)
            {
                Clock.RetryLater(nowUtc);
                return false;
            }
            List<AbilityWindow> local = candidates.FindAll(delegate(AbilityWindow candidate) {
                return candidate != null && WindowAbilityPlacement.IsOnMonitor(candidate, work);
            });
            if (local.Count > 0) candidates = local;
            int first = random.Next(candidates.Count);
            bool shrink = random.Next(2) == 0;
            targetWindow = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                AbilityWindow candidate = candidates[(first + i) % candidates.Count];
                if (candidate == null || candidate.Bounds.IsEmpty) continue;
                WindowAbilityAction action = shrink && WindowAbilityPlacement.CanShrink(candidate) &&
                    WindowAbilityPlacement.IsOnMonitor(candidate, work) ? WindowAbilityAction.Shrink : WindowAbilityAction.Pull;
                Rect planned = WindowAbilityPlacement.Target(candidate, action, pet, work);
                if (!WindowAbilityPlacement.IsMeaningful(candidate.Bounds, planned, action) &&
                    action == WindowAbilityAction.Pull && WindowAbilityPlacement.CanShrink(candidate))
                {
                    action = WindowAbilityAction.Shrink;
                    planned = WindowAbilityPlacement.Target(candidate, action, pet, work);
                }
                if (!candidate.Maximized && !WindowAbilityPlacement.IsMeaningful(candidate.Bounds, planned, action)) continue;
                targetWindow = candidate; Action = action; targetBounds = planned; break;
            }
            if (targetWindow == null) { Clock.RetryLater(nowUtc); return false; }
            petBounds = pet; petWorkArea = work; Succeeded = false;
            startingBounds = currentBounds = previousBounds = targetWindow.Bounds;
            Phase = WindowAbilityPhase.RaiseHand; elapsed = totalElapsed = stableSeconds = 0; startedUtc = nowUtc;
            Clock.Use(nowUtc);
            if (saveCooldown != null) saveCooldown(Clock.NextUseUtc);
            return true;
        }

        internal int BodyFrame
        {
            get
            {
                switch (Phase)
                {
                    case WindowAbilityPhase.RaiseHand: return elapsed < 0.25 ? 0 : elapsed < 0.60 ? 1 : 2;
                    case WindowAbilityPhase.Restore:
                    case WindowAbilityPhase.Animate:
                    case WindowAbilityPhase.Verify: return 2;
                    case WindowAbilityPhase.LowerHand: return elapsed < 0.30 ? 3 : elapsed < 0.50 ? 1 : 0;
                    default: return -1;
                }
            }
        }

        internal void Advance(double seconds)
        {
            if (!Active) return;
            double delta = Math.Max(0, Math.Min(0.05, seconds));
            elapsed += delta; totalElapsed += delta;
            try
            {
                if (Phase == WindowAbilityPhase.RaiseHand && elapsed >= RaiseSeconds)
                {
                    AbilityWindow current = desktop.Refresh(targetWindow);
                    if (current == null) { Finish(false); return; }
                    targetWindow = current;
                    currentBounds = current.Bounds;
                    if (current.Maximized)
                    {
                        if (!desktop.RestoreWithoutActivation(current)) { Finish(false); return; }
                        Phase = WindowAbilityPhase.Restore; elapsed = 0;
                    }
                    else BeginAnimation(current);
                }
                else if (Phase == WindowAbilityPhase.Restore)
                {
                    AbilityWindow current = desktop.Refresh(targetWindow);
                    if (current == null || elapsed >= 2) { Finish(false); return; }
                    if (!current.Maximized) BeginAnimation(current);
                }
                else if (Phase == WindowAbilityPhase.Animate)
                {
                    AbilityWindow current = desktop.Refresh(targetWindow);
                    if (current == null || current.Maximized) { Finish(false); return; }
                    currentBounds = current.Bounds;
                    double p = Math.Min(1, elapsed / AnimateSeconds);
                    if (!desktop.SetBounds(targetWindow, WindowAbilityPlacement.Between(startingBounds, targetBounds, p)))
                    { Finish(false); return; }
                    if (p >= 1) { Phase = WindowAbilityPhase.Verify; elapsed = stableSeconds = 0; previousBounds = currentBounds; }
                }
                else if (Phase == WindowAbilityPhase.Verify)
                {
                    AbilityWindow current = desktop.Refresh(targetWindow);
                    if (current == null || current.Maximized) { Finish(false); return; }
                    currentBounds = current.Bounds;
                    stableSeconds = WindowAbilityPlacement.Difference(previousBounds, currentBounds) < 2 ? stableSeconds + delta : 0;
                    previousBounds = currentBounds;
                    bool changed = WindowAbilityPlacement.IsMeaningful(startingBounds, currentBounds, Action) &&
                        WindowAbilityPlacement.Difference(currentBounds, targetBounds) < WindowAbilityPlacement.Difference(startingBounds, targetBounds) - 20;
                    // SetWindowPos with ASYNCWINDOWPOS only queues a request.
                    // Confirm visible geometry, allowing apps to enforce a minimum size.
                    if (changed && stableSeconds >= 0.12) Finish(true);
                    else if (elapsed >= 1.0) Finish(false);
                }
                else if (Phase == WindowAbilityPhase.LowerHand && elapsed >= LowerSeconds)
                    Cancel();
            }
            catch { Finish(false); }
        }

        private void BeginAnimation(AbilityWindow current)
        {
            targetWindow = current;
            startingBounds = current.Bounds;
            targetBounds = WindowAbilityPlacement.Target(current, Action, petBounds, petWorkArea);
            currentBounds = current.Bounds;
            if (!WindowAbilityPlacement.IsMeaningful(startingBounds, targetBounds, Action)) { Finish(false); return; }
            Phase = WindowAbilityPhase.Animate; elapsed = 0;
        }

        private void Finish(bool succeeded)
        {
            Succeeded = succeeded;
            if (!succeeded)
            {
                Clock.RetryLater(startedUtc.AddSeconds(totalElapsed));
                if (saveCooldown != null) saveCooldown(Clock.NextUseUtc);
            }
            Phase = WindowAbilityPhase.LowerHand; elapsed = 0;
        }

        internal void Cancel()
        {
            targetWindow = null; Phase = WindowAbilityPhase.Ready; elapsed = 0;
        }
    }

    internal static class WindowAbilityVisibility
    {
        internal static double VisibleArea(Rect bounds, Rect work, IList<Rect> above)
        {
            Rect onScreen = Rect.Intersect(bounds, work);
            if (onScreen.IsEmpty) return 0;
            List<Rect> pieces = new List<Rect> { onScreen };
            foreach (Rect cover in above)
            {
                List<Rect> next = new List<Rect>();
                foreach (Rect piece in pieces)
                {
                    Rect overlap = Rect.Intersect(piece, cover);
                    if (overlap.IsEmpty) { next.Add(piece); continue; }
                    Add(next, piece.Left, piece.Top, piece.Width, overlap.Top - piece.Top);
                    Add(next, piece.Left, overlap.Bottom, piece.Width, piece.Bottom - overlap.Bottom);
                    Add(next, piece.Left, overlap.Top, overlap.Left - piece.Left, overlap.Height);
                    Add(next, overlap.Right, overlap.Top, piece.Right - overlap.Right, overlap.Height);
                }
                pieces = next;
                if (pieces.Count == 0) return 0;
            }
            double area = 0;
            foreach (Rect piece in pieces) area += piece.Width * piece.Height;
            return area;
        }

        private static void Add(List<Rect> pieces, double x, double y, double width, double height)
        {
            if (width > 0 && height > 0) pieces.Add(new Rect(x, y, width, height));
        }

        internal static bool IsExposed(Rect bounds, Rect work, IList<Rect> above)
        {
            return VisibleArea(bounds, work, above) >= Math.Min(bounds.Width * bounds.Height * 0.18, 32000);
        }
    }

    internal sealed class NativeWindowAbilityDesktop : IWindowAbilityDesktop
    {
        private readonly Matrix fromDevice;
        private readonly Matrix toDevice;
        private readonly uint ownProcessId = (uint)Process.GetCurrentProcess().Id;

        internal NativeWindowAbilityDesktop(Matrix fromDevice)
        {
            this.fromDevice = fromDevice;
            toDevice = fromDevice;
            toDevice.Invert();
        }

        internal static bool IsCandidate(uint processId, uint ownProcessId, bool visible, bool iconic,
            bool cloaked, bool hung, bool owned, int style, int extendedStyle, string className,
            int titleLength, Rect bounds)
        {
            const int child = 0x40000000, caption = 0x00C00000, toolWindow = 0x00000080, noActivate = 0x08000000;
            if (processId == 0 || processId == ownProcessId || !visible || iconic || cloaked || hung || owned ||
                (style & child) != 0 || (style & caption) == 0 || (extendedStyle & (toolWindow | noActivate)) != 0 ||
                titleLength <= 0 || bounds.Width < 120 || bounds.Height < 90) return false;
            return className != "Progman" && className != "WorkerW" && className != "Shell_TrayWnd" &&
                className != "Shell_SecondaryTrayWnd" && className != "#32770";
        }

        public List<AbilityWindow> Candidates()
        {
            List<AbilityWindow> result = new List<AbilityWindow>();
            List<Rect> above = new List<Rect>();
            NativeWindow.EnumWindows(delegate(IntPtr handle, IntPtr unused) {
                AbilityWindow window = Read(handle, 0);
                if (window != null && WindowAbilityVisibility.IsExposed(window.Bounds, window.WorkArea, above)) result.Add(window);
                uint processId;
                NativeWindow.GetWindowThreadProcessId(handle, out processId);
                int cloaked;
                NativeWindow.DwmGetWindowAttribute(handle, 14, out cloaked, sizeof(int));
                NativeWindow.RECT raw;
                if (processId != ownProcessId && NativeWindow.IsWindowVisible(handle) && !NativeWindow.IsIconic(handle) &&
                    cloaked == 0 && (NativeWindow.GetWindowLong(handle, -20) & 0x20) == 0 &&
                    NativeWindow.GetWindowRect(handle, out raw) && raw.Right > raw.Left && raw.Bottom > raw.Top)
                    above.Add(new Rect(fromDevice.Transform(new Point(raw.Left, raw.Top)), fromDevice.Transform(new Point(raw.Right, raw.Bottom))));
                return true;
            }, IntPtr.Zero);
            return result;
        }

        public AbilityWindow Refresh(AbilityWindow expected)
        {
            return expected == null ? null : Read(expected.Handle, expected.ProcessId);
        }

        private AbilityWindow Read(IntPtr handle, uint expectedProcessId)
        {
            uint processId;
            NativeWindow.GetWindowThreadProcessId(handle, out processId);
            if (expectedProcessId != 0 && expectedProcessId != processId) return null;
            NativeWindow.RECT raw;
            if (!NativeWindow.GetWindowRect(handle, out raw) || raw.Right <= raw.Left || raw.Bottom <= raw.Top) return null;
            Rect bounds = new Rect(fromDevice.Transform(new Point(raw.Left, raw.Top)), fromDevice.Transform(new Point(raw.Right, raw.Bottom)));
            int cloaked;
            NativeWindow.DwmGetWindowAttribute(handle, 14, out cloaked, sizeof(int));
            StringBuilder name = new StringBuilder(128);
            AbilityNative.GetClassName(handle, name, name.Capacity);
            int style = NativeWindow.GetWindowLong(handle, -16);
            if (!IsCandidate(processId, ownProcessId, NativeWindow.IsWindowVisible(handle), NativeWindow.IsIconic(handle),
                cloaked != 0, AbilityNative.IsHungAppWindow(handle), AbilityNative.GetWindow(handle, 4) != IntPtr.Zero,
                style, NativeWindow.GetWindowLong(handle, -20), name.ToString(), NativeWindow.GetWindowTextLength(handle), bounds)) return null;
            NativeWindow.MONITORINFO monitor = new NativeWindow.MONITORINFO();
            monitor.Size = Marshal.SizeOf(typeof(NativeWindow.MONITORINFO));
            if (!NativeWindow.GetMonitorInfo(AbilityNative.MonitorFromWindow(handle, 2), ref monitor)) return null;
            Rect work = new Rect(fromDevice.Transform(new Point(monitor.Work.Left, monitor.Work.Top)),
                fromDevice.Transform(new Point(monitor.Work.Right, monitor.Work.Bottom)));
            return new AbilityWindow { Handle = handle, ProcessId = processId, Bounds = bounds, WorkArea = work,
                Maximized = AbilityNative.IsZoomed(handle), Resizable = (style & 0x00040000) != 0 };
        }

        public bool RestoreWithoutActivation(AbilityWindow window)
        {
            return Refresh(window) != null && AbilityNative.ShowWindowAsync(window.Handle, 4);
        }

        public bool SetBounds(AbilityWindow window, Rect bounds)
        {
            if (Refresh(window) == null) return false;
            Point a = toDevice.Transform(bounds.TopLeft), b = toDevice.Transform(bounds.BottomRight);
            // Async positioning cannot hang the pet on a busy external process.
            // Preserve activation, Z order, and owner Z order on every frame.
            return AbilityNative.SetWindowPos(window.Handle, IntPtr.Zero, (int)Math.Round(a.X), (int)Math.Round(a.Y),
                Math.Max(1, (int)Math.Round(b.X - a.X)), Math.Max(1, (int)Math.Round(b.Y - a.Y)), 0x4000 | 0x0010 | 0x0004 | 0x0200);
        }
    }

    internal static class AbilityNative
    {
        [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr handle);
        [DllImport("user32.dll")] internal static extern bool IsHungAppWindow(IntPtr handle);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr handle, uint command);
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr handle, StringBuilder text, int maximum);
        [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr handle, int command);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr handle, IntPtr after,
            int x, int y, int width, int height, uint flags);
    }

    public sealed partial class PetWindow : Window
    {
        private WindowAbilityController windowAbility;
        private bool IsWindowAbilityActive { get { return windowAbility != null && windowAbility.Active; } }
        private static string WindowAbilityPreferencePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet", "window-ability-cooldown.txt"); }
        }

        private WindowAbilityController EnsureWindowAbilityController(DateTime nowUtc)
        {
            if (windowAbility == null)
            {
                DateTime saved = DateTime.MinValue;
                try { DateTime.TryParseExact(File.ReadAllText(WindowAbilityPreferencePath).Trim(), "o", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out saved); } catch { }
                windowAbility = new WindowAbilityController(new NativeWindowAbilityDesktop(fromDevice),
                    new WindowAbilityClock(nowUtc, saved), random, delegate(DateTime next) {
                        try {
                            string path = WindowAbilityPreferencePath;
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            File.WriteAllText(path, next.ToString("o", CultureInfo.InvariantCulture), Encoding.UTF8);
                        } catch { }
                    });
            }
            return windowAbility;
        }

        // Called only after a completed, non-drag double click. It updates the
        // clock and speech, never enumerates or moves desktop windows.
        internal bool ReduceWindowAbilityCooldownFromDoubleClick()
        {
            if (currentCharacter != "kolo") return false;
            DateTime nowUtc = DateTime.UtcNow;
            WindowAbilityController controller = EnsureWindowAbilityController(nowUtc);
            bool wasReady = controller.Clock.IsReady(nowUtc);
            CancelWindowAbility();
            TimeSpan remaining = controller.ReduceCooldown(nowUtc);
            if (wasReady) ShowSpeech("窗口魔法已就绪！");
            else if (remaining == TimeSpan.Zero) ShowSpeech("冷却已缩短，窗口魔法已就绪！");
            else
            {
                int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
                string duration = seconds >= 60 ? (seconds / 60).ToString(CultureInfo.InvariantCulture) + " 分" : "";
                if (seconds % 60 != 0) duration += (duration.Length == 0 ? "" : " ") +
                    (seconds % 60).ToString(CultureInfo.InvariantCulture) + " 秒";
                ShowSpeech("冷却缩短 1 分钟，剩余 " + duration + "。" );
            }
            return true;
        }

        private void UpdateWindowAbility(DateTime now, double deltaSeconds)
        {
            // Offscreen test windows and constructor-time rendering never touch
            // native application windows or write the user's cooldown setting.
            if (!IsLoaded) return;
            DateTime nowUtc = now.ToUniversalTime();
            EnsureWindowAbilityController(nowUtc);
            bool eligible = currentCharacter == "kolo" && IsVisible && WindowState != WindowState.Minimized && !quietMode &&
                !pointerHeld && state == "idle" && (movement == "rest" || movement == "walk") &&
                (ContextMenu == null || !ContextMenu.IsOpen);
            if (!eligible) { CancelWindowAbility(); return; }
            if (!windowAbility.Active && windowAbility.TryStart(nowUtc, new Rect(Left, Top, Width, Height),
                GetWorkAreaForPoint(new Point(CenterX, FootY))))
            {
                StopWalking();
                ShowSpeech(windowAbility.Action == WindowAbilityAction.Shrink ? "举起手，让窗口小一点。" : "举起手，把窗口拉过来。" );
            }
            if (windowAbility.Active)
            {
                WindowAbilityPhase previousPhase = windowAbility.Phase;
                windowAbility.Advance(deltaSeconds);
                platformCacheTime = DateTime.MinValue;
                if (previousPhase != WindowAbilityPhase.LowerHand && windowAbility.Phase == WindowAbilityPhase.LowerHand)
                    ShowSpeech(windowAbility.Succeeded ? (windowAbility.Action == WindowAbilityAction.Shrink ?
                        "变小成功！5 分钟后再施法。" : "窗口拉到身边啦！5 分钟后再施法。") : "这个窗口没响应，稍后再试一次。" );
                UpdateAbilityEffects();
                if (!windowAbility.Active) ScheduleNextWalk(now);
            }
        }

        private BitmapSource PrepareAbilityFrame(BitmapSource requested)
        {
            if (!IsWindowAbilityActive || currentCharacter != "kolo") return requested;
            BitmapSource[] poses;
            if (!frames.TryGetValue("ability", out poses)) frames.TryGetValue("happy", out poses);
            int index = windowAbility.BodyFrame;
            return poses != null && poses.Length > 0 && index >= 0 ? poses[Math.Min(index, poses.Length - 1)] : requested;
        }

        private void CancelWindowAbility()
        {
            if (windowAbility != null) windowAbility.Cancel();
            CloseAbilityEffects();
        }
    }
}
