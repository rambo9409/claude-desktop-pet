using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    // All lottery timing is independent of render frequency. Busy time never
    // counts towards the uninterrupted idle requirement, and missed polls are
    // not replayed after sleep or a blocked UI thread.
    internal sealed class KoloEasterEggSession
    {
        internal const double IdleSeconds = 12;
        internal const double PollSeconds = 30;
        internal const double CooldownSeconds = 300;
        internal const double GraceSeconds = 45;
        internal const double DurationSeconds = 4.8;
        internal const double Probability = .12;
        private DateTime idleSinceUtc = DateTime.MinValue;
        private DateTime lastObservedUtc = DateTime.MinValue;
        internal DateTime NextAllowedUtc { get; private set; }
        internal DateTime NextPollUtc { get; private set; }
        internal bool Active { get; private set; }
        internal double Elapsed { get; private set; }

        internal KoloEasterEggSession(DateTime nowUtc)
        {
            NextAllowedUtc = nowUtc.AddSeconds(CooldownSeconds);
            NextPollUtc = NextAllowedUtc;
        }

        internal bool Observe(DateTime nowUtc, bool eligible, Func<double> draw)
        {
            if (lastObservedUtc != DateTime.MinValue && nowUtc < lastObservedUtc)
            {
                Cancel(nowUtc);
                NextAllowedUtc = nowUtc.AddSeconds(CooldownSeconds);
                NextPollUtc = NextAllowedUtc;
            }
            lastObservedUtc = nowUtc;
            if (!eligible)
            {
                idleSinceUtc = DateTime.MinValue;
                if (Active) Cancel(nowUtc);
                return false;
            }
            if (Active) return false;
            if (idleSinceUtc == DateTime.MinValue) idleSinceUtc = nowUtc;
            if ((nowUtc - idleSinceUtc).TotalSeconds < IdleSeconds || nowUtc < NextAllowedUtc || nowUtc < NextPollUtc)
                return false;
            NextPollUtc = nowUtc.AddSeconds(PollSeconds);
            double sample = draw();
            if (Double.IsNaN(sample) || sample < 0 || sample >= Probability) return false;
            Active = true;
            Elapsed = 0;
            idleSinceUtc = DateTime.MinValue;
            NextAllowedUtc = nowUtc.AddSeconds(CooldownSeconds);
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
            idleSinceUtc = DateTime.MinValue;
            DateTime grace = nowUtc.AddSeconds(GraceSeconds);
            if (NextAllowedUtc < grace) NextAllowedUtc = grace;
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
            StateChanged += delegate { if (WindowState == WindowState.Minimized) CancelKoloEasterEgg(); };
        }

        private bool CanPlayKoloEasterEgg()
        {
            BitmapSource[] poses;
            if (!koloEasterEggEnabled || currentCharacter != "kolo" || !IsLoaded || !IsVisible || WindowState == WindowState.Minimized ||
                quietMode || pointerHeld || notebook.Pressed || (notebookWindow != null && notebookWindow.Dragging) ||
                state != "idle" || movement != "rest" || IsWindowAbilityActive || IsKoloPortalActive ||
                (ContextMenu != null && ContextMenu.IsOpen) || !frames.TryGetValue("disguise", out poses) || poses.Length != 8)
                return false;
            PlatformEdge support = FindSupport();
            return support != null && Math.Abs(FootY - support.Y) <= 2;
        }

        private void UpdateKoloEasterEgg(DateTime now, double seconds)
        {
            DateTime utc = now.ToUniversalTime();
            bool wasActive = koloEasterEgg.Active;
            bool started = koloEasterEgg.Observe(utc, CanPlayKoloEasterEgg(), delegate { return random.NextDouble(); });
            if (wasActive && !koloEasterEgg.Active)
            {
                RestoreKoloEasterEggPose();
                return;
            }
            if (started)
            {
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
            ScheduleNextWalk(DateTime.Now);
            ShowSpeech(enabled ? "待机换装彩蛋已开启，偶尔留意我的发饰吧。" : "待机换装彩蛋已关闭。" );
        }

        private void AddKoloEasterEggMenu(ContextMenu menu)
        {
            if (currentCharacter != "kolo") return;
            MenuItem item = StyledItem("♢", "待机换装彩蛋", null);
            item.IsCheckable = true;
            item.IsChecked = koloEasterEggEnabled;
            item.ToolTip = "闲下来时，珂洛可能偷偷换装成DeepSeek；安静模式中暂停。";
            item.Click += delegate { SetKoloEasterEggEnabled(item.IsChecked); item.IsChecked = koloEasterEggEnabled; };
            menu.Items.Add(item);
        }
    }
}
