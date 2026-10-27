using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using ClaudePet;

internal static class WindowAbilityTests
{
    private static int checks;
    private static readonly DateTime Now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Rect Work = new Rect(0, 0, 1920, 1040);
    private static readonly Rect Pet = new Rect(1500, 815, 165, 225);
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(PetWindow pet, string field, object value) { typeof(PetWindow).GetField(field, Private).SetValue(pet, value); }
    private static T Get<T>(PetWindow pet, string field) { return (T)typeof(PetWindow).GetField(field, Private).GetValue(pet); }
    private static object Call(PetWindow pet, string method, params object[] args) { return typeof(PetWindow).GetMethod(method, Private).Invoke(pet, args); }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static bool Near(double actual, double expected) { return Math.Abs(actual - expected) < 0.00001; }

    private static AbilityWindow Window()
    {
        return new AbilityWindow { Handle = new IntPtr(1234), ProcessId = 88, Bounds = new Rect(100, 100, 1000, 700),
            WorkArea = Work, Resizable = true };
    }

    private sealed class FixedRandom : Random
    {
        private readonly int action;
        private int count;
        internal FixedRandom(int action) { this.action = action; }
        public override int Next(int maximum) { return count++ % 2 == 0 ? 0 : Math.Min(action, maximum - 1); }
    }

    // No test calls a native window-mutating function. Every handle and rectangle
    // below belongs only to this deterministic in-memory desktop.
    private sealed class Desktop : IWindowAbilityDesktop
    {
        internal AbilityWindow Window = WindowAbilityTests.Window();
        internal bool Empty, Gone, FailRestore, FailSet, RestoreStuck, ThrowCandidates, ThrowSet, IgnoreSet;
        internal int DelayRefreshes;
        internal Rect PendingBounds = Rect.Empty;
        internal List<AbilityWindow> ExtraWindows;
        internal int CandidateCalls, RestoreCalls, SetCalls;
        internal readonly List<Rect> Positions = new List<Rect>();
        public List<AbilityWindow> Candidates()
        {
            CandidateCalls++;
            if (ThrowCandidates) throw new InvalidOperationException();
            if (Empty) return new List<AbilityWindow>();
            List<AbilityWindow> candidates = new List<AbilityWindow> { Window };
            if (ExtraWindows != null) candidates.AddRange(ExtraWindows);
            return candidates;
        }
        public AbilityWindow Refresh(AbilityWindow expected)
        {
            if (Gone || expected.ProcessId != Window.ProcessId) return null;
            if (!PendingBounds.IsEmpty && DelayRefreshes-- <= 0) { Window.Bounds = PendingBounds; PendingBounds = Rect.Empty; }
            return Window;
        }
        public bool RestoreWithoutActivation(AbilityWindow window)
        {
            RestoreCalls++;
            if (FailRestore) return false;
            if (!RestoreStuck) { Window.Maximized = false; Window.Bounds = new Rect(200, 200, 900, 600); }
            return true;
        }
        public bool SetBounds(AbilityWindow window, Rect bounds)
        {
            if (ThrowSet) throw new InvalidOperationException();
            SetCalls++;
            if (FailSet) return false;
            Positions.Add(bounds);
            if (!IgnoreSet)
            {
                if (DelayRefreshes > 0) PendingBounds = bounds;
                else Window.Bounds = bounds;
            }
            return true;
        }
    }

    private static WindowAbilityController Controller(Desktop desktop, int action)
    {
        return new WindowAbilityController(desktop, new WindowAbilityClock(Now.AddSeconds(-20), DateTime.MinValue),
            new FixedRandom(action), null);
    }

    private static void Advance(WindowAbilityController controller, int frames)
    {
        for (int i = 0; i < frames; i++) controller.Advance(0.05);
    }

    private static void CheckClock()
    {
        WindowAbilityClock clock = new WindowAbilityClock(Now, DateTime.MinValue);
        Require(!clock.IsReady(Now) && !clock.IsReady(Now.AddSeconds(19.999)), "Startup grace must last 20 seconds.");
        Require(clock.IsReady(Now.AddSeconds(20)), "First eligible ability can start after the grace period.");
        clock.Use(Now.AddSeconds(20));
        Require(!clock.IsReady(Now.AddSeconds(319.999)), "Five-minute cooldown cannot finish early.");
        Require(clock.IsReady(Now.AddSeconds(320)), "Five-minute cooldown must finish exactly on time.");
        WindowAbilityClock resumed = new WindowAbilityClock(Now.AddMinutes(1), clock.NextUseUtc);
        Require(resumed.NextUseUtc == clock.NextUseUtc, "Restart must preserve a pending cooldown.");
        Require(resumed.IsReady(Now.AddSeconds(320)), "Elapsed time while hidden or on another character counts.");
        WindowAbilityClock expired = new WindowAbilityClock(Now, Now.AddDays(-1));
        Require(expired.NextUseUtc == Now.AddSeconds(20), "Expired save still gets startup grace.");
        WindowAbilityClock corrupt = new WindowAbilityClock(Now, Now.AddYears(10));
        Require(corrupt.NextUseUtc == Now.AddMinutes(5), "Future/corrupted save cannot lock ability indefinitely.");
        clock.RetryLater(Now);
        Require(clock.NextUseUtc == Now.AddSeconds(15), "No available window retries without starting full cooldown.");

        clock.Use(Now);
        for (int minutes = 4; minutes >= 0; minutes--)
        {
            Require(clock.ReduceCooldown(Now) == TimeSpan.FromMinutes(minutes) && clock.NextUseUtc == Now.AddMinutes(minutes),
                "Each completed double click removes exactly one minute until zero.");
        }
        Require(clock.IsReady(Now), "Five double clicks make a new five-minute cooldown ready.");
        Require(clock.ReduceCooldown(Now.AddMinutes(2)) == TimeSpan.Zero && clock.NextUseUtc == Now.AddMinutes(2),
            "Ready cooldown stays at zero instead of accumulating negative time.");
        clock.Use(Now.AddMinutes(2));
        Require(clock.Remaining(Now.AddMinutes(2)) == TimeSpan.FromMinutes(5), "Ready clicks never discount the next spell.");
        Require(clock.ReduceCooldown(Now.AddMinutes(6).AddSeconds(30)) == TimeSpan.Zero,
            "Reducing the last thirty seconds clamps at ready.");
        clock.RetryLater(Now);
        Require(clock.ReduceCooldown(Now) == TimeSpan.Zero && clock.IsReady(Now), "Double click can clear a short retry delay.");
        clock.RetryLater(Now.AddSeconds(1));
        Require(clock.Remaining(Now.AddSeconds(1)) == TimeSpan.FromSeconds(15), "Failure still applies its ordinary fifteen-second retry.");
        WindowAbilityClock fresh = new WindowAbilityClock(Now, DateTime.MinValue);
        Require(fresh.ReduceCooldown(Now) == TimeSpan.Zero, "The same nonnegative rule applies to the initial twenty-second grace.");
        Require(new WindowAbilityClock(Now, fresh.NextUseUtc).NextUseUtc == Now.AddSeconds(20),
            "Restarting still preserves the minimum startup grace after a ready save.");

        Desktop desktop = new Desktop();
        DateTime saved = DateTime.MinValue;
        int writes = 0;
        WindowAbilityController controller = new WindowAbilityController(desktop,
            new WindowAbilityClock(Now.AddSeconds(-20), DateTime.MinValue), new FixedRandom(0),
            delegate(DateTime next) { saved = next; writes++; });
        Require(controller.TryStart(Now, Pet, Work), "Test spell starts a full cooldown.");
        controller.Cancel();
        Require(controller.ReduceCooldown(Now) == TimeSpan.FromMinutes(4) && saved == Now.AddMinutes(4) && writes == 2,
            "Cancelled spell keeps its cooldown and persists the one-minute reduction.");
        WindowAbilityClock reducedResume = new WindowAbilityClock(Now.AddSeconds(3), saved);
        Require(reducedResume.NextUseUtc == saved, "Restart preserves the reduced cooldown timestamp.");
        Require(desktop.CandidateCalls == 1 && desktop.SetCalls == 0 && desktop.RestoreCalls == 0 && !controller.Active,
            "Reducing the cooldown never searches for or mutates windows, and never starts a spell.");
    }

    private static void CheckSelection()
    {
        Func<uint, bool, bool, bool, bool, bool, int, int, string, int, Rect, bool> candidate =
            delegate(uint process, bool visible, bool iconic, bool cloaked, bool hung, bool owned, int style, int ex,
                string name, int title, Rect bounds) {
                return NativeWindowAbilityDesktop.IsCandidate(process, 42, visible, iconic, cloaked, hung, owned, style, ex, name, title, bounds);
            };
        int caption = 0x00C00000;
        Rect normal = new Rect(0, 0, 1000, 700);
        Require(candidate(88, true, false, false, false, false, caption, 0, "Application", 8, normal), "Ordinary visible application is eligible.");
        Require(!candidate(42, true, false, false, false, false, caption, 0, "Application", 8, normal), "Pet and its own windows are excluded.");
        Require(!candidate(0, true, false, false, false, false, caption, 0, "Application", 8, normal), "Missing process is excluded.");
        Require(!candidate(88, false, false, false, false, false, caption, 0, "Application", 8, normal), "Hidden window is excluded.");
        Require(!candidate(88, true, true, false, false, false, caption, 0, "Application", 8, normal), "Minimized window is excluded.");
        Require(!candidate(88, true, false, true, false, false, caption, 0, "Application", 8, normal), "Other-desktop/cloaked window is excluded.");
        Require(!candidate(88, true, false, false, true, false, caption, 0, "Application", 8, normal), "Hung window is excluded.");
        Require(!candidate(88, true, false, false, false, true, caption, 0, "Application", 8, normal), "Owned popup is excluded.");
        Require(!candidate(88, true, false, false, false, false, caption | 0x40000000, 0, "Application", 8, normal), "Child window is excluded.");
        Require(!candidate(88, true, false, false, false, false, 0, 0, "Application", 8, normal), "Captionless system surfaces are excluded.");
        Require(!candidate(88, true, false, false, false, false, caption, 0x80, "Application", 8, normal), "Tool window is excluded.");
        Require(!candidate(88, true, false, false, false, false, caption, 0x08000000, "Application", 8, normal), "No-activate utility surface is excluded.");
        Require(!candidate(88, true, false, false, false, false, caption, 0, "Application", 0, normal), "Untitled helper is excluded.");
        Require(!candidate(88, true, false, false, false, false, caption, 0, "Application", 8, new Rect(0, 0, 20, 20)), "Tiny helper is excluded.");
        foreach (string shell in new[] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "#32770" })
            Require(!candidate(88, true, false, false, false, false, caption, 0, shell, 8, normal), shell + " is excluded.");

        Require(!WindowAbilityVisibility.IsExposed(normal, Work, new List<Rect> { normal }), "Completely covered windows cannot be selected.");
        Require(!WindowAbilityVisibility.IsExposed(normal, Work, new List<Rect> { new Rect(0, 0, 500, 700), new Rect(500, 0, 500, 700) }),
            "Multiple overlapping windows jointly occlude a target.");
        Require(Near(WindowAbilityVisibility.VisibleArea(normal, Work, new List<Rect> { new Rect(0, 0, 500, 700) }), 350000),
            "Occlusion uses exposed area without double-counting.");
        Require(!WindowAbilityVisibility.IsExposed(new Rect(2000, 100, 500, 400), Work, new List<Rect>()), "Offscreen windows have no exposed area.");
        Require(WindowAbilityVisibility.IsExposed(normal, Work, new List<Rect> { new Rect(0, 0, 500, 700) }), "Meaningfully exposed windows remain eligible.");

        Desktop desktop = new Desktop();
        desktop.Window.Bounds = new Rect(-1800, 100, 1000, 700);
        desktop.Window.WorkArea = new Rect(-1920, 0, 1920, 1040);
        AbilityWindow local = Window();
        desktop.ExtraWindows = new List<AbilityWindow> { local };
        WindowAbilityController controller = Controller(desktop, 0);
        Require(controller.TryStart(Now, Pet, Work) && controller.StartingBounds == local.Bounds, "Visible targets on the pet monitor are preferred.");
        desktop.ExtraWindows = null; controller = Controller(desktop, 0);
        Require(controller.TryStart(Now, Pet, Work) && controller.Action == WindowAbilityAction.Pull && Work.Contains(controller.TargetBounds),
            "Cross-monitor fallback pulls the window to the pet so its effect is visible.");
    }

    private static void CheckPlacement()
    {
        AbilityWindow window = Window();
        Rect shrink = WindowAbilityPlacement.Target(window, WindowAbilityAction.Shrink, Pet, Work);
        Require(Near(shrink.Width, 650) && Near(shrink.Height, 455), "Shrink uses 65 percent dimensions.");
        Require(Near(shrink.X + shrink.Width / 2, 600) && Near(shrink.Y + shrink.Height / 2, 450), "Shrink preserves center.");
        Rect pull = WindowAbilityPlacement.Target(window, WindowAbilityAction.Pull, Pet, Work);
        Require(Near(pull.Right, Pet.Left - 22), "Pull places window beside pet when there is enough room.");
        Require(Near(pull.Width, 1000) && Near(pull.Height, 700), "Pull preserves normal window size.");
        foreach (Rect work in new[] { Work, new Rect(-1920, -200, 1920, 1040), new Rect(1920, -1440, 2560, 1400), new Rect(-700, 0, 700, 500) })
        {
            foreach (Point anchor in new[] { work.TopLeft, work.TopRight, work.BottomLeft, work.BottomRight, new Point(work.X + work.Width / 2, work.Y + work.Height / 2) })
            {
                Rect pet = new Rect(anchor.X - 82, anchor.Y - 200, 165, 225);
                foreach (WindowAbilityAction action in new[] { WindowAbilityAction.Shrink, WindowAbilityAction.Pull })
                {
                    window.WorkArea = work;
                    window.Bounds = new Rect(work.X - 300, work.Y - 200, 2200, 1500);
                    Rect target = WindowAbilityPlacement.Target(window, action, pet, work);
                    Require(work.Contains(target), "Complete result must stay within its work area, including negative monitors.");
                    Require(target.Width > 0 && target.Height > 0, "Result size stays positive.");
                }
            }
        }
        window.Bounds = new Rect(0, 0, 300, 200);
        Require(!WindowAbilityPlacement.CanShrink(window), "Small windows are pulled instead of becoming unreadable.");
        window.Bounds = new Rect(0, 0, 1000, 700); window.Resizable = false;
        Require(!WindowAbilityPlacement.CanShrink(window), "Fixed-size windows cannot be selected for shrink.");
        Rect a = new Rect(-100, 20, 1000, 700), b = new Rect(200, 200, 650, 455);
        Require(WindowAbilityPlacement.Between(a, b, 0) == a && WindowAbilityPlacement.Between(a, b, 1) == b, "Animation reaches exact endpoints.");
        double lastX = a.X, lastWidth = a.Width;
        for (int i = 1; i <= 100; i++)
        {
            Rect position = WindowAbilityPlacement.Between(a, b, i / 100.0);
            Require(position.X >= lastX && position.Width <= lastWidth, "Animated position and size must progress smoothly without reversal.");
            lastX = position.X; lastWidth = position.Width;
        }
    }

    private static void CheckSequence()
    {
        foreach (int action in new[] { 0, 1 })
        {
            Desktop desktop = new Desktop();
            WindowAbilityController controller = Controller(desktop, action);
            Require(controller.TryStart(Now, Pet, Work), "Ready controller starts.");
            Require(controller.Action == (action == 0 ? WindowAbilityAction.Shrink : WindowAbilityAction.Pull), "Random branch chooses requested action.");
            Require(controller.Active && controller.BodyFrame == 0, "Ability starts from preparation pose.");
            Advance(controller, 6);
            Require(controller.BodyFrame == 1, "Hand raises through intermediate pose.");
            Advance(controller, 8);
            Require(controller.BodyFrame == 2, "Hand reaches its raised pose before moving a window.");
            Require(desktop.SetCalls == 0 && desktop.RestoreCalls == 0, "Windup cannot manipulate the window early.");
            Advance(controller, 7);
            Require(controller.Phase == WindowAbilityPhase.Animate, "After windup the gesture animates the window.");
            Advance(controller, 24);
            Require(controller.Phase == WindowAbilityPhase.LowerHand && controller.BodyFrame == 3 && controller.Succeeded,
                "Successful action lowers the hand through its release pose.");
            Require(desktop.Positions.Count >= 10, "Window action uses multiple smooth animation steps.");
            Advance(controller, 16);
            Require(!controller.Active && controller.BodyFrame == -1, "Gesture returns to ordinary animation.");
            Require(!controller.TryStart(Now.AddMinutes(4), Pet, Work), "Completed action remains on cooldown.");
            Require(controller.TryStart(Now.AddMinutes(5), Pet, Work), "Action may recur after five minutes.");
            controller.Cancel();
            Require(!controller.Active && controller.Clock.NextUseUtc == Now.AddMinutes(10), "Switch/drag cancellation preserves consumed cooldown.");
        }
        DateTime saved = DateTime.MinValue;
        WindowAbilityController persisted = new WindowAbilityController(new Desktop(),
            new WindowAbilityClock(Now.AddSeconds(-20), DateTime.MinValue), new FixedRandom(0), delegate(DateTime next) { saved = next; });
        persisted.TryStart(Now, Pet, Work);
        Require(saved == Now.AddMinutes(5), "Starting the gesture persists cooldown once.");
    }

    private static void CheckFailureCases()
    {
        Desktop desktop = new Desktop { Empty = true };
        WindowAbilityController controller = Controller(desktop, 0);
        Require(!controller.TryStart(Now, Pet, Work) && controller.Clock.NextUseUtc == Now.AddSeconds(15), "No targets retries later without raising hand.");
        desktop = new Desktop { ThrowCandidates = true }; controller = Controller(desktop, 0);
        Require(!controller.TryStart(Now, Pet, Work), "Enumeration failure does not escape into render loop.");

        desktop = new Desktop(); desktop.Window.Maximized = true;
        controller = Controller(desktop, 0); controller.TryStart(Now, Pet, Work); Advance(controller, 50);
        Require(desktop.RestoreCalls == 1 && desktop.SetCalls > 0 && controller.Succeeded, "Maximized target restores before geometry animation.");
        Require(Near(desktop.Window.Bounds.Width, 900 * 0.65), "Resize starts from restored bounds, not maximized bounds.");

        desktop = new Desktop { RestoreStuck = true }; desktop.Window.Maximized = true;
        controller = Controller(desktop, 0); controller.TryStart(Now, Pet, Work); Advance(controller, 80);
        Require(!controller.Active && !controller.Succeeded && desktop.SetCalls == 0 && desktop.RestoreCalls == 1,
            "Unresponsive asynchronous restore times out without repeated restore requests.");

        desktop = new Desktop { FailRestore = true }; desktop.Window.Maximized = true;
        controller = Controller(desktop, 0); controller.TryStart(Now, Pet, Work); Advance(controller, 50);
        Require(!controller.Active && !controller.Succeeded && desktop.SetCalls == 0, "Denied restore fails gracefully.");

        foreach (string failure in new[] { "gone", "reused", "set", "throw" })
        {
            desktop = new Desktop(); controller = Controller(desktop, 0); controller.TryStart(Now, Pet, Work);
            if (failure == "gone") desktop.Gone = true;
            if (failure == "reused") desktop.Window = new AbilityWindow { Handle = new IntPtr(1234), ProcessId = 999 };
            if (failure == "set") desktop.FailSet = true;
            if (failure == "throw") desktop.ThrowSet = true;
            Advance(controller, 60);
            Require(!controller.Active && !controller.Succeeded, "Invalid/protected window fails gracefully: " + failure);
            Require(desktop.Positions.Count == 0, "Failed action leaves every available window untouched: " + failure);
        }
        desktop = new Desktop(); desktop.Window.Resizable = false;
        controller = Controller(desktop, 0); controller.TryStart(Now, Pet, Work);
        Require(controller.Action == WindowAbilityAction.Pull, "Nonresizable target falls back to pulling.");
        Advance(controller, 50);
        Require(controller.Succeeded && Near(desktop.Window.Bounds.Width, 1000), "Pull can move a fixed-size ordinary window.");

        desktop = new Desktop(); controller = Controller(desktop, 0); controller.TryStart(Now, Pet, Work); Advance(controller, 8);
        controller.Cancel(); Advance(controller, 80);
        Require(desktop.SetCalls == 0 && desktop.RestoreCalls == 0, "Interrupting the hand raise never performs a delayed action.");

        desktop = new Desktop { IgnoreSet = true }; controller = Controller(desktop, 0);
        controller.TryStart(Now, Pet, Work); Advance(controller, 90);
        Require(!controller.Succeeded && !controller.Active && desktop.SetCalls > 0,
            "An accepted asynchronous SetWindowPos with no actual bounds change is not reported as success.");
        Require(controller.Clock.NextUseUtc < Now.AddMinutes(1), "A failed spell retries soon instead of consuming five minutes.");

        desktop = new Desktop { DelayRefreshes = 25 }; controller = Controller(desktop, 0);
        controller.TryStart(Now, Pet, Work); Advance(controller, 41);
        Require(controller.Phase == WindowAbilityPhase.Verify && !controller.Succeeded, "Controller waits for queued native requests to change actual bounds.");
        Advance(controller, 35);
        Require(controller.Succeeded, "An asynchronously applied request succeeds only after actual bounds settle.");

        desktop = new Desktop(); desktop.Window.Bounds = WindowAbilityPlacement.Target(desktop.Window, WindowAbilityAction.Pull, Pet, Work);
        controller = Controller(desktop, 1);
        Require(controller.TryStart(Now, Pet, Work) && controller.Action == WindowAbilityAction.Shrink,
            "A window already beside the pet shrinks instead of performing an invisible zero-distance pull.");
    }

    private static RenderTargetBitmap DrawEffect(WindowAbilityController controller, Rect work)
    {
        AbilityEffectVisual visual = new AbilityEffectVisual();
        visual.Update(controller, work);
        visual.Measure(work.Size); visual.Arrange(new Rect(work.Size)); visual.UpdateLayout();
        RenderTargetBitmap bitmap = new RenderTargetBitmap((int)work.Width, (int)work.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static void CheckEffects()
    {
        Desktop desktop = new Desktop();
        WindowAbilityController controller = Controller(desktop, 0);
        controller.TryStart(Now, Pet, Work); Advance(controller, 16);
        RenderTargetBitmap bitmap = DrawEffect(controller, Work);
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        int visible = 0;
        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] >= 20) visible++;
        Require(visible > 12000, "Actual WPF render contains clearly visible spell and window outline pixels.");
        Require(visible < bitmap.PixelWidth * bitmap.PixelHeight / 5, "Spell overlay leaves the desktop content mostly transparent.");
        controller.Cancel(); bitmap = DrawEffect(controller, Work);
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        bool clean = true;
        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 0) { clean = false; break; }
        Require(clean, "Cancelled spell has no lingering rendered elements.");

        AbilityEffectWindow overlay = new AbilityEffectWindow { Width = 400, Height = 300, Opacity = 0 };
        try
        {
            IntPtr handle = new WindowInteropHelper(overlay).EnsureHandle();
            int styles = NativeWindow.GetWindowLong(handle, -20);
            Require((styles & 0x080000A0) == 0x080000A0 && !overlay.ShowActivated && !overlay.Focusable && !overlay.IsHitTestVisible,
                "Native overlay is a no-activate, click-through tool window.");
        }
        finally { overlay.Close(); }
    }

    private static void WritePreview()
    {
        string directory = Path.Combine("artifacts", "tests", "ability-v23-frames");
        Directory.CreateDirectory(directory);
        Rect work = new Rect(0, 0, 960, 640), pet = new Rect(750, 405, 165, 225);
        Dictionary<string, BitmapSource[]> frames = PetAssets.Load("kolo");
        int output = 0;
        for (int action = 0; action < 2; action++)
        {
            Desktop desktop = new Desktop();
            desktop.Window.WorkArea = work;
            desktop.Window.Bounds = new Rect(70, 90, 560, 380);
            WindowAbilityController controller = Controller(desktop, action);
            controller.TryStart(Now, pet, work);
            for (int i = 0; i < 82; i++, output++)
            {
                controller.Advance(0.04);
                DrawingVisual background = new DrawingVisual();
                using (DrawingContext drawing = background.RenderOpen())
                {
                    drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(20, 26, 38)), null, work);
                    FormattedText heading = new FormattedText(action == 0 ? "珂洛 · 缩小术" : "珂洛 · 牵引术", CultureInfo.GetCultureInfo("zh-CN"),
                        FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 23, Brushes.Wheat, 1.0);
                    drawing.DrawText(heading, new Point(30, 24));
                    Rect bounds = desktop.Window.Bounds;
                    drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(42, 52, 68)), new Pen(new SolidColorBrush(Color.FromRgb(88, 102, 120)), 1), bounds, 9, 9);
                    drawing.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(77, 91, 109)), 1), new Point(bounds.Left, bounds.Top + 43), new Point(bounds.Right, bounds.Top + 43));
                    FormattedText caption = new FormattedText("示例窗口 · 模拟数据", CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
                        new Typeface("Microsoft YaHei UI"), 14, Brushes.WhiteSmoke, 1.0);
                    drawing.DrawText(caption, new Point(bounds.Left + 18, bounds.Top + 13));
                    for (int line = 0; line < 5; line++)
                        drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(65, 78, 98)), null,
                            new Rect(bounds.Left + 23, bounds.Top + 72 + line * 25, Math.Max(40, bounds.Width - 70 - line % 2 * 65), 7), 3, 3);
                    int pose = controller.BodyFrame;
                    drawing.DrawImage(pose >= 0 ? frames["ability"][pose] : frames["idle"][0], new Rect(pet.Left, pet.Bottom - pet.Width, pet.Width, pet.Width));
                    FormattedText footnote = new FormattedText("实际 WPF 特效渲染  ·  仅操作测试数据", CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
                        new Typeface("Microsoft YaHei UI"), 13, Brushes.SlateGray, 1.0);
                    drawing.DrawText(footnote, new Point(30, 607));
                }
                RenderTargetBitmap bitmap = new RenderTargetBitmap(960, 640, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(background);
                AbilityEffectVisual effect = new AbilityEffectVisual(); effect.Update(controller, work);
                effect.Measure(work.Size); effect.Arrange(new Rect(work.Size)); effect.UpdateLayout(); bitmap.Render(effect);
                PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream file = File.Create(Path.Combine(directory, output.ToString("D3") + ".png"))) encoder.Save(file);
                if (i == 38)
                {
                    File.Copy(Path.Combine(directory, output.ToString("D3") + ".png"),
                        Path.Combine("artifacts", "tests", "ability-v23-" + (action == 0 ? "shrink" : "pull") + ".png"), true);
                }
            }
        }
        Console.WriteLine("Ability preview: " + output + " frames at 40ms; mocked windows only.");
    }

    private static WindowAbilityController StartMockGesture(PetWindow pet, Desktop desktop)
    {
        WindowAbilityController controller = Controller(desktop, 0);
        controller.TryStart(Now, Pet, Work);
        Set(pet, "windowAbility", controller);
        Set(pet, "currentCharacter", "kolo");
        Set(pet, "state", "idle"); Set(pet, "stateEnds", DateTime.MaxValue);
        Set(pet, "movement", "rest"); Set(pet, "quietMode", false); Set(pet, "pointerHeld", false);
        return controller;
    }

    private static int RunNativeHost(string rendezvous)
    {
        // A separate process is essential: the production adapter correctly
        // excludes the pet's own process and all its windows.
        Application app = new Application();
        Rect work = SystemParameters.WorkArea;
        Window host = new Window {
            Title = "原神启动 · 窗口能力自动测试", ShowActivated = false, ShowInTaskbar = true,
            WindowStyle = WindowStyle.SingleBorderWindow, ResizeMode = ResizeMode.CanResize,
            Left = work.Left + 30, Top = work.Top + 60, Width = Math.Min(800, work.Width - 60),
            Height = Math.Min(520, work.Height - 100), Opacity = 0
        };
        host.Loaded += delegate {
            IntPtr handle = new WindowInteropHelper(host).Handle;
            Matrix transform = PresentationSource.FromVisual(host).CompositionTarget.TransformFromDevice;
            File.WriteAllText(rendezvous + ".pending", handle.ToInt64().ToString(CultureInfo.InvariantCulture) + "\n" +
                transform.M11.ToString("R", CultureInfo.InvariantCulture) + "\n" + transform.M22.ToString("R", CultureInfo.InvariantCulture));
            File.Move(rendezvous + ".pending", rendezvous);
        };
        DispatcherTimer timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timeout.Tick += delegate { timeout.Stop(); host.Close(); };
        timeout.Start();
        app.Run(host);
        return 0;
    }

    private static AbilityWindow WaitForNativeBounds(NativeWindowAbilityDesktop desktop, AbilityWindow target, Rect bounds)
    {
        Stopwatch timer = Stopwatch.StartNew();
        AbilityWindow actual = null;
        while (timer.Elapsed.TotalSeconds < 2)
        {
            actual = desktop.Refresh(target);
            if (actual == null || WindowAbilityPlacement.Difference(actual.Bounds, bounds) < 3) break;
            Thread.Sleep(10);
        }
        return actual;
    }

    private static void CheckNativeOwnedWindow()
    {
        string rendezvous = Path.Combine(Path.GetTempPath(), "kolo-ability-native-" + Guid.NewGuid().ToString("N") + ".txt");
        Process host = null;
        AbilityEffectWindow overlay = null;
        try
        {
            IntPtr foreground = NativeWindow.GetForegroundWindow();
            host = Process.Start(new ProcessStartInfo {
                FileName = Assembly.GetExecutingAssembly().Location,
                Arguments = "--native-host \"" + rendezvous + "\"",
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            });
            Stopwatch timer = Stopwatch.StartNew();
            while (!File.Exists(rendezvous) && !host.HasExited && timer.Elapsed.TotalSeconds < 4) Thread.Sleep(10);
            Require(File.Exists(rendezvous), "Owned native test process creates its helper window.");
            string[] info = File.ReadAllLines(rendezvous);
            IntPtr handle = new IntPtr(long.Parse(info[0], CultureInfo.InvariantCulture));
            uint process;
            NativeWindow.GetWindowThreadProcessId(handle, out process);
            Require(process == (uint)host.Id, "Native smoke refuses any HWND outside its own helper process.");
            NativeWindowAbilityDesktop desktop = new NativeWindowAbilityDesktop(new Matrix(
                double.Parse(info[1], CultureInfo.InvariantCulture), 0, 0,
                double.Parse(info[2], CultureInfo.InvariantCulture), 0, 0));
            // Intentionally never call Candidates(): only the verified test HWND
            // is ever passed to any window-mutating production method.
            AbilityWindow target = desktop.Refresh(new AbilityWindow { Handle = handle, ProcessId = process });
            Require(target != null, "Production native adapter accepts an ordinary helper window.");
            Require(NativeWindow.GetForegroundWindow() == foreground, "Starting helper does not activate it.");

            overlay = new AbilityEffectWindow { Width = 400, Height = 300, Opacity = 0, Left = target.WorkArea.Left, Top = target.WorkArea.Top };
            overlay.Show();
            Require(NativeWindow.GetForegroundWindow() == foreground, "Showing actual native spell overlay does not steal focus.");
            Rect pet = new Rect(target.WorkArea.Right - 190, target.WorkArea.Bottom - 225, 165, 225);
            Rect original = target.Bounds;
            Rect shrink = WindowAbilityPlacement.Target(target, WindowAbilityAction.Shrink, pet, target.WorkArea);
            Require(desktop.SetBounds(target, shrink), "Production native shrink request is accepted for the helper only.");
            AbilityWindow shrunk = WaitForNativeBounds(desktop, target, shrink);
            Require(shrunk != null && WindowAbilityPlacement.Difference(shrunk.Bounds, shrink) < 3 &&
                WindowAbilityPlacement.IsMeaningful(original, shrunk.Bounds, WindowAbilityAction.Shrink),
                "Native GetWindowRect confirms the helper really shrank to requested dimensions.");
            Require(NativeWindow.GetForegroundWindow() == foreground, "Native shrinking preserves foreground focus.");

            Rect pull = WindowAbilityPlacement.Target(shrunk, WindowAbilityAction.Pull, pet, shrunk.WorkArea);
            Require(desktop.SetBounds(shrunk, pull), "Production native pull request is accepted for the helper only.");
            AbilityWindow pulled = WaitForNativeBounds(desktop, shrunk, pull);
            Require(pulled != null && WindowAbilityPlacement.Difference(pulled.Bounds, pull) < 3 &&
                WindowAbilityPlacement.IsMeaningful(shrunk.Bounds, pulled.Bounds, WindowAbilityAction.Pull),
                "Native GetWindowRect confirms the helper really moved beside the pet.");
            Require(NativeWindow.GetForegroundWindow() == foreground, "Native pulling preserves foreground focus.");
            Console.WriteLine("Native ability smoke: own helper HWND only; actual shrink/pull bounds and unchanged foreground verified.");
        }
        finally
        {
            if (overlay != null) overlay.Close();
            if (host != null)
            {
                if (!host.HasExited) { host.CloseMainWindow(); if (!host.WaitForExit(2000)) { host.Kill(); host.WaitForExit(); } }
                host.Dispose();
            }
            if (File.Exists(rendezvous)) File.Delete(rendezvous);
            if (File.Exists(rendezvous + ".pending")) File.Delete(rendezvous + ".pending");
        }
    }

    private static void CheckMainWindowHooks()
    {
        // The pet itself is a transparent, nonactivating test host. Its window
        // adapter is injected before Show(), so native user-window mutation is
        // impossible even if a test pauses for longer than the startup grace.
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string characterPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet", "character.txt");
        byte[] savedCharacter = File.Exists(characterPath) ? File.ReadAllBytes(characterPath) : null;
        string cooldownPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet", "window-ability-cooldown.txt");
        byte[] savedCooldown = File.Exists(cooldownPath) ? File.ReadAllBytes(cooldownPath) : null;
        PetWindow pet = null;
        try
        {
            pet = new PetWindow { ShowActivated = false, Opacity = 0, Topmost = false };
            Get<DispatcherTimer>(pet, "timer").Stop();
            Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
            Set(pet, "bubbleEnds", DateTime.MinValue);
            Get<SpeechWindow>(pet, "speechWindow").Opacity = 0;
            Desktop desktop = new Desktop();
            WindowAbilityController controller = StartMockGesture(pet, desktop);
            Dictionary<string, BitmapSource[]> frames = new Dictionary<string, BitmapSource[]>(
                (Dictionary<string, BitmapSource[]>)Call(pet, "LoadCharacterFrames", "kolo"));
            BitmapSource[] gesture = new BitmapSource[4];
            for (int i = 0; i < gesture.Length; i++)
            {
                gesture[i] = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
                    new byte[] { (byte)(20 + i), 100, 200, 255, 20, 100, 200, 255, 20, 100, 200, 255, 20, 100, 200, 255 }, 8);
                gesture[i].Freeze();
            }
            frames["ability"] = gesture;
            Set(pet, "frames", frames);
            pet.Show();
            pet.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
            Get<DispatcherTimer>(pet, "timer").Stop();
            Require(pet.IsLoaded && pet.IsVisible, "WPF hook test host is loaded without activation.");
            CheckDoubleClickCooldown(pet, cooldownPath);
            controller = StartMockGesture(pet, desktop);
            Set(pet, "autoWalk", true); Set(pet, "nextWalk", DateTime.MinValue);
            // Force a location without support: unpaused movement would fall.
            Set(pet, "platformCache", new List<PlatformEdge>());
            Set(pet, "platformCacheTime", DateTime.Now);
            pet.Left = 600; pet.Top = 200;
            Point heldPosition = new Point(pet.Left, pet.Top);
            Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-50));
            Call(pet, "OnTick", null, EventArgs.Empty);
            Require(Get<string>(pet, "movement") == "rest" && new Point(pet.Left, pet.Top) == heldPosition,
                "Real main tick pauses automatic movement and gravity during gesture.");
            Require(ReferenceEquals(Get<Image>(pet, "sprite").Source, gesture[0]), "Main renderer selects gesture preparation frame.");
            Advance(controller, 5);
            Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-50)); Call(pet, "OnTick", null, EventArgs.Empty);
            Require(ReferenceEquals(Get<Image>(pet, "sprite").Source, gesture[1]), "Main renderer selects intermediate hand raise frame.");
            Advance(controller, 7);
            Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-50)); Call(pet, "OnTick", null, EventArgs.Empty);
            Require(ReferenceEquals(Get<Image>(pet, "sprite").Source, gesture[2]), "Main renderer selects raised hand frame.");
            Require(desktop.SetCalls == 0, "Main renderer never applies native movement before hand raise completes.");

            AbilityEffectWindow effectWindow = new AbilityEffectWindow { Width = 400, Height = 300, Opacity = 0, Owner = pet };
            HwndSource effectSource = HwndSource.FromHwnd(new WindowInteropHelper(effectWindow).EnsureHandle());
            Set(pet, "abilityEffectWindow", effectWindow);
            Set(pet, "quietMode", true); Call(pet, "UpdateWindowAbility", DateTime.Now, 0.05);
            Require(!controller.Active, "Quiet mode cancels pending ability.");
            Require(Get<AbilityEffectWindow>(pet, "abilityEffectWindow") == null && effectSource.IsDisposed,
                "Cancelling the gesture closes and releases its native effect overlay immediately.");
            controller = StartMockGesture(pet, desktop); Set(pet, "pointerHeld", true);
            Call(pet, "UpdateWindowAbility", DateTime.Now, 0.05);
            Require(!controller.Active, "Dragging cancels pending ability.");
            controller = StartMockGesture(pet, desktop); Set(pet, "movement", "fall");
            Call(pet, "UpdateWindowAbility", DateTime.Now, 0.05);
            Require(!controller.Active, "Unsupported airborne state cancels ability.");
            controller = StartMockGesture(pet, desktop); Call(pet, "Play", "happy", "test", 500, 150);
            Require(!controller.Active, "Ordinary interaction cancels gesture immediately.");
            controller = StartMockGesture(pet, desktop); Call(pet, "SwitchCharacter", "ganyu");
            Require(!controller.Active, "Character switch cancels gesture immediately.");
            controller = StartMockGesture(pet, desktop); pet.Hide();
            Require(!controller.Active, "Hiding pet cancels pending window action.");
            controller = StartMockGesture(pet, desktop); pet.Close(); pet = null;
            Require(!controller.Active, "Closing pet cancels pending window action.");
            Require(desktop.SetCalls == 0 && desktop.RestoreCalls == 0, "Cancellation checks never manipulate any window.");
        }
        finally
        {
            if (pet != null) pet.Close();
            if (savedCharacter == null) { if (File.Exists(characterPath)) File.Delete(characterPath); }
            else File.WriteAllBytes(characterPath, savedCharacter);
            if (savedCooldown == null) { if (File.Exists(cooldownPath)) File.Delete(cooldownPath); }
            else File.WriteAllBytes(cooldownPath, savedCooldown);
            app.Shutdown();
        }
    }

    private static void CheckDoubleClickCooldown(PetWindow pet, string cooldownPath)
    {
        DateTime now = DateTime.UtcNow;
        Desktop desktop = new Desktop();
        DateTime saved = DateTime.MinValue;
        WindowAbilityController controller = new WindowAbilityController(desktop,
            new WindowAbilityClock(now.AddSeconds(-20), DateTime.MinValue), new FixedRandom(0), delegate(DateTime next) { saved = next; });
        Require(controller.TryStart(now, Pet, Work), "Double-click test starts only its mock gesture.");
        Set(pet, "windowAbility", controller); Set(pet, "currentCharacter", "kolo");
        Require(pet.ReduceWindowAbilityCooldownFromDoubleClick(), "Kolo handles cooldown-reducing double clicks.");
        Require(!controller.Active && controller.Clock.NextUseUtc == now.AddMinutes(4) && saved == controller.Clock.NextUseUtc,
            "Double click cancels an active gesture and reduces, rather than resets, the five-minute cooldown.");
        Require(Get<SpeechWindow>(pet, "speechWindow").Text.Text.Contains("剩余 4 分"), "Double click immediately reports the remaining cooldown.");
        for (int i = 0; i < 4; i++) pet.ReduceWindowAbilityCooldownFromDoubleClick();
        Require(controller.Clock.IsReady(DateTime.UtcNow) && Get<SpeechWindow>(pet, "speechWindow").Text.Text.Contains("已就绪"),
            "The fifth double click reports readiness.");
        for (int i = 0; i < 8; i++) pet.ReduceWindowAbilityCooldownFromDoubleClick();
        Require(desktop.CandidateCalls == 1 && desktop.SetCalls == 0 && desktop.RestoreCalls == 0 && !controller.Active,
            "Even repeated ready clicks do not select or operate a window from the gesture handler.");
        now = DateTime.UtcNow; controller.Clock.Use(now);
        Require(controller.Clock.Remaining(now) == TimeSpan.FromMinutes(5), "Spam clicking ready does not prepay the next cooldown.");

        foreach (string character in new[] { "hutao", "ganyu", "varesa" })
        {
            Set(pet, "currentCharacter", character);
            DateTime before = controller.Clock.NextUseUtc;
            Require(!pet.ReduceWindowAbilityCooldownFromDoubleClick() && controller.Clock.NextUseUtc == before,
                character + " cannot reduce Kolo's cooldown.");
        }
        Set(pet, "windowAbility", null);
        Require(!pet.ReduceWindowAbilityCooldownFromDoubleClick() && Get<WindowAbilityController>(pet, "windowAbility") == null,
            "Other characters do not initialize the window ability during a double click.");

        // The production persistence path is exercised without starting its
        // native adapter: the cooldown stays four minutes in the future.
        now = DateTime.UtcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(cooldownPath));
        File.WriteAllText(cooldownPath, now.AddMinutes(5).ToString("o", CultureInfo.InvariantCulture));
        Set(pet, "currentCharacter", "kolo");
        Require(pet.ReduceWindowAbilityCooldownFromDoubleClick(), "A first double click lazily loads the saved cooldown.");
        controller = Get<WindowAbilityController>(pet, "windowAbility");
        DateTime persisted = DateTime.ParseExact(File.ReadAllText(cooldownPath).Trim(), "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Require(persisted == now.AddMinutes(4) && controller.Clock.NextUseUtc == persisted,
            "The actual cooldown file contains the reduced UTC timestamp.");
        Require(ReferenceEquals(controller, Call(pet, "EnsureWindowAbilityController", now)),
            "Double-click and normal tick share one lazily initialized controller.");
        Set(pet, "quietMode", true);
        Call(pet, "UpdateWindowAbility", DateTime.Now, 0.016);
        Require(ReferenceEquals(controller, Get<WindowAbilityController>(pet, "windowAbility")),
            "A subsequent normal update keeps the double-click controller and clock.");
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--native-host") return RunNativeHost(args[1]);
            CheckClock(); CheckSelection(); CheckPlacement(); CheckSequence(); CheckFailureCases();
            CheckEffects();
            if (args.Length > 0 && args[0] == "--preview") WritePreview();
            CheckNativeOwnedWindow();
            CheckMainWindowHooks();
            Console.WriteLine("Window ability: " + checks + " checks passed (mock desktop + owned native helper; no user windows changed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
