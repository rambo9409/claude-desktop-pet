using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    // Session-only ownership: starting, switching or hiding always puts the
    // notebook safely back in Claude's hands. A click is never a release.
    internal sealed class NotebookState
    {
        internal bool Held { get; private set; }
        internal bool Pressed { get; private set; }
        internal bool Dragged { get; private set; }
        internal Point PressPoint { get; private set; }
        internal NotebookState() { Held = true; }
        internal void Press(Point point) { Pressed = true; Dragged = false; PressPoint = point; }
        internal bool Move(Point point)
        {
            if (!Pressed || Dragged || (point - PressPoint).Length <= 6) return false;
            Dragged = true; Held = false; return true;
        }
        internal bool Release() { bool result = Dragged; Pressed = false; Dragged = false; return result; }
        internal void Detach() { Held = false; }
        internal void Recover() { Held = true; Pressed = false; Dragged = false; }
        internal bool BlocksMagic { get { return Held || Pressed; } }
    }

    internal static class NotebookGeometry
    {
        internal static Point ToImage(Point local, Size element, Size image)
        {
            double scale = Math.Min(element.Width / image.Width, element.Height / image.Height);
            if (scale <= 0 || Double.IsNaN(scale)) return new Point(-1, -1);
            return new Point((local.X - (element.Width - image.Width * scale) / 2) / scale,
                (local.Y - (element.Height - image.Height * scale) / 2) / scale);
        }
        internal static Point Clamp(Point position, Size size, Rect work)
        {
            return new Point(Math.Max(work.Left, Math.Min(position.X, work.Right - size.Width)),
                Math.Max(work.Top, Math.Min(position.Y, work.Bottom - size.Height)));
        }
        internal static Rect ReturnArea(Rect pet)
        { return new Rect(pet.Left + pet.Width * .12, pet.Top + pet.Height * .22, pet.Width * .76, pet.Height * .76); }
    }

    internal sealed class DetachedNotebookWindow : Window
    {
        private readonly Func<Point> cursor;
        private readonly Func<Point, Rect> workArea;
        private readonly Func<Point, bool> drop;
        private readonly Action giveBack;
        private Point press, start;
        private bool moved;
        internal bool Dragging { get; private set; }

        internal DetachedNotebookWindow(BitmapSource image, double width, Func<Point> readCursor,
            Func<Point, Rect> getWorkArea, Func<Point, bool> onDrop, Action onReturn)
        {
            cursor = readCursor; workArea = getWorkArea; drop = onDrop; giveBack = onReturn;
            Title = "珂洛的小本本 · 拖回身边或双击归还";
            Width = width; Height = Math.Max(60, width * image.PixelHeight / image.PixelWidth);
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
            Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false; ShowActivated = false;
            Content = new Image { Source = image, Stretch = Stretch.Uniform,
                ToolTip = "拖动小本本；拖回珂洛身上、双击或右键可归还。" };
            ContextMenu menu = new ContextMenu(); MenuItem item = new MenuItem { Header = "把本子还给珂洛" };
            item.Click += delegate { giveBack(); }; menu.Items.Add(item); ContextMenu = menu;
            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                e.Handled = true;
                if (e.ClickCount >= 2) { giveBack(); return; }
                if (!CaptureMouse() || !IsMouseCaptured) return;
                moved = false;
                press = cursor();
                start = new Point(Left, Top);
                Dragging = true;
            };
            MouseMove += delegate(object sender, MouseEventArgs e) {
                if (!Dragging || e.LeftButton != MouseButtonState.Pressed) return;
                Vector delta = cursor() - press; if (delta.Length > 4) moved = true;
                if (moved) { Left = start.X + delta.X; Top = start.Y + delta.Y; }
                e.Handled = true;
            };
            MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e) {
                if (!Dragging) return;
                Dragging = false; ReleaseMouseCapture(); e.Handled = true;
                if (!moved || !drop(new Point(Left + Width / 2, Top + Height / 2))) KeepOnScreen();
            };
            LostMouseCapture += delegate { if (Dragging) { Dragging = false; KeepOnScreen(); } };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; giveBack(); } };
            Closing += delegate { Dragging = false; if (IsMouseCaptured) ReleaseMouseCapture(); };
        }
        internal void Place(Point position) { Left = position.X; Top = position.Y; }
        internal void KeepOnScreen()
        {
            Point point = NotebookGeometry.Clamp(new Point(Left, Top), new Size(Width, Height),
                workArea(new Point(Left + Width / 2, Top + Height / 2)));
            Place(point);
        }
    }

    public sealed partial class PetWindow
    {
        private readonly NotebookState notebook = new NotebookState();
        private DetachedNotebookWindow notebookWindow;
        private BitmapSource notebookPressedFrame;
        private MenuItem notebookStatus;
        private bool returningNotebook;

        private void InitializeNotebook()
        {
            PreviewMouseLeftButtonDown += OnNotebookDown;
            PreviewMouseMove += OnNotebookMove;
            PreviewMouseLeftButtonUp += OnNotebookUp;
            StateChanged += delegate { if (WindowState == WindowState.Minimized) ReturnNotebook(false); };
            LostMouseCapture += delegate {
                if (!notebook.Pressed) return;
                // An interrupted hand-to-desktop drag does not grant magic.
                // Normal release clears Pressed before releasing capture.
                ReturnNotebook(false);
            };
        }
        private bool NotebookBlocksMagic
        { get { return notebook.BlocksMagic || (notebookWindow != null && notebookWindow.Dragging); } }

        private void AddNotebookMenu(ContextMenu menu)
        {
            if (currentCharacter != "kolo") return;
            notebookStatus = StyledItem("▤", "", null);
            notebookStatus.ToolTip = "按住手中本子拖出来；拖回珂洛身上、双击桌面本子或点这里即可归还。";
            notebookStatus.Click += delegate {
                if (!notebook.Held) ReturnNotebook(true);
                else ShowSpeech("拿着本子我会乖乖的。按住本子拖出来，我才有空施法；拖回来或双击本子就能还给我。" );
            };
            menu.Items.Add(notebookStatus); RefreshNotebookMenu();
        }
        private void RefreshNotebookMenu()
        {
            if (notebookStatus != null) notebookStatus.Header = notebook.Held ? "小本本在手 · 窗口魔法暂停" : "找回小本本 · 点击归还";
            RefreshNotebookPortrait();
        }
        private BitmapSource PrepareNotebookFrame(BitmapSource requested)
        {
            if (currentCharacter != "kolo" || !notebook.Held) return requested;
            if (notebook.Pressed && notebookPressedFrame != null) return notebookPressedFrame;
            foreach (KeyValuePair<string, BitmapSource[]> pair in frames) {
                if (pair.Key.StartsWith("book-", StringComparison.Ordinal) || pair.Key == "notebook") continue;
                int index = Array.IndexOf(pair.Value, requested); BitmapSource[] held;
                if (index >= 0 && frames.TryGetValue("book-" + pair.Key, out held) && held.Length > index) return held[index];
            }
            BitmapSource[] idle; return frames.TryGetValue("book-idle", out idle) ? idle[0] : requested;
        }
        private bool NotebookHit(Point spritePoint)
        {
            BitmapSource image = sprite.Source as BitmapSource;
            if (currentCharacter != "kolo" || !notebook.Held || image == null) return false;
            Rect box = PetAssets.NotebookHitBox(image);
            return !box.IsEmpty && box.Contains(NotebookGeometry.ToImage(spritePoint,
                new Size(sprite.ActualWidth, sprite.ActualHeight), new Size(image.Width, image.Height)));
        }
        private void OnNotebookDown(object sender, MouseButtonEventArgs e)
        {
            // Extra taps after the eighth click belong to the accepted costume
            // gesture, including the area where the notebook was just drawn.
            if (ProtectKoloCostumeTaps) return;
            if (e.ChangedButton != MouseButton.Left || !TryBeginNotebookPress(e.GetPosition(sprite), CursorPosition(e))) return;
            e.Handled = true;
        }
        private bool TryBeginNotebookPress(Point spritePoint, Point screenPoint)
        {
            if (!NotebookHit(spritePoint)) return false;
            CancelKoloEasterEgg();
            tapSequence.Reset(); CancelWindowAbility();
            // WPF can dispatch a synchronous MouseMove while establishing capture.
            // Arm only after that transfer, and consume the hit if capture failed.
            if (CaptureMouse() && IsMouseCaptured)
            {
                notebookPressedFrame = sprite.Source as BitmapSource;
                notebook.Press(screenPoint);
            }
            return true;
        }
        private void OnNotebookMove(object sender, MouseEventArgs e)
        {
            if (!notebook.Pressed) return;
            // Up commits the drop. LostMouseCapture cancels it. A synchronization
            // move without a pressed button is neither of those events.
            if (e.LeftButton == MouseButtonState.Pressed) MoveNotebookPress(CursorPosition(e));
            e.Handled = true;
        }
        private void MoveNotebookPress(Point point)
        {
            bool detached = notebook.Move(point);
            if (detached) {
                BitmapSource[] book;
                if (!frames.TryGetValue("notebook", out book)) { notebook.Recover(); ReleaseMouseCapture(); return; }
                double width = Math.Max(55, Math.Min(115, Width * .42));
                notebookWindow = new DetachedNotebookWindow(book[0], width,
                    delegate { return CursorPosition(null); }, GetWorkAreaForPoint, DropNotebook, delegate { ReturnNotebook(true); });
                DetachedNotebookWindow created = notebookWindow;
                created.Opacity = Opacity;
                MenuPalette palette = MenuPalette.ForCharacter("kolo");
                ConfigureMenuStyles(created.ContextMenu, palette);
                created.ContextMenu.Background = MenuBrush(palette.Surface);
                created.ContextMenu.Foreground = MenuBrush(palette.Text);
                created.ContextMenu.BorderBrush = MenuBrush(palette.Border);
                created.ContextMenu.FontFamily = new FontFamily("Microsoft YaHei UI");
                created.ContextMenu.FontSize = 13;
                created.Closed += delegate {
                    if (!returningNotebook && Object.ReferenceEquals(notebookWindow, created)) { notebookWindow = null; ReturnNotebook(false); }
                };
                created.Place(new Point(point.X - created.Width / 2, point.Y - created.Height / 2));
                if (IsLoaded && IsVisible) created.Show();
                RefreshNotebookMenu();
                sprite.Source = frames["idle"][0];
            }
            if (notebook.Dragged && notebookWindow != null)
                notebookWindow.Place(new Point(point.X - notebookWindow.Width / 2, point.Y - notebookWindow.Height / 2));
        }
        private void OnNotebookUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !notebook.Pressed) return;
            FinishNotebookPress(true); e.Handled = true;
        }
        private void FinishNotebookPress(bool announce)
        {
            bool draggedBook = notebook.Release(); notebookPressedFrame = null; ReleaseMouseCapture(); tapSequence.Reset();
            if (!draggedBook) {
                if (announce) ShowSpeech("拿着本子就不捣乱。按住本子拖出来试试；双击离手的本子可归还。" );
                return;
            }
            if (notebookWindow != null && DropNotebook(new Point(notebookWindow.Left + notebookWindow.Width / 2, notebookWindow.Top + notebookWindow.Height / 2))) return;
            if (notebookWindow != null) notebookWindow.KeepOnScreen();
            if (announce) ShowSpeech(windowMagicEnabled ? "本子被拿走啦！等冷却好了，我又可以施法了。" : "本子被拿走啦。窗口魔法仍处于关闭状态，可在右键菜单开启。" );
        }
        private bool DropNotebook(Point point)
        {
            if (currentCharacter != "kolo" || !NotebookGeometry.ReturnArea(new Rect(Left, Top, Width, Height)).Contains(point)) return false;
            ReturnNotebook(true); return true;
        }
        private void ReturnNotebook(bool announce)
        {
            notebook.Recover(); notebookPressedFrame = null; CancelWindowAbility();
            if (IsMouseCaptured) ReleaseMouseCapture(); tapSequence.Reset();
            DetachedNotebookWindow detached = notebookWindow; notebookWindow = null;
            if (detached != null) { returningNotebook = true; try { detached.Close(); } finally { returningNotebook = false; } }
            RefreshNotebookMenu();
            if (currentCharacter == "kolo" && frames != null) sprite.Source = PrepareNotebookFrame(frames["idle"][0]);
            if (announce && currentCharacter == "kolo") ShowSpeech("本子回来啦。我会乖乖拿着，窗口魔法暂停。" );
        }
    }
}
