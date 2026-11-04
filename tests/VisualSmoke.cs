using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using ClaudePet;

// Test launcher: exposes the normally taskbar-hidden pet to window automation.
// Production layout, input handlers and rendering are used without modification.
internal static class VisualSmoke
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        if (args.Length > 0 && args[0] == "--verify") { VerifyHighlights(); return; }
        if (args.Length > 0 && args[0] == "--live-preview") { RenderLiveHighlights(); return; }
        if (args.Length > 0 && args[0] == "--highlights") { app.Run(new HighlightsWindow()); return; }
        PetWindow pet = new PetWindow { ShowInTaskbar = true };
        Type type = typeof(PetWindow);
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        type.GetField("autoWalk", flags).SetValue(pet, false);
        type.GetField("quietMode", flags).SetValue(pet, true);
        pet.Loaded += delegate {
            pet.Left = 900; pet.Top = 400;
            type.GetField("bubbleEnds", flags).SetValue(pet, DateTime.MaxValue);
        };
        app.Run(pet);
    }

    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Private).GetValue(owner); }
    private static object Call(object owner, string name, params object[] args) { return owner.GetType().GetMethod(name, Private | BindingFlags.DeclaredOnly).Invoke(owner, args); }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Layout(HighlightsWindow window)
    {
        WpfTestRender.Drain();
        FrameworkElement root = (FrameworkElement)window.Content;
        root.Measure(new Size(window.Width, window.Height)); root.Arrange(new Rect(0, 0, window.Width, window.Height)); root.UpdateLayout();
        WpfTestRender.Drain();
    }
    private static void RenderPreview(HighlightsWindow window, string path)
    {
        Layout(window);
        RenderTargetBitmap bitmap = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
        WpfTestRender.Render(bitmap, (Visual)window.Content);
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream file = File.Create(path)) encoder.Save(file);
    }

    private static void RenderLiveHighlights()
    {
        // Opt-in, read-only network preview. Never call FetchAndSave or show the window,
        // so neither the user's news cache nor Loaded refresh handlers are touched.
        System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072;
        System.Net.ServicePointManager.DefaultConnectionLimit = Math.Max(12, System.Net.ServicePointManager.DefaultConnectionLimit);
        List<Task<SourceResult>> tasks = new List<Task<SourceResult>>();
        foreach (string name in HighlightsLayout.Sources) {
            string selected = name;
            tasks.Add(Task.Factory.StartNew(delegate { return DailyHighlightsService.FetchSource(selected); }));
        }
        Task.WaitAll(tasks.ToArray(), 22000);
        List<SourceResult> fetched = new List<SourceResult>();
        for (int i = 0; i < tasks.Count; i++) fetched.Add(tasks[i].Status == TaskStatus.RanToCompletion ? tasks[i].Result :
            new SourceResult { Name = HighlightsLayout.Sources[i], Error = "连接超时，请稍后刷新" });
        HighlightsResult result = DailyHighlightsService.MergeResults(fetched, null, DateTime.Now);
        string previews = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "v23"));
        Directory.CreateDirectory(previews);
        File.WriteAllText(Path.Combine(previews, "highlights-live-data.json"), new JavaScriptSerializer().Serialize(result));
        HighlightsWindow window = new HighlightsWindow("ganyu", PetAssets.Load("ganyu")["idle"][0]);
        try {
            // Modify only this unshown window's in-memory layout. Navigation does not
            // persist anything because all sections are already expanded.
            HighlightsLayout local = Field<HighlightsLayout>(window, "sectionLayout");
            local.Order = new List<string>(HighlightsLayout.Sources); local.Collapsed.Clear();
            window.Width = 780; window.Height = 760;
            Call(window, "Render", result); Layout(window);
            Field<ScrollViewer>(window, "scroll").ScrollToTop();
            RenderPreview(window, Path.Combine(previews, "highlights-live-overview.png"));
            Call(window, "NavigateSource", "GitHub"); Layout(window);
            RenderPreview(window, Path.Combine(previews, "highlights-live-github.png"));
        } finally { window.Close(); }
        foreach (SourceResult source in fetched)
            Console.WriteLine("LIVE PREVIEW " + source.Name + " | " + source.Items.Count + " items | " + (source.Error ?? "OK"));
        Console.WriteLine("Saved real-source overview and GitHub previews without writing user cache or layout.");
    }

    private static void VerifyHighlights()
    {
        string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet", "highlights-layout.json");
        byte[] original = File.Exists(settings) ? File.ReadAllBytes(settings) : null;
        string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "visual-smoke-results.txt");
        HighlightsWindow window = null, reopened = null;
        try
        {
            PetSettings.SaveHighlightsLayout(new HighlightsLayout());
            HighlightsResult result = new HighlightsResult { Day = "2026-10-23", UpdatedAt = "2026-10-23 12:00:00", Overview = "测试数据：检查版块折叠、排序和重新打开后的布局。" };
            foreach (string source in HighlightsLayout.Sources)
                for (int i = 0; i < 3; i++) result.Items.Add(new HighlightItem { Source = source, Title = source + " 测试消息 " + (i + 1), Summary = "用于布局验证的本地测试数据", Url = "https://example.invalid" });
            window = new HighlightsWindow(); Call(window, "Render", result); Layout(window);
            Dictionary<string, FrameworkElement> heads = Field<Dictionary<string, FrameworkElement>>(window, "sectionHeaders");
            Dictionary<string, FrameworkElement> bodies = Field<Dictionary<string, FrameworkElement>>(window, "sectionBodies");
            foreach (string source in HighlightsLayout.Sources)
            {
                Button toggle = (Button)((Grid)heads[source]).Children[1];
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(bodies[source].Visibility == Visibility.Collapsed, "Collapse failed: " + source);
                Require(PetSettings.LoadHighlightsLayout().Collapsed.Contains(source), "Collapse not saved: " + source);
            }
            Dictionary<string, Button> navigation = Field<Dictionary<string, Button>>(window, "sourceNavigation");
            Require(navigation.Count == HighlightsLayout.Sources.Length, "Every section must have a fixed navigation button");
            navigation["AI"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window);
            Require(bodies["AI"].Visibility == Visibility.Visible && !PetSettings.LoadHighlightsLayout().Collapsed.Contains("AI"), "Navigation must expand a collapsed section");
            ((Button)((Grid)heads["AI"]).Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Field<ScrollViewer>(window, "scroll").ScrollToTop();
            Layout(window);
            ScrollViewer scroll = Field<ScrollViewer>(window, "scroll");
            for (int i = 0; i < HighlightsLayout.Sources.Length; i++)
            {
                FrameworkElement heading = heads[HighlightsLayout.Sources[i]];
                Point top = heading.TranslatePoint(new Point(0, 0), scroll);
                Call(window, "UpdateDropMarker", new Point(top.X, top.Y + 1));
                Require(Field<int>(window, "dropIndex") == i, "Wrong insertion target before section " + i);
            }
            Call(window, "UpdateDropMarker", new Point(0, 2000));
            Require(Field<int>(window, "dropIndex") == HighlightsLayout.Sources.Length, "Bottom insertion target missing");
            // Exercise the same WPF drop handler with a local drag data object.
            typeof(HighlightsWindow).GetField("draggingSource", Private).SetValue(window, "NBA");
            ConstructorInfo ctor = typeof(DragEventArgs).GetConstructor(Private, null,
                new Type[] { typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point) }, null);
            Require(ctor != null, "WPF DragEventArgs constructor unavailable");
            Point first = heads["微博热搜"].TranslatePoint(new Point(0, 1), scroll);
            DragEventArgs drop = (DragEventArgs)ctor.Invoke(new object[] { new DataObject("KoloPet.HighlightsSection", "NBA"), DragDropKeyStates.None, DragDropEffects.Move, scroll, first });
            drop.RoutedEvent = DragDrop.DropEvent;
            Call(window, "OnSectionDrop", scroll, drop);
            Require(drop.Handled && drop.Effects == DragDropEffects.Move, "Drop rejected");
            Require(PetSettings.LoadHighlightsLayout().Order[0] == "NBA", "Drop did not move NBA to top");
            typeof(HighlightsWindow).GetField("draggingSource", Private).SetValue(window, null);
            Call(window, "Render", result);
            // Refresh must preserve order and collapsed state; reopening loads the saved settings.
            reopened = new HighlightsWindow(); Call(reopened, "Render", result); Layout(reopened);
            Require(Field<HighlightsLayout>(reopened, "sectionLayout").Order[0] == "NBA", "Reopen lost order");
            foreach (FrameworkElement body in Field<Dictionary<string, FrameworkElement>>(reopened, "sectionBodies").Values)
                Require(body.Visibility == Visibility.Collapsed, "Reopen lost collapsed state");
            RenderPreview(reopened, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "highlights-collapsed-preview.png"));
            heads = Field<Dictionary<string, FrameworkElement>>(reopened, "sectionHeaders");
            ((Button)((Grid)heads["NBA"]).Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!PetSettings.LoadHighlightsLayout().Collapsed.Contains("NBA"), "Expansion not saved");
            RenderPreview(reopened, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "highlights-expanded-preview.png"));
            VerifyThemes();
            const string report = "PASS: all ten collapse buttons; fixed category navigation and expansion; saved collapse; insertion positions; WPF drop handler NBA to top; refresh/reopen persistence; expansion; four character themes and cached portraits; text contrast; live theme switching retains scroll/content/collapse/deferred refresh; 620x480 long-title/error layout; loading and empty states; rendered previews.\nWPF event and layout verification with offline fixtures; this test does not simulate physical mouse dragging or perform network requests.\n";
            File.WriteAllText(log, report);
            Console.WriteLine(report);
        }
        catch (Exception e) { File.WriteAllText(log, "FAIL: " + e); Console.Error.WriteLine(e); Environment.ExitCode = 1; }
        finally
        {
            if (window != null) window.Close(); if (reopened != null) reopened.Close();
            if (original == null) { if (File.Exists(settings)) File.Delete(settings); }
            else File.WriteAllBytes(settings, original);
        }
    }

    private static HighlightsResult ThemeFixture()
    {
        HighlightsResult result = new HighlightsResult {
            Day = "2026-10-26", UpdatedAt = "2026-10-26 15:30:00",
            Overview = "离线预览示例：热搜、GitHub 热门 Fork、AI 与生活资讯。点击上方分类快速跳转，喜欢的版块可以拖到前面，读完也可以收起。"
        };
        string[] titles = {
            "离线示例 · 收藏一份秋日出游清单，给周末留一点期待",
            "离线示例 · 当熟悉的旋律响起，这支舞台片段让人想再看一遍",
            "离线示例 · 新赛季的精彩瞬间与比赛回顾"
        };
        foreach (string source in HighlightsLayout.Sources)
        {
            result.Sources.Add(new HighlightSourceState { Name = source, Provider = source == "GitHub" ? "GitHub API · 近90天新项目 / 累计 Fork" : source == "NBA" ? "虎扑 NBA" : "Bing 新闻", UpdatedAt = "2026-10-27 09:00:00" });
            for (int i = 0; i < titles.Length; i++)
                result.Items.Add(new HighlightItem { Source = source, Title = source == "GitHub" ? "example/project-" + (i + 1) : source == "AI" ? "离线示例 · 开源模型与人工智能应用的今日动态" : titles[i], Summary = source == "NBA" ? "虎扑 · 赛后讨论与球员动态" : "本地测试内容，仅用于检查排版与主题。", Hot = source == "GitHub" ? "Fork 1200  ·  Star 1.5万  ·  TypeScript" : i == 0 ? "关注度 12.8万" : "持续更新", Url = "https://example.invalid" });
        }
        return result;
    }

    private static void VerifyThemes()
    {
        string previews = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "v23"));
        Directory.CreateDirectory(previews);
        PetSettings.SaveHighlightsLayout(new HighlightsLayout());
        HighlightsResult result = ThemeFixture();
        HighlightsWindow window = new HighlightsWindow("kolo", PetAssets.Load("kolo")["idle"][0]);
        try
        {
            Call(window, "Render", result); Layout(window);
            ScrollViewer scroll = Field<ScrollViewer>(window, "scroll");
            Dictionary<string, FrameworkElement> bodies = Field<Dictionary<string, FrameworkElement>>(window, "sectionBodies");
            FrameworkElement originalBody = bodies["微博热搜"];
            foreach (string character in new string[] { "kolo", "hutao", "ganyu", "varesa" })
            {
                BitmapSource portrait = PetAssets.Load(character)["idle"][0];
                scroll.ScrollToVerticalOffset(138); Layout(window);
                double previousOffset = scroll.VerticalOffset;
                window.ApplyCharacterTheme(character, portrait); Layout(window);
                Require(Object.ReferenceEquals(originalBody, bodies["微博热搜"]), "Theme rebuilt the content tree: " + character);
                Require(Math.Abs(scroll.VerticalOffset - previousOffset) < 0.5, "Theme reset the scroll position: " + character);
                Require(Object.ReferenceEquals(Field<Image>(window, "portraitImage").Source, portrait), "Theme decoded or replaced cached portrait: " + character);
                PetWindow.MenuPalette palette = PetWindow.MenuPalette.ForCharacter(character);
                Require(((SolidColorBrush)window.Resources["WindowSurface"]).Color == (Color)ColorConverter.ConvertFromString(palette.Surface), "Theme differs from character menu: " + character);
                Require(Contrast((SolidColorBrush)window.Resources["PrimaryText"], (SolidColorBrush)window.Resources["CardSurface"]) >= 4.5, "News title contrast: " + character);
                Require(Contrast((SolidColorBrush)window.Resources["SecondaryText"], (SolidColorBrush)window.Resources["CardSurface"]) >= 4.5, "Metadata contrast: " + character);
                scroll.ScrollToTop(); Layout(window); CheckTextBounds((FrameworkElement)window.Content, (FrameworkElement)window.Content);
                RenderPreview(window, Path.Combine(previews, "highlights-" + character + ".png"));
            }
            Call(window, "NavigateSource", "GitHub"); Layout(window);
            Require(scroll.VerticalOffset > 100, "Navigation must jump to GitHub without a full render");
            RenderPreview(window, Path.Combine(previews, "highlights-github.png"));
            Dictionary<string, FrameworkElement> heads = Field<Dictionary<string, FrameworkElement>>(window, "sectionHeaders");
            ((Button)((Grid)heads["微博热搜"]).Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            typeof(HighlightsWindow).GetField("draggingSource", Private).SetValue(window, "NBA");
            HighlightsResult pending = ThemeFixture(); Call(window, "Render", pending);
            window.ApplyCharacterTheme("ganyu", PetAssets.Load("ganyu")["idle"][0]);
            Require(bodies["微博热搜"].Visibility == Visibility.Collapsed, "Theme lost collapsed state");
            Require(Object.ReferenceEquals(Field<HighlightsResult>(window, "deferredResult"), pending), "Theme discarded pending refresh during drag");
            typeof(HighlightsWindow).GetField("draggingSource", Private).SetValue(window, null);
            typeof(HighlightsWindow).GetField("deferredResult", Private).SetValue(window, null);
            Field<HighlightsLayout>(window, "sectionLayout").Collapsed.Clear();
            result.Items[0].Title = "离线长标题示例：当一条消息包含很多文字时，标题应自然换行，阅读原文按钮保持在右侧，卡片不能越过窗口边缘，也不应遮住后面的内容。";
            result.Errors.Add("微博暂时无法连接，请稍后重试"); result.Errors.Add("B站来源暂未更新，正在显示已有内容"); result.Errors.Add("体育来源暂未更新");
            window.Width = 620; window.Height = 480; Call(window, "Render", result); Layout(window);
            CheckTextBounds((FrameworkElement)window.Content, (FrameworkElement)window.Content);
            Require(Field<TextBlock>(window, "statusText").ActualHeight > 20, "Long error status should wrap");
            Require(Field<TextBlock>(window, "statusText").ToolTip.ToString().Contains("体育"), "Full error unavailable in tooltip");
            RenderPreview(window, Path.Combine(previews, "highlights-compact.png"));
            scroll.ScrollToVerticalOffset(270); Layout(window);
            RenderPreview(window, Path.Combine(previews, "highlights-compact-scrolled.png"));
            window.Width = 780; window.Height = 700;
            window.ApplyCharacterTheme("hutao", PetAssets.Load("hutao")["idle"][0]);
            Call(window, "SetStatus", "正在更新微博、B站、体育与虎扑的内容…", false);
            Call(window, "RenderLoading"); RenderPreview(window, Path.Combine(previews, "highlights-loading.png"));
            Call(window, "RenderEmpty", "这次没有连接上", "稍后点击刷新，继续看看今天的新鲜事。");
            Call(window, "SetStatus", "更新失败，请稍后重试", true); RenderPreview(window, Path.Combine(previews, "highlights-error.png"));
            HighlightsResult empty = new HighlightsResult { Day = "2026-10-26", UpdatedAt = "2026-10-26 15:30:00" };
            Call(window, "Render", empty); RenderPreview(window, Path.Combine(previews, "highlights-empty.png"));
            Require(Field<Dictionary<string, FrameworkElement>>(window, "sectionBodies").Count == HighlightsLayout.Sources.Length, "Empty result lost sections");
        }
        finally { window.Close(); }
    }

    private static void CheckTextBounds(DependencyObject node, FrameworkElement root)
    {
        TextBlock text = node as TextBlock;
        if (text != null && text.Visibility == Visibility.Visible && text.ActualWidth > 0)
        {
            Point location = text.TranslatePoint(new Point(), root);
            Require(location.X >= -0.5 && location.X + text.ActualWidth <= root.ActualWidth + 0.5, "Text outside horizontal window bounds: " + text.Text);
        }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) CheckTextBounds(VisualTreeHelper.GetChild(node, i), root);
    }

    private static double Contrast(SolidColorBrush first, SolidColorBrush second)
    {
        double a = Luminance(first.Color), b = Luminance(second.Color);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
    private static double Luminance(Color color) { return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B); }
    private static double Linear(byte value) { double channel = value / 255d; return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4); }
}
