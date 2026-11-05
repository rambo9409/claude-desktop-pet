using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    // Count visible use, not idle time or random polls. Readiness is latched
    // until a safe opportunity, so moving or interrupting does not reroll it.
    internal sealed class KoloEasterEggSession
    {
        internal const double RequiredUseSeconds = 180;
        internal const double DurationSeconds = 4.8;
        private DateTime lastObservedUtc;
        private bool previouslyInUse;
        internal double UsedSeconds { get; private set; }
        internal bool Completed { get; private set; }
        internal bool ManualRequested { get; private set; }
        internal bool Ready { get { return (ManualRequested || UsedSeconds >= RequiredUseSeconds) && !Completed; } }
        internal bool Active { get; private set; }
        internal double Elapsed { get; private set; }

        internal KoloEasterEggSession(DateTime nowUtc)
        {
            Reset(nowUtc);
        }

        internal void Reset(DateTime nowUtc)
        {
            lastObservedUtc = nowUtc; previouslyInUse = false;
            UsedSeconds = 0; Completed = false; Active = false; Elapsed = 0; ManualRequested = false;
        }

        internal void RequestManual() { ManualRequested = true; Completed = false; }

        internal bool Observe(DateTime nowUtc, bool inUse, bool eligible)
        {
            double elapsed = (nowUtc - lastObservedUtc).TotalSeconds;
            // Ignore suspended/blocked gaps and clock corrections. Normal UI
            // cadence and short stalls count actual elapsed time, not frames.
            if (inUse && previouslyInUse && elapsed > 0 && elapsed <= 5 && !Completed) {
                UsedSeconds = Math.Min(RequiredUseSeconds, UsedSeconds + elapsed);
                if (UsedSeconds >= RequiredUseSeconds - .000001) UsedSeconds = RequiredUseSeconds;
            }
            lastObservedUtc = nowUtc;
            previouslyInUse = inUse;
            if (!inUse || !eligible) { if (Active) Cancel(nowUtc); return false; }
            if (Active || !Ready) return false;
            Active = true;
            Elapsed = 0;
            return true;
        }

        internal bool Advance(double seconds)
        {
            if (!Active) return false;
            if (Double.IsNaN(seconds) || seconds <= 0) return false;
            // Preserve visible costume stages even if the desktop resumes
            // after suspension. There is no instant delayed switch on wake.
            Elapsed = Math.Min(DurationSeconds, Elapsed + Math.Min(.1, seconds));
            if (Elapsed < DurationSeconds - .000001) return false;
            Active = false;
            Completed = true;
            ManualRequested = false;
            return true;
        }

        internal int FrameIndex(int count)
        {
            if (count <= 0) return 0;
            return Math.Max(0, Math.Min(count - 1, (int)(Elapsed / DurationSeconds * count)));
        }

        internal void Cancel(DateTime nowUtc)
        {
            Active = false;
            Elapsed = 0;
            ManualRequested = false;
            lastObservedUtc = nowUtc;
            previouslyInUse = false;
        }
    }

    internal static class KoloEasterEggSettings
    {
        internal static string DefaultPath { get { return Path.Combine(PetStorage.RootDirectory, "kolo-easter-egg-enabled.txt"); } }
        internal static bool Load(string path)
        {
            string text = PetStorage.TryReadAllText(path, 64, "load-kolo-easter-egg");
            if (text == null) return true;
            bool enabled;
            if (Boolean.TryParse(text.Trim(), out enabled)) return enabled;
            PetStorage.Log("load-kolo-easter-egg", new InvalidDataException("Unrecognized idle costume preference."));
            return false;
        }
        internal static bool Save(string path, bool enabled)
        { return PetStorage.TryWriteAllText(path, enabled ? "true" : "false", "save-kolo-easter-egg"); }
    }

    public sealed partial class PetWindow
    {
        private readonly KoloEasterEggSession koloEasterEgg = new KoloEasterEggSession(DateTime.UtcNow);
        private bool koloEasterEggEnabled = KoloEasterEggSettings.Load(KoloEasterEggSettings.DefaultPath);
        private bool IsKoloEasterEggActive { get { return koloEasterEgg.Active; } }
        private bool KoloEasterEggEnabled { get { return koloEasterEggEnabled; } }

        private void InitializeKoloEasterEgg()
        {
            StateChanged += delegate { if (WindowState == WindowState.Minimized) { CancelKoloEasterEgg(); tapSequence.Reset(); } };
            ContextMenuOpening += delegate { CancelKoloEasterEgg(); tapSequence.Reset(); };
        }

        private void ResetKoloEasterEggUsage()
        {
            koloEasterEgg.Reset(DateTime.UtcNow);
        }

        private bool CanPlayKoloEasterEgg()
        {
            BitmapSource[] poses;
            if ((!koloEasterEggEnabled && !koloEasterEgg.ManualRequested) || currentCharacter != "kolo" || !IsLoaded || !IsVisible || WindowState == WindowState.Minimized ||
                pointerHeld || notebook.Pressed || (notebookWindow != null && notebookWindow.Dragging) ||
                state != "idle" || (movement != "rest" && movement != "walk") || IsWindowAbilityActive || IsKoloPortalActive ||
                (ContextMenu != null && ContextMenu.IsOpen) || !frames.TryGetValue("disguise", out poses) || poses.Length != 8)
                return false;
            PlatformEdge support = FindSupport();
            return support != null && Math.Abs(FootY - support.Y) <= 2;
        }

        private void UpdateKoloEasterEgg(DateTime now, double seconds)
        {
            DateTime utc = now.ToUniversalTime();
            if (ContextMenu != null && ContextMenu.IsOpen) {
                tapSequence.Reset();
                if (koloEasterEgg.ManualRequested) CancelKoloEasterEgg();
            }
            bool wasActive = koloEasterEgg.Active;
            bool inUse = (koloEasterEggEnabled || koloEasterEgg.ManualRequested) && currentCharacter == "kolo" && IsLoaded && IsVisible && WindowState != WindowState.Minimized;
            bool started = koloEasterEgg.Observe(utc, inUse, CanPlayKoloEasterEgg());
            if (wasActive && !koloEasterEgg.Active)
            {
                RestoreKoloEasterEggPose();
                return;
            }
            if (started)
            {
                StopWalking();
                // Closing a detached notebook is part of the existing recovery
                // path. The change of costume never strands a second window.
                ReturnNotebook(false);
                CancelWindowAbility();
                tapSequence.Reset();
                nextWalk = now.AddSeconds(KoloEasterEggSession.DurationSeconds + 2);
                return;
            }
            if (!koloEasterEgg.Advance(seconds)) return;
            // Completing the real character switch refreshes menus, tool
            // portraits and persisted selection through a single existing path.
            SwitchCharacter("deepseek");
            ShowSpeech("咦，被发现啦！今天换我陪你。" );
        }

        private void RequestKoloEasterEggFromGesture()
        {
            if (currentCharacter != "kolo") return;
            koloEasterEgg.RequestManual();
            // The explicit completed gesture takes priority over the short
            // happy reaction from earlier taps, but never relocates a held pet.
            state = "idle"; stateEnds = DateTime.MaxValue;
            UpdateKoloEasterEgg(DateTime.Now, 0);
            ShowSpeech(koloEasterEgg.Active ? "四次双击，换装秘密被你发现啦！" : "收到啦，落稳后就给你看换装秘密。" );
        }

        private BitmapSource PrepareKoloEasterEggFrame(BitmapSource requested)
        {
            BitmapSource[] poses;
            if (!koloEasterEgg.Active || currentCharacter != "kolo" || !frames.TryGetValue("disguise", out poses) || poses.Length != 8)
                return requested;
            return poses[koloEasterEgg.FrameIndex(poses.Length)];
        }

        private void RestoreKoloEasterEggPose()
        {
            if (currentCharacter == "kolo" && sprite != null && frames != null)
                sprite.Source = PrepareNotebookFrame(frames["idle"][0]);
        }

        private void CancelKoloEasterEgg()
        {
            bool wasActive = koloEasterEgg.Active;
            koloEasterEgg.Cancel(DateTime.UtcNow);
            if (wasActive) { RestoreKoloEasterEggPose(); ScheduleNextWalk(DateTime.Now); }
        }

        private void SetKoloEasterEggEnabled(bool enabled)
        {
            if (!KoloEasterEggSettings.Save(KoloEasterEggSettings.DefaultPath, enabled))
            { ShowSpeech("换装彩蛋设置未保存，请稍后重试。" ); return; }
            koloEasterEggEnabled = enabled;
            CancelKoloEasterEgg();
            ResetKoloEasterEggUsage();
            ScheduleNextWalk(DateTime.Now);
            ShowSpeech(enabled ? "换装彩蛋已开启，陪你三分钟就揭晓。" : "换装彩蛋已关闭。" );
        }

        private void AddKoloEasterEggMenu(ContextMenu menu)
        {
            if (currentCharacter != "kolo") return;
            MenuItem item = StyledItem("♢", "待机换装彩蛋", null);
            item.IsCheckable = true;
            item.IsChecked = koloEasterEggEnabled;
            item.ToolTip = "使用珂洛满3分钟必定换装成DeepSeek；移动与安静模式也计时，隐藏时暂停。也可连续双击4次主动换装（共8下，每组间隔不超过1.5秒），无需开启自动彩蛋。拖拽或传送期间等动作结束再换装。";
            item.Click += delegate { SetKoloEasterEggEnabled(item.IsChecked); item.IsChecked = koloEasterEggEnabled; };
            menu.Items.Add(item);
        }
    }
}
