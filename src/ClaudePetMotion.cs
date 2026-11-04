using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

[assembly: AssemblyTitle("原神启动")]
[assembly: AssemblyDescription("多形象桌宠、资讯、窗口魔法与原神战斗循环助手")]
[assembly: AssemblyProduct("原神启动")]
[assembly: AssemblyVersion("2.9.0.0")]
[assembly: AssemblyFileVersion("2.9.0.0")]

namespace ClaudePet
{
    internal static class NativeWindow
    {
        internal delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);
        internal delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, ref RECT rectangle, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MONITORINFO
        {
            internal int Size;
            internal RECT Monitor;
            internal RECT Work;
            internal uint Flags;
        }

        [DllImport("user32.dll")]
        internal static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        internal static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out POINT point);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        internal static extern int GetWindowLong(IntPtr handle, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        internal static extern int SetWindowLong(IntPtr handle, int index, int value);

        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr handle);

        [DllImport("user32.dll")]
        internal static extern bool IsIconic(IntPtr handle);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr handle, out RECT rect);

        [DllImport("user32.dll")]
        internal static extern int GetWindowTextLength(IntPtr handle);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
    }

    internal sealed class PlatformEdge
    {
        internal IntPtr Handle;
        internal double Left;
        internal double Right;
        internal double Y;
        internal double Bottom;
        internal bool IsFloor;
    }

    // Gait progress follows distance, so changing speed cannot skip backwards
    // through the poses. A full cycle contains two alternating foot/hand steps.
    internal static class MotionCycle
    {
        internal static double Advance(double phase, double distance, double cycleDistance)
        {
            double next = phase + distance / Math.Max(1, cycleDistance);
            return next - Math.Floor(next);
        }

        internal static int Frame(double phase, int count, bool reversed)
        {
            int index = Math.Min(count - 1, (int)Math.Floor(phase * count));
            return reversed ? count - 1 - index : index;
        }

        internal static int ClimbFacing(int side, bool screenEdge)
        {
            // A window is on the inside of its edge; the screen wall is outside.
            return screenEdge ? side : -side;
        }

        internal static double ClimbLeft(double wallX, double width, int facing)
        {
            // The motion sheets share a hand-contact anchor at x = 400 / 512.
            double contact = 400.0 / 512.0;
            return wallX - width * (facing > 0 ? contact : 1 - contact);
        }
    }

    internal static class MovementGait
    {
        internal static string Normalize(string preference)
        {
            return preference == "run" || preference == "mixed" ? preference : "stroll";
        }

        internal static string Choose(string preference, double choice)
        {
            preference = Normalize(preference);
            return preference == "mixed" ? (choice < 0.65 ? "stroll" : "run") : preference;
        }

        internal static double Speed(string character, string gait, double variation, double width)
        {
            variation = Math.Max(0, Math.Min(1, variation));
            double speed = gait == "run" ? 95 + 45 * variation : 30 + 15 * variation;
            double sprint = character == "varesa" && gait == "run" ? 4 : 1;
            return speed * sprint * Math.Max(0.6, width / 165.0);
        }

        internal static string FrameKey(string gait)
        {
            // Legacy walk frames contain each character's fast movement:
            // Hu Tao's butterfly flight, Varesa's masked sprint, or a normal run.
            return gait == "run" ? "walk" : "stroll";
        }

        internal static string PreferencePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet", "movement-mode.txt"); }
        }

        internal static string LoadPreference()
        {
            try { return Normalize(File.ReadAllText(PreferencePath, Encoding.UTF8).Trim()); }
            catch { return "stroll"; }
        }

        internal static void SavePreference(string preference)
        {
            PetStorage.TryWriteAllText(PreferencePath, Normalize(preference), "movement-mode");
        }
    }

    // A separate window keeps speech readable even when the pet's Viewbox is tiny.
    // It is owned by the pet and cannot take focus or intercept mouse interaction.
    internal sealed class SpeechWindow : Window
    {
        internal readonly TextBlock Text;

        internal SpeechWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            UseLayoutRounding = true;
            Text = new TextBlock {
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 15,
                LineHeight = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 43, 36)),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 7, 12, 7)
            };
            Content = new Border {
                Child = Text,
                Background = new SolidColorBrush(Color.FromArgb(250, 255, 248, 235)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(105, 77, 65)),
                BorderThickness = new Thickness(1.25),
                CornerRadius = new CornerRadius(12),
                MinWidth = 112,
                MaxWidth = 300
            };
            SourceInitialized += delegate {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                const int extendedStyle = -20;
                const int noActivate = 0x08000000;
                const int transparent = 0x00000020;
                const int toolWindow = 0x00000080;
                NativeWindow.SetWindowLong(handle, extendedStyle,
                    NativeWindow.GetWindowLong(handle, extendedStyle) | noActivate | transparent | toolWindow);
            };
        }

        internal static Rect FindVisiblePixels(BitmapSource source)
        {
            BitmapSource pixels = source;
            if (pixels.Format != PixelFormats.Bgra32 && pixels.Format != PixelFormats.Pbgra32)
                pixels = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = pixels.PixelWidth;
            int height = pixels.PixelHeight;
            int stride = width * 4;
            byte[] data = new byte[stride * height];
            pixels.CopyPixels(data, stride, 0);
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Ignore faint shadow/antialias pixels when finding the character's head.
                    if (data[y * stride + x * 4 + 3] < 32) continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
            if (right < left) return new Rect(0, 0, 1, 1);
            return new Rect((double)left / width, (double)top / height,
                (double)(right - left + 1) / width, (double)(bottom - top + 1) / height);
        }

        internal static Point Place(Rect character, Size speech, Rect work)
        {
            const double gap = 5;
            double x = character.Left + (character.Width - speech.Width) / 2;
            double y = character.Top - speech.Height - gap;
            if (y < work.Top + gap)
            {
                // At the top edge put speech beside the head instead of covering it.
                y = character.Top;
                if (character.Right + gap + speech.Width <= work.Right - gap)
                    x = character.Right + gap;
                else if (character.Left - gap - speech.Width >= work.Left + gap)
                    x = character.Left - gap - speech.Width;
            }
            return new Point(
                Math.Max(work.Left + gap, Math.Min(x, work.Right - speech.Width - gap)),
                Math.Max(work.Top + gap, Math.Min(y, work.Bottom - speech.Height - gap)));
        }
    }

    public sealed partial class PetWindow : Window
    {
        private static readonly string[] CharacterOrder = { "kolo", "gpt6a", "deepseek", "hutao", "ganyu", "varesa" };
        private static readonly Dictionary<string, string> CharacterNames = new Dictionary<string, string> {
            { "kolo", "珂洛" },
            { "gpt6a", "GPT6A·龙女" },
            { "deepseek", "DeepSeek·小肥鱼" },
            { "hutao", "胡桃" },
            { "ganyu", "甘雨" },
            { "varesa", "瓦雷莎" }
        };

        internal static string CharacterDisplayName(string key)
        {
            string name;
            return key != null && CharacterNames.TryGetValue(key, out name) ? name : CharacterNames["kolo"];
        }

        internal static string CharacterMark(string key)
        {
            switch (key) {
                case "gpt6a": return "✧";
                case "deepseek": return "≈";
                case "hutao": return "✿";
                case "ganyu": return "❄";
                case "varesa": return "⚡";
                default: return "✦";
            }
        }

        private readonly Image sprite;
        private readonly SpeechWindow speechWindow;
        private readonly Dictionary<BitmapSource, Rect> visiblePixelCache = new Dictionary<BitmapSource, Rect>();
        private bool speechPositionLocked;
        private readonly DispatcherTimer timer;
        private readonly Dictionary<string, Dictionary<string, BitmapSource[]>> characterCache = new Dictionary<string, Dictionary<string, BitmapSource[]>>();
        private readonly List<MenuItem> characterItems = new List<MenuItem>();
        private Dictionary<string, BitmapSource[]> frames;
        private readonly Random random = new Random();
        private readonly string[] phrases = {
            "让我想一想。", "先把问题拆开。", "今天也要加油。", "别忘了喝水。",
            "我会陪着你。", "要休息一会儿吗？", "好点子正在路上。", "慢慢来，也很好。"
        };

        private string currentCharacter;
        private string state = "idle";
        private string movement = "rest";
        private string preferredGait;
        private string activeGait = "run";
        private DateTime stateStarted = DateTime.Now;
        private DateTime stateEnds = DateTime.MaxValue;
        private DateTime movementStarted = DateTime.Now;
        private DateTime walkEnds = DateTime.MinValue;
        private DateTime nextWalk;
        private DateTime nextSpeedChange = DateTime.MinValue;
        private DateTime nextHighlightsRefresh = DateTime.Now.AddSeconds(5);
        private DateTime lastTick = DateTime.Now;
        private DateTime bubbleEnds = DateTime.Now.AddSeconds(4);
        private DateTime platformCacheTime = DateTime.MinValue;
        private int frameMs = 150;
        private int direction = -1;
        private double moveSpeed;
        private double targetMoveSpeed;
        private double runPhase;
        private double climbPhase;
        private double climbAttachLeft;
        private double fallVelocityX;
        private double fallVelocityY;
        private double dragVelocityX;
        private double dragVelocityY;
        private double heldTilt;
        private int climbSide;
        private int climbVerticalDirection;
        private bool climbIsScreen;
        private DateTime climbReverseAt = DateTime.MinValue;
        private Point lastDragPoint;
        private DateTime lastDragTime = DateTime.MinValue;
        private bool autoWalk = true;
        private bool quietMode;
        private bool landedFromFall;
        private bool highlightsRefreshing;
        private IntPtr ownHandle;
        private IntPtr supportHandle;
        private IntPtr fallFromHandle;
        private readonly IntPtr startupForeground;
        private Matrix fromDevice = Matrix.Identity;
        private List<PlatformEdge> platformCache = new List<PlatformEdge>();
        private Point pointerStart;
        private Point windowStart;
        private bool pointerHeld;
        private bool dragged;
        private HighlightsWindow highlightsWindow;
        private GptResetWindow gptResetWindow;
        private TextBlock menuCharacterText;

        public PetWindow()
        {
            startupForeground = NativeWindow.GetForegroundWindow();
            currentCharacter = PetSettings.LoadCharacter();
            if (!CharacterNames.ContainsKey(currentCharacter)) currentCharacter = "kolo";
            quietMode = PetSettings.LoadQuietMode();
            preferredGait = MovementGait.LoadPreference();
            ScheduleNextWalk(DateTime.Now);
            Title = "原神启动 · " + CharacterNames[currentCharacter];
            Width = 165;
            Height = 225;
            MinWidth = 100;
            MinHeight = 136;
            MaxWidth = 450;
            MaxHeight = 614;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            Focusable = true;

            frames = LoadCharacterFrames(currentCharacter);

            Viewbox viewbox = new Viewbox();
            viewbox.Stretch = Stretch.Uniform;
            Grid root = new Grid();
            root.Width = 330;
            root.Height = 450;
            root.Background = Brushes.Transparent;
            viewbox.Child = root;
            Content = viewbox;

            sprite = new Image();
            sprite.Source = PrepareNotebookFrame(frames["idle"][0]);
            sprite.Stretch = Stretch.Uniform;
            sprite.HorizontalAlignment = HorizontalAlignment.Stretch;
            sprite.VerticalAlignment = VerticalAlignment.Bottom;
            sprite.Margin = new Thickness(0, 42, 0, 0);
            sprite.RenderTransformOrigin = new Point(0.5, 0.82);
            InitializeWings(root);
            root.Children.Add(sprite);
            InitializeEffects(root);
            InitializeNightsoul(root);
            InitializeAiAbilities(root);

            speechWindow = new SpeechWindow();
            speechWindow.Text.Text = "你好，我是" + CharacterNames[currentCharacter] + "。";
            InitializeNotebook();

            ContextMenu = BuildMenu();
            MouseLeftButtonDown += OnPointerDown;
            MouseMove += OnPointerMove;
            MouseLeftButtonUp += OnPointerUp;
            MouseWheel += OnMouseWheel;
            KeyDown += OnKeyDown;

            SourceInitialized += delegate {
                ownHandle = new WindowInteropHelper(this).Handle;
                HwndSource source = HwndSource.FromHwnd(ownHandle);
                if (source != null && source.CompositionTarget != null)
                    fromDevice = source.CompositionTarget.TransformFromDevice;
                monitorsCachedAt = DateTime.MinValue;
                platformCacheTime = DateTime.MinValue;
            };

            Loaded += delegate {
                PlaceAtStart();
                if (ShowActivated) Activate();
                speechWindow.Owner = this;
                UpdateSpeechPosition();
            };
            IsVisibleChanged += delegate {
                if (!IsVisible) CancelWindowAbility();
                if (!IsVisible) ReturnNotebook(false);
                if (!IsVisible) CancelAiAbilities();
                if (!IsVisible) tapSequence.Reset();
                if (!IsVisible) ClearGanyuTraversal();
                if (!IsVisible && movement == "wallrun") EndVaresaRun();
                if (!IsVisible) CancelWings();
                if (!IsVisible && speechWindow.IsVisible) speechWindow.Hide();
            };

            timer = new DispatcherTimer(DispatcherPriority.Render);
            timer.Interval = TimeSpan.FromMilliseconds(16);
            timer.Tick += OnTick;
            timer.Start();
        }

        private double FootInset { get { return Height * 0.038; } }
        private double FootY { get { return Top + Height - FootInset; } }
        private double CenterX { get { return Left + Width * 0.5; } }

        private Dictionary<string, BitmapSource[]> LoadCharacterFrames(string character)
        {
            Dictionary<string, BitmapSource[]> cached;
            if (!characterCache.TryGetValue(character, out cached))
            {
                cached = PetAssets.Load(character);
                characterCache[character] = cached;
            }
            return cached;
        }

        private void SwitchCharacter(string key)
        {
            if (!CharacterNames.ContainsKey(key)) return;
            ReturnNotebook(false);
            tapSequence.Reset();
            CancelWindowAbility();
            CancelAiAbilities();
            ClearGanyuTraversal();
            bool leftWall = movement == "wallrun";
            if (leftWall) EndVaresaRun();
            currentCharacter = key;
            platformCacheTime = DateTime.MinValue;
            frames = LoadCharacterFrames(key);
            CancelButterflyEffects();
            CancelWings();
            state = "idle";
            movement = "rest";
            if (leftWall && FindSupport() == null) BeginFall(0);
            stateStarted = DateTime.Now;
            stateEnds = DateTime.MaxValue;
            sprite.Source = PrepareNotebookFrame(frames["idle"][0]);
            Title = "原神启动 · " + CharacterNames[key];
            PetSettings.SaveCharacter(key);
            if (ContextMenu != null) ContextMenu.IsOpen = false;
            ContextMenu = BuildMenu();
            if (highlightsWindow != null) highlightsWindow.ApplyCharacterTheme(key, CompanionPortrait());
            if (combatPlannerWindow != null) combatPlannerWindow.ApplyCharacterTheme(key);
            if (gptResetWindow != null) gptResetWindow.ApplyCharacterTheme(key);
            ScheduleNextWalk(DateTime.Now);
            ShowSpeech("已换成" + CharacterNames[key] + "啦。");
        }

        private BitmapSource CompanionPortrait()
        {
            return currentCharacter == "kolo" && notebook.Held ? frames["book-idle"][0] : frames["idle"][0];
        }

        private void RefreshNotebookPortrait()
        {
            if (highlightsWindow != null && currentCharacter == "kolo")
                highlightsWindow.SetPortrait(CompanionPortrait());
        }

        private void OpenHighlights()
        {
            if (highlightsWindow == null)
            {
                highlightsWindow = new HighlightsWindow(currentCharacter, CompanionPortrait());
                highlightsWindow.Closed += delegate { highlightsWindow = null; };
                highlightsWindow.Show();
            }
            else
            {
                highlightsWindow.ApplyCharacterTheme(currentCharacter, CompanionPortrait());
                highlightsWindow.Activate();
            }
        }

        private void OpenGptResetPrediction()
        {
            if (gptResetWindow == null)
            {
                gptResetWindow = new GptResetWindow(currentCharacter);
                gptResetWindow.Closed += delegate { gptResetWindow = null; };
                gptResetWindow.Show();
            }
            else
            {
                gptResetWindow.ApplyCharacterTheme(currentCharacter);
                gptResetWindow.Activate();
            }
        }

        private void RefreshHighlightsInBackground()
        {
            if (highlightsRefreshing) return;
            if (DailyHighlightsService.HasFreshCache(3))
            {
                nextHighlightsRefresh = DateTime.Now.AddHours(3);
                return;
            }
            highlightsRefreshing = true;
            nextHighlightsRefresh = DateTime.Now.AddHours(6);
            Task<HighlightsResult> task = Task.Factory.StartNew<HighlightsResult>(delegate { return DailyHighlightsService.FetchAndSave(); });
            task.ContinueWith(delegate(Task<HighlightsResult> finished) {
                Dispatcher.BeginInvoke(new Action(delegate {
                    highlightsRefreshing = false;
                    if (finished.Status == TaskStatus.RanToCompletion && finished.Result.Items.Count > 0)
                        ShowSpeech("每日看点已经准备好啦！");
                }));
            });
        }

        private void ScheduleNextWalk(DateTime from)
        {
            double minimumSeconds = quietMode ? 35 : 4;
            double randomSeconds = quietMode ? 40 : 7;
            nextWalk = from.AddSeconds(minimumSeconds + random.NextDouble() * randomSeconds);
        }

        private void Play(string nextState, string text, int durationMs, int speedMs)
        {
            CancelWindowAbility();
            ClearGanyuTraversal();
            if (movement == "wallrun") EndVaresaRun();
            movement = "rest";
            state = nextState;
            stateStarted = DateTime.Now;
            stateEnds = stateStarted.AddMilliseconds(durationMs);
            frameMs = speedMs;
            ScheduleNextWalk(stateEnds);
            ShowSpeech(text);
        }

        private void ShowSpeech(string text)
        {
            speechWindow.Text.Text = text;
            speechPositionLocked = false;
            bubbleEnds = DateTime.Now.AddSeconds(Math.Max(2.8, text.Length * 0.16));
            UpdateSpeechPosition();
        }

        private void UpdateSpeechPosition()
        {
            if (!IsLoaded || !IsVisible || WindowState == WindowState.Minimized || DateTime.Now >= bubbleEnds)
            {
                if (speechWindow.IsVisible) speechWindow.Hide();
                if (DateTime.Now >= bubbleEnds) speechPositionLocked = false;
                return;
            }
            // A phrase belongs to its initial screen position for its lifetime.
            // Animation, movement, resizing and transformation must not jitter it.
            if (speechPositionLocked)
            {
                if (!speechWindow.IsVisible) speechWindow.Show();
                return;
            }
            if (!sprite.IsArrangeValid) UpdateLayout();
            BitmapSource bitmap = sprite.Source as BitmapSource;
            if (bitmap == null || sprite.ActualWidth <= 0 || sprite.ActualHeight <= 0) return;
            Rect visible;
            if (!visiblePixelCache.TryGetValue(bitmap, out visible))
            {
                visible = SpeechWindow.FindVisiblePixels(bitmap);
                visiblePixelCache[bitmap] = visible;
            }
            double imageScale = Math.Min(sprite.ActualWidth / bitmap.Width, sprite.ActualHeight / bitmap.Height);
            double imageWidth = bitmap.Width * imageScale;
            double imageHeight = bitmap.Height * imageScale;
            Rect local = new Rect((sprite.ActualWidth - imageWidth) / 2 + visible.X * imageWidth,
                (sprite.ActualHeight - imageHeight) / 2 + visible.Y * imageHeight,
                visible.Width * imageWidth, visible.Height * imageHeight);
            Rect onPet = sprite.TransformToAncestor(this).TransformBounds(local);
            Point pixelTop = PointToScreen(onPet.TopLeft);
            Point pixelBottom = PointToScreen(onPet.BottomRight);
            Rect character = new Rect(fromDevice.Transform(pixelTop), fromDevice.Transform(pixelBottom));
            Rect work = GetWorkAreaForPoint(new Point(character.Left + character.Width / 2, character.Top));
            FrameworkElement speech = (FrameworkElement)speechWindow.Content;
            speech.Measure(new Size(Math.Max(1, work.Width - 10), double.PositiveInfinity));
            Point location = SpeechWindow.Place(character, speech.DesiredSize, work);
            speechWindow.Left = location.X;
            speechWindow.Top = location.Y;
            speechPositionLocked = true;
            if (!speechWindow.IsVisible) speechWindow.Show();
        }

        private void OnTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            double deltaSeconds = Math.Max(0.001, Math.Min(0.05, (now - lastTick).TotalSeconds));
            lastTick = now;
            if (now >= nextHighlightsRefresh) RefreshHighlightsInBackground();
            if (now >= stateEnds)
            {
                state = "idle";
                stateStarted = now;
                stateEnds = DateTime.MaxValue;
                frameMs = 150;
            }

            UpdateWindowAbility(now, deltaSeconds);
            UpdateAiAbilities(now, deltaSeconds);
            if (state == "idle" && !pointerHeld && !notebook.Pressed && !IsWindowAbilityActive) UpdateMovement(now, deltaSeconds);

            string visibleState = state;
            DateTime animationStart = stateStarted;
            if (state == "idle" && movement == "climb")
            {
                visibleState = climbVerticalDirection < 0 ? "climb-up" : "climb-down";
                animationStart = movementStarted;
            }
            else if (state == "idle" && (movement == "walk" || movement == "wallrun"))
            {
                visibleState = ActiveMovementFrameKey;
                animationStart = movementStarted;
            }
            else if (state == "idle" && movement == "jump" && frames.ContainsKey("jump"))
            {
                visibleState = "jump";
                animationStart = movementStarted;
            }
            else if (state == "idle" && movement == "held")
            {
                visibleState = frames.ContainsKey("held") ? "held" : "idle";
                animationStart = movementStarted;
            }
            else if (state == "idle" && movement == "fall" && frames.ContainsKey("fall"))
            {
                visibleState = "fall";
                animationStart = movementStarted;
            }

            BitmapSource[] current = frames[visibleState];
            int elapsed = Math.Max(0, (int)(now - animationStart).TotalMilliseconds);
            int index = current.Length == 1 ? 0 : (elapsed / frameMs) % current.Length;
            if (state == "idle" && (movement == "walk" || movement == "wallrun"))
                index = MotionCycle.Frame(runPhase, current.Length, false);
            else if (state == "idle" && movement == "climb")
                // Down artwork is the exact reverse of up. Keep the physical pose
                // continuous when direction changes halfway through a climb.
                index = MotionCycle.Frame(climbPhase, current.Length, climbVerticalDirection > 0);
            else if (state == "idle" && movement == "jump")
                index = GanyuJumpFrame(current.Length);

            TransformGroup transforms = new TransformGroup();
            sprite.RenderTransformOrigin = new Point(0.5, movement == "wallrun" ? 0.5 : 0.82);
            if (movement == "wallrun" && state == "idle")
            {
                double fit = VaresaRunTrack.Fit(varesaRunAngle);
                transforms.Children.Add(new ScaleTransform(direction * fit, fit));
                transforms.Children.Add(new RotateTransform(varesaRunAngle));
            }
            else if ((movement == "walk" || movement == "jump") && state == "idle")
            {
                transforms.Children.Add(new ScaleTransform(direction > 0 ? 1 : -1, 1));
                if (movement == "jump") ApplyGanyuJumpPose(transforms);
            }
            else if (movement == "climb" && state == "idle")
            {
                transforms.Children.Add(new ScaleTransform(MotionCycle.ClimbFacing(climbSide, climbIsScreen), 1));
            }
            else if (movement == "held" && state == "idle")
            {
                transforms.Children.Add(new ScaleTransform(0.98, 1.035));
                transforms.Children.Add(new RotateTransform(heldTilt));
                transforms.Children.Add(new TranslateTransform(0, 4));
            }
            else if (movement == "fall" && state == "idle")
            {
                transforms.Children.Add(new ScaleTransform(direction > 0 ? 1 : -1, 1));
                double tilt = HasGlider ? 3 + Math.Sin(glideAnimation.AirTime * 2.4) * 2 :
                    Math.Min(18, 4 + Math.Abs(fallVelocityY) * 0.035);
                transforms.Children.Add(new RotateTransform(direction * tilt));
            }
            else if (state == "idle")
            {
                double wave = Math.Sin(elapsed / 470.0);
                transforms.Children.Add(new ScaleTransform(1.0 + wave * 0.006, 1.0 + wave * 0.012));
                transforms.Children.Add(new TranslateTransform(0, -Math.Abs(wave) * 1.5));
            }
            else if (state == "happy")
            {
                transforms.Children.Add(new RotateTransform(Math.Sin(elapsed / 90.0) * 1.5));
            }
            BitmapSource requested = PrepareGlidingFrame(current[index], deltaSeconds);
            requested = PrepareAbilityFrame(requested);
            requested = PrepareAiAbilityFrame(requested);
            requested = PrepareNotebookFrame(requested);
            RenderButterflyEffects(requested, transforms, deltaSeconds);
            RenderWings(transforms);

            UpdateNightsoul(now, deltaSeconds);

            UpdateSpeechPosition();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel && !CanCloseAiAbilities()) e.Cancel = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            timer.Stop();
            ReturnNotebook(false);
            CancelWindowAbility();
            CloseAiAbilities();
            ClearGanyuTraversal();
            CancelWings();
            speechWindow.Close();
            if (gptResetWindow != null) gptResetWindow.Close();
            base.OnClosed(e);
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) {
                    WriteCrashLog(e.Exception);
                    MessageBox.Show("桌宠遇到错误，日志已保存。", "原神启动", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Handled = true;
                };
                app.Run(new PetWindow());
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
                MessageBox.Show("桌宠启动失败，日志已保存。", "原神启动", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void WriteCrashLog(Exception exception)
        {
            // error.log keeps the full stack of the latest crash; diagnostics.log keeps a one-line history.
            PetStorage.TryWriteAllText(Path.Combine(PetStorage.RootDirectory, "error.log"), DateTime.Now.ToString("s") + Environment.NewLine + exception, "crash-log");
            PetStorage.Log("crash", exception);
        }
    }
}
