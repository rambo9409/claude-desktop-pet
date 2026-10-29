using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClaudePet
{
    internal sealed class ResetHistoryItem
    {
        internal DateTimeOffset? At;
        internal string Title, Summary, Link;
    }

    internal sealed class ResetRhythmItem
    {
        internal string Label;
        internal double GapDays;
    }

    internal sealed class GptResetData
    {
        internal DateTimeOffset UpdatedAt;
        internal DateTimeOffset? LastResetAt, WindowEndAt;
        internal double? Probability24, Probability48, PaceDays, Median30Days, Longest30Days;
        internal int? VerifiedCount;
        internal bool SourceStale, HasSignal;
        internal string WindowLabel, Commitment, SignalStatus, SignalSummary, SignalUrl, StatusDescription, StatusIndicator;
        internal string Attribution, SourceUrl, Disclaimer;
        internal readonly List<ResetHistoryItem> History = new List<ResetHistoryItem>();
        internal readonly List<ResetRhythmItem> Rhythm = new List<ResetRhythmItem>();
        internal readonly List<string> Errors = new List<string>();

        internal bool IsStale(DateTimeOffset now)
        {
            return SourceStale || now - UpdatedAt > TimeSpan.FromMinutes(20) || UpdatedAt - now > TimeSpan.FromMinutes(5);
        }

        internal static string BeijingTime(DateTimeOffset? value)
        {
            return value.HasValue ? value.Value.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "未提供";
        }

        internal static string CommonWindow(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "未提供";
            string[] pieces = value.Split('-');
            if (pieces.Length != 2) return value;
            DateTime first, last;
            string[] formats = { "h tt", "hh tt", "h:mm tt", "hh:mm tt" };
            if (DateTime.TryParseExact(pieces[0].Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out first)
                && DateTime.TryParseExact(pieces[1].Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out last))
                return first.ToString("HH:mm", CultureInfo.InvariantCulture) + "–" + last.ToString("HH:mm", CultureInfo.InvariantCulture);
            return value;
        }

        internal string Countdown(DateTimeOffset now)
        {
            if (!HasSignal || !WindowEndAt.HasValue) return "站点未提供明确的下次重置时间";
            if (IsStale(now)) return "预测已过期，请刷新后查看时间窗口";
            TimeSpan left = WindowEndAt.Value - now;
            if (left <= TimeSpan.Zero) return "站点所列窗口已结束，等待新信号";
            return "距站点所列窗口结束  " + ((int)left.TotalHours).ToString("00") + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
        }

        internal static GptResetData Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024) throw new FormatException("预测数据为空或过大。");
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 40 };
            Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || !Bool(root, "ok")) throw new FormatException("预测接口未返回有效数据。");
            Dictionary<string, object> forecast = Map(root, "forecast");
            DateTimeOffset? updated = Date(root, "updated_at");
            if (forecast == null || !updated.HasValue) throw new FormatException("预测数据缺少更新时间或内容。");
            GptResetData data = new GptResetData {
                UpdatedAt = updated.Value, SourceStale = Bool(root, "stale"), LastResetAt = Date(forecast, "last_reset_at"),
                WindowEndAt = Date(forecast, "official_window_end_at"), Probability24 = Number(forecast, "probability_24h", 0, 100),
                Probability48 = Number(forecast, "probability_48h", 0, 100), PaceDays = Number(forecast, "pace_days", 0, 3650),
                Median30Days = Number(forecast, "median_gap_30d", 0, 3650), Longest30Days = Number(forecast, "longest_30d", 0, 3650),
                Commitment = Text(forecast, "commitment_label"), SignalStatus = Text(forecast, "signal_status"),
                SignalSummary = Text(forecast, "official_summary", 1000), SignalUrl = SafeUrl(Text(forecast, "official_url", 2048)),
                WindowLabel = CommonWindow(Text(forecast, "time_window_label")),
                Attribution = Text(root, "attribution"), SourceUrl = SafeUrl(Text(root, "source_url")), Disclaimer = Text(root, "disclaimer")
            };
            data.HasSignal = Bool(forecast, "has_official") || (!String.IsNullOrWhiteSpace(data.Commitment) && data.Commitment != "—");
            double? count = Number(forecast, "verified_count", 0, 1000000);
            if (count.HasValue) data.VerifiedCount = (int)count.Value;
            Dictionary<string, object> status = Map(root, "openai_status");
            data.StatusDescription = Text(status, "description"); data.StatusIndicator = Text(status, "indicator");
            foreach (object item in Items(root, "history"))
            {
                Dictionary<string, object> map = item as Dictionary<string, object>;
                if (map == null || data.History.Count >= 30) continue;
                data.History.Add(new ResetHistoryItem { At = Date(map, "at"), Title = Text(map, "title", 200), Summary = Text(map, "summary", 720), Link = SafeUrl(Text(map, "link", 2048)) });
            }
            foreach (object item in Items(forecast, "rhythm"))
            {
                Dictionary<string, object> map = item as Dictionary<string, object>;
                double? days = Number(map, "gap_days", 0, 3650);
                if (days.HasValue && data.Rhythm.Count < 12) data.Rhythm.Add(new ResetRhythmItem { Label = Text(map, "label"), GapDays = days.Value });
            }
            foreach (object item in Items(root, "errors"))
                if (item != null && data.Errors.Count < 10) data.Errors.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
            return data;
        }

        internal static string SafeUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https" || !uri.IsDefaultPort
                || !String.IsNullOrEmpty(uri.UserInfo) || uri.IsLoopback || uri.HostNameType != UriHostNameType.Dns) return null;
            string host = uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
            if (!host.Contains(".") || host.EndsWith(".local") || host.EndsWith(".localhost") || host.EndsWith(".internal")) return null;
            return uri.AbsoluteUri;
        }
        private static object Value(Dictionary<string, object> map, string key) { object value; return map != null && map.TryGetValue(key, out value) ? value : null; }
        private static Dictionary<string, object> Map(Dictionary<string, object> map, string key) { return Value(map, key) as Dictionary<string, object>; }
        private static string Text(Dictionary<string, object> map, string key, int limit = 400)
        {
            string text = Convert.ToString(Value(map, key), CultureInfo.InvariantCulture) ?? "";
            return text.Length > limit ? text.Substring(0, limit) + "…" : text;
        }
        private static bool Bool(Dictionary<string, object> map, string key) { return Value(map, key) is bool && (bool)Value(map, key); }
        private static IEnumerable Items(Dictionary<string, object> map, string key) { return Value(map, key) as object[] ?? new object[0]; }
        private static double? Number(Dictionary<string, object> map, string key, double min, double max)
        {
            double result;
            return Double.TryParse(Text(map, key), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                && !Double.IsNaN(result) && !Double.IsInfinity(result) && result >= min && result <= max ? (double?)result : null;
        }
        private static DateTimeOffset? Date(Dictionary<string, object> map, string key)
        {
            string value = Text(map, key);
            // The public endpoint uses offset-bearing ISO timestamps; do not assume the machine's timezone.
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"(?:Z|[+-]\d{2}:\d{2})$")) return null;
            DateTimeOffset parsed;
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) ? (DateTimeOffset?)parsed : null;
        }
    }

    internal static class GptResetService
    {
        internal const string SiteUrl = "https://tangyun.ai/";
        internal const string ApiUrl = "https://tangyun.ai/api/site/reset-radar";
        private static readonly HttpClient Client = CreateClient();
        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(18), MaxResponseContentBufferSize = 1024 * 1024 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GenshinStart/2.3");
            client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            return client;
        }
        internal static string CachePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet", "gpt-reset-cache.json"); } }
        internal static GptResetData ReadCache(string path)
        {
            try { return File.Exists(path) && new FileInfo(path).Length <= 1024 * 1024 ? GptResetData.Parse(File.ReadAllText(path)) : null; } catch { return null; }
        }
        internal static Task<GptResetData> FetchAsync() { return FetchAsync(CachePath); }
        internal static async Task<GptResetData> FetchAsync(string cachePath)
        {
            string json = await Client.GetStringAsync(ApiUrl).ConfigureAwait(false);
            GptResetData data = GptResetData.Parse(json);
            PetStorage.TryWriteAllText(cachePath, json, "gpt-reset-cache");
            return data;
        }
    }

    internal sealed class GptResetWindow : Window
    {
        private readonly StackPanel body;
        private readonly TextBlock syncStatus;
        private readonly Button refreshButton;
        private readonly ScrollViewer scroll;
        private readonly DispatcherTimer refreshTimer, countdownTimer;
        private readonly bool useNetwork;
        private bool loading, closed;
        private bool showingCache;
        private string lastError;
        private GptResetData data;
        private TextBlock countdown;
        private bool wasStale;

        internal GptResetWindow(string character) : this(character, true) { }
        internal GptResetWindow(string character, bool startNetwork)
        {
            useNetwork = startNetwork;
            Title = "原神启动 · GPT 重置预测";
            Width = Math.Min(800, SystemParameters.WorkArea.Width - 32); Height = Math.Min(850, SystemParameters.WorkArea.Height - 48);
            MinWidth = 560; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResizeWithGrip; FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13;
            ApplyCharacterTheme(character);
            ConfigureControls();
            Border shell = new Border { CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(24, 22, 24, 16) };
            shell.SetResourceReference(Border.BackgroundProperty, "ResetSurface"); shell.SetResourceReference(Border.BorderBrushProperty, "ResetBorder");
            Content = shell;
            Grid layout = new Grid(); shell.Child = layout;
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 18) }; layout.Children.Add(header);
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel(); header.Children.Add(title);
            title.Children.Add(Label("GPT 重置预测", 25, true));
            TextBlock sub = Label("堂云 AI 数据同步  /  北京时间 UTC+8", 12, false, true); sub.Margin = new Thickness(0, 5, 0, 0); title.Children.Add(sub);
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(actions, 1); header.Children.Add(actions);
            refreshButton = Button("刷新", delegate { Refresh(); }); actions.Children.Add(refreshButton);
            actions.Children.Add(Button("原站 ↗", delegate { OpenLink(GptResetService.SiteUrl); }));
            Button close = Button("×", delegate { Close(); }); close.ToolTip = "关闭"; actions.Children.Add(close);
            scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
            Grid.SetRow(scroll, 1); layout.Children.Add(scroll); body = new StackPanel(); scroll.Content = body;
            syncStatus = Label("连接堂云 AI 获取公开预测…", 11, false, true); syncStatus.Margin = new Thickness(0, 13, 12, 0); Grid.SetRow(syncStatus, 2); layout.Children.Add(syncStatus);
            body.Children.Add(Card(Label("正在读取预测数据…", 16, false)));
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) }; refreshTimer.Tick += delegate { Refresh(); };
            countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; countdownTimer.Tick += delegate { Tick(DateTimeOffset.UtcNow); };
            Loaded += delegate {
                if (!useNetwork) return;
                data = GptResetService.ReadCache(GptResetService.CachePath); showingCache = data != null;
                if (data != null) Render(data, DateTimeOffset.UtcNow);
                Refresh(); refreshTimer.Start(); countdownTimer.Start();
            };
            Closed += delegate { closed = true; refreshTimer.Stop(); countdownTimer.Stop(); };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); };
        }

        internal void ApplyCharacterTheme(string character)
        {
            PetWindow.MenuPalette palette = PetWindow.MenuPalette.ForCharacter(character);
            string[] keys = { "ResetSurface", "ResetText", "ResetMuted", "ResetAccent", "ResetBorder", "ResetCard", "ResetHover" };
            string[] values = { palette.Surface, palette.Text, palette.Muted, palette.Accent, palette.Border, palette.Checked, palette.Hover };
            for (int i = 0; i < keys.Length; i++) { SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i])); brush.Freeze(); Resources[keys[i]] = brush; }
            SetResourceReference(ForegroundProperty, "ResetText");
        }

        private void ConfigureControls()
        {
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type Button}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
  <Border x:Name='Chrome' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
    <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
  </Border><ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource ResetHover}'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource ResetAccent}'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='0.5'/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ScrollBar}'><Setter Property='Width' Value='11'/><Setter Property='Template'><Setter.Value>
<ControlTemplate TargetType='{x:Type ScrollBar}'>
  <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'>
    <Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
    <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='{DynamicResource ResetBorder}' CornerRadius='4' Margin='3,0'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
    <Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
  </Track></Grid>
</ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
            Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(xaml));
        }

        private async void Refresh()
        {
            if (!useNetwork || loading || closed) return;
            loading = true; refreshButton.IsEnabled = false; syncStatus.Text = "正在同步堂云 AI…";
            try {
                GptResetData fresh = await GptResetService.FetchAsync();
                if (closed) return;
                data = fresh; showingCache = false; lastError = null; Render(data, DateTimeOffset.UtcNow);
            } catch {
                if (closed) return;
                lastError = "同步失败，请检查网络后重试";
                if (data != null) { showingCache = true; Render(data, DateTimeOffset.UtcNow); }
                else { body.Children.Clear(); body.Children.Add(Card(Label("暂时无法获取预测\n请稍后刷新，或打开原站查看。", 17, false))); }
            } finally {
                loading = false;
                if (!closed) { refreshButton.IsEnabled = true; UpdateStatus(DateTimeOffset.UtcNow); }
            }
        }

        internal void Render(GptResetData value, DateTimeOffset now)
        {
            double oldScroll = scroll.VerticalOffset; data = value; body.Children.Clear(); countdown = null;
            if (data == null) { body.Children.Add(Card(Label("暂无预测数据", 17, false))); return; }
            wasStale = data.IsStale(now);
            TextBlock scope = Label("站点预测 · 全站重置观察\n与个人账号额度的重置时间不同；本工具不读取个人额度。", 12, false, true);
            scope.Margin = new Thickness(0, 0, 0, 16); body.Children.Add(scope);
            if (wasStale || showingCache || lastError != null || data.Errors.Count > 0)
                body.Children.Add(Card(Label(wasStale ? "预测已过期 · 以下为历史快照，请刷新后再参考。" : lastError != null ? lastError + " · 暂时显示上次数据。" : data.Errors.Count > 0 ? "来源部分数据暂不可用，以下内容可能不完整。" : "本地缓存 · 正在核对最新预测。", 13, true)));
            body.Children.Add(Section(data.HasSignal ? "历史模型基线 · 另有站点收录信号" : "未来重置概率"));
            UniformGrid probabilities = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 0, -4, 0) };
            probabilities.Children.Add(Probability("未来 24 小时", data.Probability24)); probabilities.Children.Add(Probability("未来 48 小时", data.Probability48)); body.Children.Add(probabilities);
            StackPanel signal = new StackPanel();
            signal.Children.Add(Label(data.HasSignal ? "站点收录信号  ·  " + data.Commitment : "历史节奏模型", 16, true));
            TextBlock signalText = Label(data.HasSignal ? data.SignalSummary : "目前站点未收录明确的重置承诺。概率来自历史节奏模型，不代表确定会发生。", 12, false, true);
            signalText.Margin = new Thickness(0, 7, 0, 0); signal.Children.Add(signalText);
            if (data.HasSignal && data.WindowEndAt.HasValue) signal.Children.Add(Label("站点所列窗口截止  " + GptResetData.BeijingTime(data.WindowEndAt), 13, true));
            countdown = Label(data.Countdown(now), 12, false, true); countdown.Margin = new Thickness(0, 8, 0, 0); signal.Children.Add(countdown);
            if (data.HasSignal && data.SignalUrl != null) signal.Children.Add(Link("查看信号原文 ↗", data.SignalUrl));
            body.Children.Add(Card(signal));
            body.Children.Add(Section("时间与节奏"));
            UniformGrid metrics = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 0, -4, 0) };
            metrics.Children.Add(Metric("上次重置 · 北京时间", GptResetData.BeijingTime(data.LastResetAt)));
            metrics.Children.Add(Metric("历史常见时段 · 北京时间", data.WindowLabel));
            metrics.Children.Add(Metric("近期重置间隔", Days(data.PaceDays)));
            metrics.Children.Add(Metric("近 30 天中位间隔", Days(data.Median30Days)));
            body.Children.Add(metrics);
            if (data.Rhythm.Count > 0) body.Children.Add(RhythmChart(data));
            if (!String.IsNullOrWhiteSpace(data.StatusDescription)) {
                TextBlock status = Label("来源收录的服务状态  ·  " + (data.StatusIndicator == "none" ? "服务正常" : data.StatusDescription), 12, false, true);
                status.Margin = new Thickness(0, 1, 0, 10); body.Children.Add(status);
            }
            body.Children.Add(Section("历史记录  ·  " + data.History.Count + " 条"));
            if (data.History.Count == 0) body.Children.Add(Card(Label("站点暂未提供历史记录", 13, false, true)));
            foreach (ResetHistoryItem history in data.History) {
                StackPanel item = new StackPanel(); item.Children.Add(Label(GptResetData.BeijingTime(history.At), 12, true));
                TextBlock summary = Label(String.IsNullOrWhiteSpace(history.Summary) ? history.Title : history.Summary, 13, false); summary.Margin = new Thickness(0, 7, 0, 0); item.Children.Add(summary);
                if (history.Link != null) item.Children.Add(Link("来源原文 ↗", history.Link));
                body.Children.Add(Card(item));
            }
            string count = data.VerifiedCount.HasValue ? "来源累计记录 " + data.VerifiedCount + " 次；" : "";
            string attribution = String.IsNullOrWhiteSpace(data.Attribution) ? "上游来源未标注" : data.Attribution;
            TextBlock footer = Label(count + "近 30 天最长等待 " + Days(data.Longest30Days) + "\n同步来源：tangyun.ai；" + attribution + "\n第三方公开预测，仅供参考；与 OpenAI 无关联。", 11, false, true);
            footer.Margin = new Thickness(0, 8, 0, 12); body.Children.Add(footer);
            if (data.SourceUrl != null) body.Children.Add(Link("查看上游数据来源 ↗", data.SourceUrl));
            scroll.ScrollToVerticalOffset(oldScroll); UpdateStatus(now);
        }

        private static string Days(double? value) { return value.HasValue ? value.Value.ToString("0.#", CultureInfo.InvariantCulture) + " 天" : "未提供"; }
        private void Tick(DateTimeOffset now)
        {
            if (data == null) return;
            if (wasStale != data.IsStale(now)) { Render(data, now); return; }
            if (countdown != null) countdown.Text = data.Countdown(now);
            if (!loading) UpdateStatus(now);
        }
        private void UpdateStatus(DateTimeOffset now)
        {
            if (lastError != null) syncStatus.Text = lastError + (data == null ? "" : " · 快照 " + GptResetData.BeijingTime(data.UpdatedAt));
            else syncStatus.Text = data == null ? "尚未获取数据" : (data.IsStale(now) ? "已过期 · " : showingCache ? "缓存 · " : "已同步 · ") + "来源更新 " + GptResetData.BeijingTime(data.UpdatedAt) + "  ·  每 10 分钟自动刷新";
        }
        private TextBlock Label(string text, double size, bool bold, bool muted = false)
        {
            TextBlock block = new TextBlock { Text = text ?? "", FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.55 };
            block.SetResourceReference(TextBlock.ForegroundProperty, muted ? "ResetMuted" : "ResetText"); return block;
        }
        private TextBlock Section(string text) { TextBlock block = Label(text, 14, true); block.Margin = new Thickness(0, 6, 0, 10); return block; }
        private Border Card(UIElement child)
        {
            Border card = new Border { Child = child, CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 13, 16, 13), Margin = new Thickness(0, 0, 0, 10), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "ResetCard"); card.SetResourceReference(Border.BorderBrushProperty, "ResetBorder"); return card;
        }
        private Border Probability(string label, double? value)
        {
            StackPanel panel = new StackPanel(); panel.Children.Add(Label(label, 12, false, true));
            TextBlock number = Label(value.HasValue ? value.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "—", 40, true); number.Margin = new Thickness(0, 4, 0, 3); number.SetResourceReference(TextBlock.ForegroundProperty, "ResetAccent"); panel.Children.Add(number);
            ProgressBar bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = value ?? 0, Height = 5, BorderThickness = new Thickness(0) };
            bar.SetResourceReference(Control.ForegroundProperty, "ResetAccent"); bar.SetResourceReference(Control.BackgroundProperty, "ResetBorder"); panel.Children.Add(bar);
            Border card = Card(panel); card.Margin = new Thickness(4, 0, 4, 10); return card;
        }
        private Border Metric(string name, string value)
        {
            StackPanel panel = new StackPanel(); panel.Children.Add(Label(name, 11, false, true)); TextBlock content = Label(value, 16, true); content.Margin = new Thickness(0, 5, 0, 0); panel.Children.Add(content);
            Border card = Card(panel); card.Margin = new Thickness(4, 0, 4, 10); return card;
        }
        private Border RhythmChart(GptResetData value)
        {
            StackPanel panel = new StackPanel(); panel.Children.Add(Label("最近重置的间隔（天）", 12, false, true));
            UniformGrid chart = new UniformGrid { Columns = value.Rhythm.Count, Height = 127, Margin = new Thickness(0, 8, 0, 0) };
            double max = 1; foreach (ResetRhythmItem item in value.Rhythm) max = Math.Max(max, item.GapDays);
            foreach (ResetRhythmItem item in value.Rhythm) {
                StackPanel column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(3, 0, 3, 0) };
                TextBlock number = Label(item.GapDays.ToString("0.#", CultureInfo.InvariantCulture), 11, true); number.TextAlignment = TextAlignment.Center; column.Children.Add(number);
                Border bar = new Border { Height = Math.Max(3, item.GapDays / max * 70), CornerRadius = new CornerRadius(4, 4, 0, 0), MaxWidth = 38, Margin = new Thickness(0, 4, 0, 4) }; bar.SetResourceReference(Border.BackgroundProperty, "ResetAccent"); column.Children.Add(bar);
                TextBlock date = Label(item.Label, 10, false, true); date.TextAlignment = TextAlignment.Center; date.TextWrapping = TextWrapping.NoWrap; date.TextTrimming = TextTrimming.CharacterEllipsis; date.ToolTip = item.Label; column.Children.Add(date); chart.Children.Add(column);
            }
            panel.Children.Add(chart); return Card(panel);
        }
        private Button Button(string text, Action action)
        {
            Button button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(6, 0, 0, 0), FontSize = 12, Cursor = Cursors.Hand, BorderThickness = new Thickness(1) };
            button.SetResourceReference(Control.ForegroundProperty, "ResetText"); button.SetResourceReference(Control.BackgroundProperty, "ResetCard"); button.SetResourceReference(Control.BorderBrushProperty, "ResetBorder");
            button.Click += delegate { action(); }; return button;
        }
        private Button Link(string text, string url)
        {
            Button link = Button(text, delegate { OpenLink(url); }); link.Margin = new Thickness(0, 9, 0, 0); link.HorizontalAlignment = HorizontalAlignment.Left; return link;
        }
        private void OpenLink(string url)
        {
            string safe = GptResetData.SafeUrl(url); if (safe == null) return;
            try { Process.Start(new ProcessStartInfo(safe) { UseShellExecute = true }); }
            catch { syncStatus.Text = "无法打开浏览器，请稍后重试。"; }
        }
    }
}
