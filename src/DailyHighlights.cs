using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Xml;

namespace ClaudePet
{
    public sealed class HighlightsWindow : Window
    {
        private readonly StackPanel contentPanel;
        private readonly TextBlock statusText;
        private readonly Button refreshButton;
        private readonly ScrollViewer scroll;
        private readonly HighlightsLayout sectionLayout;
        private readonly DispatcherTimer dragScrollTimer;
        private readonly Image portraitImage;
        private readonly TextBlock characterMark, companionText;
        private readonly Border statusDot;
        private readonly List<Border> dropMarkers = new List<Border>();
        private readonly Dictionary<string, FrameworkElement> sectionBodies = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, FrameworkElement> sectionHeaders = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, Button> sourceNavigation = new Dictionary<string, Button>();
        private const string SectionDragFormat = "KoloPet.HighlightsSection";
        private HighlightsResult currentResult, deferredResult;
        private string draggingSource;
        private int dropIndex = -1;
        private double dragScrollStep;
        private Point dragPointer;
        private bool dropCompleted, refreshing;

        public HighlightsWindow() : this("kolo", null) { }
        public HighlightsWindow(string character) : this(character, null) { }
        public HighlightsWindow(string character, BitmapSource portrait)
        {
            sectionLayout = PetSettings.LoadHighlightsLayout();
            dragScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            dragScrollTimer.Tick += delegate {
                if (draggingSource != null && dragScrollStep != 0)
                {
                    scroll.ScrollToVerticalOffset(scroll.VerticalOffset + dragScrollStep);
                    scroll.UpdateLayout(); UpdateDropMarker(dragPointer);
                }
            };
            Title = "原神启动 · 每日看点"; MinWidth = 620; MinHeight = 440;
            Width = Math.Min(780, Math.Max(MinWidth, SystemParameters.WorkArea.Width - 40));
            Height = Math.Min(760, Math.Max(MinHeight, SystemParameters.WorkArea.Height - 40));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip;
            AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = true; Topmost = true;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            ConfigureStyles();
            Border shell = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(24), ClipToBounds = true };
            Theme(shell, Border.BackgroundProperty, "WindowSurface");
            Theme(shell, Border.BorderBrushProperty, "Outline");
            Content = shell;
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(108) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 39 });
            shell.Child = layout;
            Border header = new Border { CornerRadius = new CornerRadius(23, 23, 0, 0), Padding = new Thickness(23, 12, 22, 12) };
            Theme(header, Border.BackgroundProperty, "HeaderSurface"); layout.Children.Add(header);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed) DragMove();
            };
            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Child = headerGrid;
            Border portraitFrame = new Border { Width = 65, Height = 65, CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };
            Theme(portraitFrame, Border.BackgroundProperty, "SelectedSurface"); Theme(portraitFrame, Border.BorderBrushProperty, "Outline");
            Grid portraitGrid = new Grid();
            portraitImage = new Image { Width = 73, Height = 82, Stretch = Stretch.Uniform, IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(portraitImage, BitmapScalingMode.HighQuality);
            characterMark = Label("✦", 31, "Accent"); characterMark.FontFamily = new FontFamily("Segoe UI Symbol");
            characterMark.HorizontalAlignment = HorizontalAlignment.Center; characterMark.VerticalAlignment = VerticalAlignment.Center;
            portraitGrid.Children.Add(characterMark); portraitGrid.Children.Add(portraitImage); portraitFrame.Child = portraitGrid; headerGrid.Children.Add(portraitFrame);
            StackPanel heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0) };
            TextBlock title = Label("每日看点", 26, "PrimaryText"); title.FontWeight = FontWeights.Bold; heading.Children.Add(title);
            companionText = Label(String.Empty, 12, "SecondaryText"); companionText.Margin = new Thickness(1, 5, 0, 0); heading.Children.Add(companionText);
            Grid.SetColumn(heading, 1); headerGrid.Children.Add(heading);
            StackPanel controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(controls, 2); headerGrid.Children.Add(controls);
            refreshButton = ActionButton("↻  刷新", "刷新今日看点"); refreshButton.Padding = new Thickness(14, 9, 14, 9);
            refreshButton.Click += delegate { RefreshAsync(); }; controls.Children.Add(refreshButton);
            Button close = ActionButton("×", "关闭今日看点"); close.FontSize = 21; close.Padding = new Thickness(0); close.Width = 36; close.Height = 36;
            close.Margin = new Thickness(9, 0, 0, 0); Theme(close, Control.BackgroundProperty, "WindowSurface"); close.Click += delegate { Close(); }; controls.Children.Add(close);
            System.Windows.Controls.Primitives.UniformGrid navigation = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Margin = new Thickness(21, 5, 21, 8) };
            foreach (string source in HighlightsLayout.Sources) {
                string selected = source;
                Button link = ActionButton(source, "跳转到 " + source); link.FontSize = 11.5; link.Padding = new Thickness(4, 7, 4, 7); link.Margin = new Thickness(3);
                link.Click += delegate { NavigateSource(selected); };
                sourceNavigation[source] = link; navigation.Children.Add(link);
            }
            Grid.SetRow(navigation, 1); layout.Children.Add(navigation);
            contentPanel = new StackPanel { Margin = new Thickness(25, 22, 25, 20) };
            scroll = new ScrollViewer { Content = contentPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, AllowDrop = true, PanningMode = PanningMode.VerticalOnly };
            scroll.PreviewDragOver += OnSectionDragOver; scroll.PreviewDrop += OnSectionDrop;
            scroll.DragLeave += delegate(object sender, DragEventArgs e) {
                Point point = e.GetPosition(scroll);
                if (point.X < 0 || point.X > scroll.ActualWidth || point.Y < 0 || point.Y > scroll.ActualHeight) { dragScrollStep = 0; SetDropMarker(-1); }
            };
            Grid.SetRow(scroll, 2); layout.Children.Add(scroll);
            Border footer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(26, 9, 26, 9) };
            Theme(footer, Border.BorderBrushProperty, "Outline");
            Grid footerGrid = new Grid(); footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusDot = new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            Theme(statusDot, Border.BackgroundProperty, "Accent"); footerGrid.Children.Add(statusDot);
            statusText = Label(String.Empty, 11.5, "SecondaryText"); statusText.TextWrapping = TextWrapping.Wrap; statusText.MaxHeight = 52; statusText.TextTrimming = TextTrimming.CharacterEllipsis; statusText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(statusText, 1); footerGrid.Children.Add(statusText); footer.Child = footerGrid; Grid.SetRow(footer, 3); layout.Children.Add(footer);
            ApplyCharacterTheme(character, portrait); RenderLoading(); SetStatus("准备整理今日内容", false);
            Loaded += delegate {
                HighlightsResult cached = DailyHighlightsService.LoadToday();
                if (cached != null) Render(cached);
                if (cached == null || !DailyHighlightsService.HasFreshCache(0.5)) RefreshAsync();
            };
            Closed += delegate { dragScrollTimer.Stop(); };
        }

        public void ApplyCharacterTheme(string character) { ApplyCharacterTheme(character, null); }
        public void ApplyCharacterTheme(string character, BitmapSource portrait)
        {
            PetWindow.MenuPalette palette = PetWindow.MenuPalette.ForCharacter(character);
            Resources["WindowSurface"] = ColorBrush(palette.Surface);
            Resources["CardSurface"] = Blend(palette.Surface, palette.Text, character == "ganyu" ? 0.015 : 0.045);
            Resources["SelectedSurface"] = ColorBrush(palette.Checked);
            Resources["PrimaryText"] = ColorBrush(palette.Text); Resources["SecondaryText"] = ColorBrush(palette.Muted);
            Resources["Accent"] = ColorBrush(palette.Accent); Resources["Outline"] = Blend(palette.Surface, palette.Border, 0.48);
            Resources["HoverSurface"] = ColorBrush(palette.Hover); Resources["DisabledText"] = ColorBrush(palette.Disabled);
            Resources["Attention"] = ColorBrush(palette.Exit);
            Resources["HeaderSurface"] = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(palette.Checked), (Color)ColorConverter.ConvertFromString(palette.Surface), 14);
            string name = character == "hutao" ? "胡桃" : character == "ganyu" ? "甘雨" : character == "varesa" ? "瓦雷莎" : "珂洛";
            companionText.Text = name + "的资讯小站  ·  陪你看看新鲜事";
            characterMark.Text = character == "hutao" ? "✿" : character == "ganyu" ? "❄" : character == "varesa" ? "ϟ" : "✦";
            portraitImage.Source = portrait; portraitImage.Visibility = portrait == null ? Visibility.Collapsed : Visibility.Visible;
            characterMark.Visibility = portrait == null ? Visibility.Visible : Visibility.Collapsed;
            // Dynamic resources retain the reader's scroll position, collapse state and in-progress drag.
        }

        public void RefreshNow() { RefreshAsync(); }
        private async void RefreshAsync()
        {
            if (refreshing) return;
            refreshing = true; refreshButton.IsEnabled = false; refreshButton.Content = "整理中…";
            SetStatus("正在更新 10 个版块：热搜、GitHub、AI、股市与生活资讯…", false);
            if (currentResult == null) RenderLoading();
            try {
                HighlightsResult result = await Task.Factory.StartNew<HighlightsResult>(delegate { return DailyHighlightsService.FetchAndSave(); }); Render(result);
            } catch {
                if (currentResult == null) RenderEmpty("这次没有连接上", "稍后点击刷新，继续看看今天的新鲜事。");
                SetStatus(currentResult == null ? "更新失败，请稍后重试" : "更新失败，正在显示上次获取的内容", true);
            } finally { refreshing = false; refreshButton.IsEnabled = true; refreshButton.Content = "↻  刷新"; }
        }
        private void SetStatus(string text, bool needsAttention)
        {
            statusText.Text = text;
            statusText.ToolTip = text;
            statusDot.Opacity = needsAttention ? 1 : 0.65;
            Theme(statusText, TextBlock.ForegroundProperty, needsAttention ? "Attention" : "SecondaryText");
            Theme(statusDot, Border.BackgroundProperty, needsAttention ? "Attention" : "Accent");
        }
        private void RenderLoading() { RenderEmpty("正在收集今日新鲜事", "热搜与视频  ·  GitHub / AI  ·  股市 / 汽车  ·  游戏 / 时尚  ·  体育 / 虎扑"); }
        private void RenderEmpty(string title, string detail)
        {
            contentPanel.Children.Clear(); sectionBodies.Clear(); sectionHeaders.Clear(); dropMarkers.Clear();
            Border card = Surface(new Thickness(26, 33, 26, 33)); card.Margin = new Thickness(0, 15, 0, 0);
            StackPanel inner = new StackPanel();
            TextBlock mark = Label("✦", 32, "Accent"); mark.HorizontalAlignment = HorizontalAlignment.Center; inner.Children.Add(mark);
            TextBlock heading = Label(title, 18, "PrimaryText"); heading.FontWeight = FontWeights.SemiBold; heading.TextAlignment = TextAlignment.Center; heading.Margin = new Thickness(0, 15, 0, 8); inner.Children.Add(heading);
            TextBlock message = Label(detail, 13, "SecondaryText"); message.TextAlignment = TextAlignment.Center; message.TextWrapping = TextWrapping.Wrap; inner.Children.Add(message);
            card.Child = inner; contentPanel.Children.Add(card);
        }

        private void Render(HighlightsResult result)
        {
            if (draggingSource != null) { deferredResult = result; return; }
            currentResult = result; sectionBodies.Clear(); sectionHeaders.Clear(); dropMarkers.Clear(); contentPanel.Children.Clear();
            DateTime day;
            string dayText = DateTime.TryParseExact(result.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day) ? day.ToString("M月d日  dddd", CultureInfo.GetCultureInfo("zh-CN")) : result.Day;
            Grid dateRow = new Grid { Margin = new Thickness(2, 0, 2, 14) };
            dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dateRow.Children.Add(Label(dayText, 13, "SecondaryText"));
            TextBlock count = Label(result.Items.Count + " 条内容  /  " + HighlightsLayout.Sources.Length + " 个版块", 12, "SecondaryText"); Grid.SetColumn(count, 1); dateRow.Children.Add(count); contentPanel.Children.Add(dateRow);
            Border overview = Surface(new Thickness(18, 16, 18, 16)); Theme(overview, Border.BackgroundProperty, "SelectedSurface");
            StackPanel overviewBody = new StackPanel();
            TextBlock overviewTitle = Label("✦  今日速览", 13, "Accent"); overviewTitle.FontWeight = FontWeights.SemiBold; overviewBody.Children.Add(overviewTitle);
            TextBlock overviewText = Label(String.IsNullOrWhiteSpace(result.Overview) ? "今天的看点都在这里，展开下方版块慢慢读。" : result.Overview, 14, "PrimaryText");
            overviewText.TextWrapping = TextWrapping.Wrap; overviewText.LineHeight = 24; overviewText.MaxHeight = 72; overviewText.TextTrimming = TextTrimming.CharacterEllipsis; overviewText.ToolTip = result.Overview; overviewText.Margin = new Thickness(0, 8, 0, 0);
            overviewBody.Children.Add(overviewText); overview.Child = overviewBody; contentPanel.Children.Add(overview);
            TextBlock hint = Label("点击版块收起内容  ·  拖动左侧手柄调整顺序", 11.5, "SecondaryText"); hint.Margin = new Thickness(2, 19, 0, 7); contentPanel.Children.Add(hint);
            for (int sourceIndex = 0; sourceIndex < sectionLayout.Order.Count; sourceIndex++) {
                string source = sectionLayout.Order[sourceIndex]; List<HighlightItem> section = new List<HighlightItem>();
                for (int i = 0; i < result.Items.Count; i++) if (result.Items[i].Source == source) section.Add(result.Items[i]);
                sourceNavigation[source].Content = source + " · " + section.Count;
                HighlightSourceState sourceState = FindSourceState(source);
                sourceNavigation[source].ToolTip = source + " · " + SourceStatus(sourceState);
                AddDropMarker(); contentPanel.Children.Add(CreateSection(source, section));
            }
            AddDropMarker();
            string errorText = result.Errors != null && result.Errors.Count > 0 ? "  ·  部分来源未更新：" + String.Join("；", result.Errors.ToArray()) : String.Empty;
            SetStatus("更新于 " + result.UpdatedAt + errorText, !String.IsNullOrEmpty(errorText));
        }

        private HighlightSourceState FindSourceState(string source)
        {
            return currentResult == null || currentResult.Sources == null ? null : currentResult.Sources.Find(delegate(HighlightSourceState item) { return item.Name == source; });
        }
        private static string SourceStatus(HighlightSourceState state)
        {
            if (state == null) return "等待更新";
            string details = String.IsNullOrEmpty(state.Provider) ? String.Empty : state.Provider + " · ";
            details += String.IsNullOrEmpty(state.UpdatedAt) ? "暂无可用内容" : (state.IsCached ? "缓存于 " : "更新于 ") + state.UpdatedAt;
            return details + (String.IsNullOrEmpty(state.Error) ? String.Empty : " · " + state.Error);
        }
        private void NavigateSource(string source)
        {
            if (draggingSource != null || !sectionHeaders.ContainsKey(source)) return;
            if (sectionLayout.Collapsed.Contains(source)) ((Button)((Grid)sectionHeaders[source]).Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            scroll.UpdateLayout();
            FrameworkElement heading = sectionHeaders[source];
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + heading.TranslatePoint(new Point(), scroll).Y);
        }

        private UIElement CreateSection(string source, List<HighlightItem> items)
        {
            StackPanel section = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            Grid heading = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            section.Children.Add(heading); sectionHeaders[source] = heading;
            Button grip = ActionButton("⠿", "拖动排序：" + source); grip.FontFamily = new FontFamily("Segoe UI Symbol"); grip.FontSize = 19;
            grip.Background = Brushes.Transparent; grip.BorderThickness = new Thickness(0); grip.Padding = new Thickness(0); grip.Margin = new Thickness(0, 0, 5, 0);
            grip.Cursor = Cursors.SizeNS; grip.ToolTip = "按住拖动排序；也可按 Alt + ↑ / ↓ 调整"; grip.Height = 46; grip.Tag = source; Theme(grip, Control.ForegroundProperty, "SecondaryText");
            Point start = new Point();
            bool pressed = false;
            grip.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                start = e.GetPosition(this);
                pressed = true;
                grip.Focus();
                grip.CaptureMouse();
                e.Handled = true;
            };
            grip.PreviewMouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e) {
                pressed = false;
                grip.ReleaseMouseCapture();
                e.Handled = true;
            };
            grip.LostMouseCapture += delegate { pressed = false; };
            grip.PreviewMouseMove += delegate(object sender, MouseEventArgs e) {
                if (!pressed || e.LeftButton != MouseButtonState.Pressed || draggingSource != null) return;
                Point now = e.GetPosition(this);
                if (Math.Abs(now.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance &&
                    Math.Abs(now.X - start.X) < SystemParameters.MinimumHorizontalDragDistance) return;
                pressed = false;
                grip.ReleaseMouseCapture();
                BeginSectionDrag(grip, source);
                e.Handled = true;
            };
            grip.PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == 0 || (key != Key.Up && key != Key.Down)) return;
                int index = sectionLayout.Order.IndexOf(source);
                int target = index + (key == Key.Up ? -1 : 1);
                if (target >= 0 && target < sectionLayout.Order.Count)
                {
                    string before = key == Key.Up ? sectionLayout.Order[target]
                        : (target + 1 < sectionLayout.Order.Count ? sectionLayout.Order[target + 1] : null);
                    if (sectionLayout.MoveBefore(source, before))
                    {
                        PetSettings.SaveHighlightsLayout(sectionLayout);
                        Render(currentResult);
                        sectionHeaders[source].BringIntoView();
                        ((Grid)sectionHeaders[source]).Children[0].Focus();
                    }
                }
                e.Handled = true;
            };

            heading.Children.Add(grip);
            bool collapsed = sectionLayout.Collapsed.Contains(source);
            Button toggle = ActionButton(String.Empty, source + (collapsed ? "，展开" : "，收起")); toggle.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            toggle.Padding = new Thickness(13, 11, 13, 11); toggle.ToolTip = "点击展开或收起此版块"; Grid.SetColumn(toggle, 1); heading.Children.Add(toggle);
            Grid caption = new Grid(); caption.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) }); caption.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            string mark = source == "微博热搜" ? "热" : source == "B站热门" ? "B" : source == "体育" ? "赛" : source == "NBA" ? "球" : source == "GitHub" ? "Git" : source == "AI" ? "AI" : source == "股票" ? "股" : source == "汽车" ? "车" : source == "游戏" ? "游" : "美";
            Border badge = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left }; Theme(badge, Border.BackgroundProperty, "SelectedSurface");
            TextBlock badgeText = Label(mark, 11, "Accent"); badgeText.FontWeight = FontWeights.SemiBold; badgeText.HorizontalAlignment = HorizontalAlignment.Center; badgeText.VerticalAlignment = VerticalAlignment.Center; badge.Child = badgeText; caption.Children.Add(badge);
            StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock title = Label(source == "NBA" ? "NBA · 虎扑" : source == "GitHub" ? "GitHub · 热门 Fork" : source, 15, "PrimaryText"); title.FontWeight = FontWeights.SemiBold; titleRow.Children.Add(title);
            TextBlock count = Label(items.Count + " 条", 11.5, "SecondaryText"); count.Margin = new Thickness(11, 0, 0, 0); count.VerticalAlignment = VerticalAlignment.Center; titleRow.Children.Add(count); Grid.SetColumn(titleRow, 1); caption.Children.Add(titleRow);
            TextBlock state = Label(collapsed ? "展开  ›" : "收起  ⌄", 11.5, "SecondaryText"); state.VerticalAlignment = VerticalAlignment.Center; state.Margin = new Thickness(12, 0, 0, 0); Grid.SetColumn(state, 2); caption.Children.Add(state); toggle.Content = caption;
            StackPanel body = new StackPanel { Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible }; sectionBodies[source] = body;
            HighlightSourceState sourceState = FindSourceState(source);
            if (sourceState != null) {
                TextBlock freshness = Label(SourceStatus(sourceState), 11, sourceState.IsCached || !String.IsNullOrEmpty(sourceState.Error) ? "Attention" : "SecondaryText");
                freshness.TextWrapping = TextWrapping.Wrap; freshness.Margin = new Thickness(4, 0, 4, 10); body.Children.Add(freshness);
            }
            if (source == "GitHub") {
                TextBlock description = Label("近 90 天创建的公开原创项目，按累计 Fork 排序；非当日增量榜。", 11.5, "SecondaryText");
                description.TextWrapping = TextWrapping.Wrap; description.Margin = new Thickness(4, 0, 4, 10); body.Children.Add(description);
            }
            for (int i = 0; i < items.Count; i++) body.Children.Add(CreateCard(items[i], i + 1));
            if (items.Count == 0) {
                Border empty = Surface(new Thickness(19, 18, 19, 18)); TextBlock message = Label(sourceState == null || String.IsNullOrEmpty(sourceState.Error) ? "还没有获取到内容，稍后刷新再看看。" : sourceState.Error + "。暂无缓存，稍后刷新再看看。", 13, "SecondaryText"); message.TextWrapping = TextWrapping.Wrap; empty.Child = message; body.Children.Add(empty);
            }
            section.Children.Add(body);
            toggle.Click += delegate {
                bool hide = !sectionLayout.Collapsed.Contains(source);
                if (hide) sectionLayout.Collapsed.Add(source); else sectionLayout.Collapsed.Remove(source);
                body.Visibility = hide ? Visibility.Collapsed : Visibility.Visible; state.Text = hide ? "展开  ›" : "收起  ⌄";
                System.Windows.Automation.AutomationProperties.SetName(toggle, source + (hide ? "，展开" : "，收起")); PetSettings.SaveHighlightsLayout(sectionLayout);
            };
            return section;
        }
        private void AddDropMarker()
        {
            Border marker = new Border { Height = 3, Margin = new Thickness(1, 2, 1, 2), CornerRadius = new CornerRadius(2), Opacity = 0, IsHitTestVisible = false };
            Theme(marker, Border.BackgroundProperty, "Accent"); dropMarkers.Add(marker); contentPanel.Children.Add(marker);
        }
        private void BeginSectionDrag(Button grip, string source)
        {
            double previousOffset = scroll.VerticalOffset;
            draggingSource = source;
            dropCompleted = false;
            foreach (FrameworkElement body in sectionBodies.Values) body.Visibility = Visibility.Collapsed;
            sectionHeaders[source].Opacity = 0.45;
            SetStatus("将版块拖到插入线处松开 · 按 Esc 取消", false);
            scroll.UpdateLayout();
            dragScrollTimer.Start();
            try
            {
                DragDrop.DoDragDrop(grip, new DataObject(SectionDragFormat, source), DragDropEffects.Move);
            }
            finally
            {
                dragScrollTimer.Stop();
                dragScrollStep = 0;
                draggingSource = null;
                SetDropMarker(-1);
                HighlightsResult next = deferredResult ?? currentResult;
                deferredResult = null;
                Render(next);
                scroll.UpdateLayout();
                if (dropCompleted) sectionHeaders[source].BringIntoView();
                else scroll.ScrollToVerticalOffset(previousOffset);
            }
        }

        private bool IsSectionDrag(DragEventArgs e)
        {
            return draggingSource != null && e.Data.GetDataPresent(SectionDragFormat) &&
                String.Equals(e.Data.GetData(SectionDragFormat) as string, draggingSource, StringComparison.Ordinal);
        }

        private void OnSectionDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;
            if (!IsSectionDrag(e)) return;
            e.Effects = DragDropEffects.Move;
            Point point = e.GetPosition(scroll);
            dragPointer = point;
            dragScrollStep = point.Y < 48 ? -16 : (point.Y > scroll.ActualHeight - 48 ? 16 : 0);
            UpdateDropMarker(point);
        }

        private void UpdateDropMarker(Point point)
        {
            int index = sectionLayout.Order.Count;
            for (int i = 0; i < sectionLayout.Order.Count; i++)
            {
                FrameworkElement heading = sectionHeaders[sectionLayout.Order[i]];
                Point top = heading.TranslatePoint(new Point(0, 0), scroll);
                if (point.Y < top.Y + heading.ActualHeight / 2) { index = i; break; }
            }
            SetDropMarker(index);
        }

        private void SetDropMarker(int index)
        {
            dropIndex = index;
            for (int i = 0; i < dropMarkers.Count; i++) dropMarkers[i].Opacity = i == index ? 1 : 0;
        }

        private void OnSectionDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;
            if (!IsSectionDrag(e)) return;
            UpdateDropMarker(e.GetPosition(scroll));
            string before = dropIndex >= sectionLayout.Order.Count ? null : sectionLayout.Order[dropIndex];
            if (sectionLayout.MoveBefore(draggingSource, before)) PetSettings.SaveHighlightsLayout(sectionLayout);
            dropCompleted = true;
            e.Effects = DragDropEffects.Move;
        }


        private UIElement CreateCard(HighlightItem item, int rank)
        {
            Border card = Surface(new Thickness(15, 14, 15, 14)); card.Margin = new Thickness(0, 0, 0, 8);
            Grid body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(37) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) }); card.Child = body;
            Border rankBadge = new Border { Width = 25, Height = 25, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }; Theme(rankBadge, Border.BackgroundProperty, "SelectedSurface");
            TextBlock rankText = Label(rank.ToString("00", CultureInfo.InvariantCulture), 11.5, rank <= 3 ? "Accent" : "SecondaryText"); rankText.FontWeight = FontWeights.SemiBold; rankText.HorizontalAlignment = HorizontalAlignment.Center; rankText.VerticalAlignment = VerticalAlignment.Center; rankBadge.Child = rankText; body.Children.Add(rankBadge);
            StackPanel text = new StackPanel { Margin = new Thickness(0, 1, 10, 0) }; Grid.SetColumn(text, 1); body.Children.Add(text);
            TextBlock title = Label(item.Title, 14.5, "PrimaryText"); title.FontWeight = FontWeights.SemiBold; title.LineHeight = 23; title.TextWrapping = TextWrapping.Wrap; text.Children.Add(title);
            if (!String.IsNullOrWhiteSpace(item.Summary)) {
                TextBlock summary = Label(item.Summary, 12, "SecondaryText"); summary.TextWrapping = TextWrapping.Wrap; summary.LineHeight = 19; summary.MaxHeight = 38; summary.TextTrimming = TextTrimming.CharacterEllipsis; summary.ToolTip = item.Summary; summary.Margin = new Thickness(0, 5, 0, 0); text.Children.Add(summary);
            }
            if (!String.IsNullOrWhiteSpace(item.Hot)) { TextBlock hot = Label(item.Hot, 11.5, "Accent"); hot.TextWrapping = TextWrapping.Wrap; hot.Margin = new Thickness(0, 6, 0, 0); text.Children.Add(hot); }
            Button open = ActionButton("↗", "阅读原文：" + item.Title); open.FontFamily = new FontFamily("Segoe UI Symbol"); open.FontSize = 17; open.Width = 32; open.Height = 32; open.Padding = new Thickness(0); open.VerticalAlignment = VerticalAlignment.Center; open.HorizontalAlignment = HorizontalAlignment.Right; open.ToolTip = "在浏览器阅读原文"; open.Tag = item.Url;
            Theme(open, Control.BackgroundProperty, "SelectedSurface"); Theme(open, Control.ForegroundProperty, "Accent"); open.Click += delegate(object sender, RoutedEventArgs e) { OpenUrl(Convert.ToString(((Button)sender).Tag)); }; Grid.SetColumn(open, 2); body.Children.Add(open); return card;
        }
        private void OpenUrl(string url)
        {
            try { string safe = DailyHighlightsService.SafeHttpsUrl(url); if (safe == null) return; Process.Start(new ProcessStartInfo(safe) { UseShellExecute = true }); }
            catch { SetStatus("网页没有打开，请稍后重试", true); }
        }
        private static void Theme(FrameworkElement element, DependencyProperty property, string key) { element.SetResourceReference(property, key); }
        private static TextBlock Label(string text, double size, string brushKey) { TextBlock value = new TextBlock { Text = text ?? String.Empty, FontSize = size }; Theme(value, TextBlock.ForegroundProperty, brushKey); return value; }
        private static Border Surface(Thickness padding) { Border value = new Border { CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1), Padding = padding }; Theme(value, Border.BackgroundProperty, "CardSurface"); Theme(value, Border.BorderBrushProperty, "Outline"); return value; }
        private static Button ActionButton(string text, string accessibleName) { Button value = new Button { Content = text, FontSize = 12, Padding = new Thickness(10, 7, 10, 7), Cursor = Cursors.Hand }; System.Windows.Automation.AutomationProperties.SetName(value, accessibleName); return value; }
        private void ConfigureStyles()
        {
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type Button}'>
 <Setter Property='Foreground' Value='{DynamicResource PrimaryText}'/><Setter Property='Background' Value='{DynamicResource CardSurface}'/>
 <Setter Property='BorderBrush' Value='{DynamicResource Outline}'/><Setter Property='BorderThickness' Value='1'/>
 <Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='HorizontalContentAlignment' Value='Center'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
  <Border x:Name='Face' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='11' Padding='{TemplateBinding Padding}'>
   <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' RecognizesAccessKey='True'/>
  </Border>
  <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource HoverSurface}'/></Trigger>
   <Trigger Property='IsPressed' Value='True'><Setter TargetName='Face' Property='Opacity' Value='.75'/></Trigger>
   <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Face' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter Property='Foreground' Value='{DynamicResource DisabledText}'/><Setter TargetName='Face' Property='Opacity' Value='.65'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='ScrollPage' TargetType='{x:Type RepeatButton}'>
 <Setter Property='Focusable' Value='False'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='{x:Type ScrollBar}'>
 <Setter Property='Width' Value='10'/><Setter Property='Background' Value='Transparent'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
  <Grid Background='Transparent' Margin='0,8,3,8'>
   <Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'>
    <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Style='{StaticResource ScrollPage}'/></Track.DecreaseRepeatButton>
    <Track.Thumb><Thumb MinHeight='28'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'>
     <Border x:Name='ThumbFace' Background='{DynamicResource Outline}' CornerRadius='4' Margin='1,0'/>
     <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='ThumbFace' Property='Background' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='IsDragging' Value='True'><Setter TargetName='ThumbFace' Property='Background' Value='{DynamicResource Accent}'/></Trigger></ControlTemplate.Triggers>
    </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
    <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Style='{StaticResource ScrollPage}'/></Track.IncreaseRepeatButton>
   </Track>
  </Grid>
 </ControlTemplate></Setter.Value></Setter>
</Style>
</ResourceDictionary>";
            Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(xaml));
        }
        private static SolidColorBrush Blend(string from, string to, double amount)
        {
            Color a = (Color)ColorConverter.ConvertFromString(from), b = (Color)ColorConverter.ConvertFromString(to);
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb((byte)(a.R + (b.R - a.R) * amount), (byte)(a.G + (b.G - a.G) * amount), (byte)(a.B + (b.B - a.B) * amount))); brush.Freeze(); return brush;
        }
        private static SolidColorBrush ColorBrush(string value) { SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush; }
    }
}
