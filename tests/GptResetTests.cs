using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudePet;

internal static class GptResetTests
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name, Private).GetValue(value); }
    private static DateTimeOffset At(string value) { return DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture); }
    private static void Reject(string json)
    {
        bool rejected = false;
        try { GptResetData.Parse(json); } catch { rejected = true; }
        Check(rejected, "Invalid API result was accepted: " + json);
    }
    private static void Parsing(string json)
    {
        GptResetData data = GptResetData.Parse(json);
        Check(data.Probability24 == 53 && data.Probability48 == 78, "Probability units must remain percent from the source.");
        Check(data.UpdatedAt == At("2026-10-27T08:54:08+08:00"), "Source update timestamp lost its timezone.");
        Check(data.LastResetAt == At("2026-10-26T14:08:46+08:00"), "Last reset timestamp changed.");
        Check(data.WindowLabel == "12:00–15:00", "Beijing common window conversion failed.");
        Check(!data.HasSignal && !data.WindowEndAt.HasValue, "A historical time slot was treated as a next reset commitment.");
        Check(data.Countdown(data.UpdatedAt).Contains("未提供明确"), "Model probability fabricated a countdown.");
        Check(data.History.Count == 12 && data.Rhythm.Count == 6, "Live fixture history/rhythm not preserved.");
        Check(data.VerifiedCount == 50 && data.PaceDays == 1.5 && data.Median30Days == 2.4, "Live fixture statistics changed.");
        Check(data.Attribution == "数据来自 codex-reset.com" && data.SourceUrl == "https://codex-reset.com/", "Upstream attribution dropped.");
        Check(!data.IsStale(data.UpdatedAt.AddMinutes(19)), "Recent data incorrectly expired.");
        Check(data.IsStale(data.UpdatedAt.AddMinutes(21)), "Old source timestamp is presented as live.");
        Check(data.IsStale(data.UpdatedAt.AddMinutes(-6)), "Future source timestamp is presented as current.");
        Check(GptResetData.Parse(json.Replace("\"stale\":false", "\"stale\":true")).IsStale(data.UpdatedAt), "Source stale flag ignored.");
        Check(GptResetData.BeijingTime(At("2026-10-27T00:54:08Z")) == "2026-10-27 08:54", "UTC timestamp is not displayed in Beijing time.");
        Check(GptResetData.CommonWindow("12 AM - 1 PM") == "00:00–13:00", "AM/PM midday/midnight conversion failed.");
        Check(GptResetData.CommonWindow("unrecognized source label") == "unrecognized source label", "Unknown source slot should stay as supplied.");
        Check(GptResetData.CommonWindow(null) == "未提供", "Missing historical slot became a time.");
        string invalidNumbers = json.Replace("\"probability_24h\":53", "\"probability_24h\":101").Replace("\"probability_48h\":78", "\"probability_48h\":\"NaN\"");
        GptResetData missing = GptResetData.Parse(invalidNumbers);
        Check(!missing.Probability24.HasValue && !missing.Probability48.HasValue, "Invalid probabilities were displayed.");
        Reject("{}"); Reject("[]"); Reject("not-json"); Reject("{\"ok\":false}"); Reject("{\"ok\":true,\"forecast\":{}}");
        Reject(json.Replace("2026-10-27T08:54:08+08:00", "2026-10-27T08:54:08"));
        foreach (string url in new[] { "javascript:alert(1)", "file:///C:/Windows/system.ini", "https://user:pass@example.org/", "data:text/html,test", "relative/path", "http://example.org/", "https://localhost/", "https://127.0.0.1/", "https://10.0.0.1/", "https://device.local/", "https://example.org:8888/" })
            Check(GptResetData.SafeUrl(url) == null, "Unsafe external link accepted: " + url);
        Check(GptResetData.SafeUrl("https://x.com/example/status/1") != null, "Ordinary public source link rejected.");
        string badLink = json.Replace("https://x.com/thsottiaux/status/2114588174249071102", "file:///C:/test.cmd");
        Check(GptResetData.Parse(badLink).History[0].Link == null, "History parser did not sanitize links.");
        GptResetData longText = GptResetData.Parse("{\"ok\":true,\"updated_at\":\"2026-10-27T08:54:08+08:00\",\"forecast\":{\"commitment_label\":\"" + new string('x', 10000) + "\"},\"history\":[{\"summary\":\"" + new string('y', 10000) + "\"}]}");
        Check(longText.Commitment.Length <= 401 && longText.History[0].Summary.Length <= 721, "Unbounded source text can overwhelm layout.");
        string official = json.Replace("\"has_official\":false", "\"has_official\":true")
            .Replace("\"official_window_end_at\":\"\"", "\"official_window_end_at\":\"2026-10-27T10:54:08+08:00\"")
            .Replace("\"commitment_label\":\"—\"", "\"commitment_label\":\"测试信号\"");
        GptResetData commitment = GptResetData.Parse(official);
        Check(commitment.Countdown(data.UpdatedAt).Contains("02:00:00"), "Explicit source deadline countdown incorrect.");
        Check(commitment.Countdown(data.UpdatedAt.AddMinutes(21)).Contains("已过期"), "Stale forecast countdown still counts toward old deadline.");
        commitment.UpdatedAt = commitment.WindowEndAt.Value;
        Check(commitment.Countdown(commitment.UpdatedAt).Contains("已结束"), "Ended window shows negative countdown.");
        string temp = Path.Combine("artifacts", "tests", "gpt-reset-test-cache.json");
        File.WriteAllText(temp, json);
        Check(GptResetService.ReadCache(temp).History.Count == 12, "Disk cache parsing failed.");
        File.WriteAllText(temp, "corrupt cache");
        Check(GptResetService.ReadCache(temp) == null, "Corrupt cache blocks recovery.");
        Check(GptResetService.ReadCache(temp + ".missing") == null, "Missing cache blocks first launch.");
    }

    private static void Layout(GptResetWindow window)
    {
        FrameworkElement content = (FrameworkElement)window.Content;
        content.Measure(new Size(window.Width, window.Height)); content.Arrange(new Rect(0, 0, window.Width, window.Height)); content.UpdateLayout();
    }
    private static void Render(GptResetWindow window, string name)
    {
        Layout(window);
        RenderTargetBitmap image = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
        image.Render((Visual)window.Content); PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (FileStream output = File.Create(Path.Combine("artifacts", "tests", name + ".png"))) encoder.Save(output);
    }
    private static IEnumerable<TextBlock> Texts(DependencyObject parent)
    {
        TextBlock text = parent as TextBlock; if (text != null) yield return text;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (TextBlock child in Texts(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
    private static string AllText(GptResetWindow window)
    {
        string text = ""; foreach (TextBlock item in Texts((Visual)window.Content)) text += item.Text + "\n"; return text;
    }
    private static double Channel(byte value) { double x = value / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
    private static double Luma(Color color) { return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B); }
    private static void Visual(string json)
    {
        GptResetData data = GptResetData.Parse(json);
        GptResetWindow window = new GptResetWindow("kolo", false) { Width = 800, Height = 850 };
        foreach (string theme in new[] { "kolo", "hutao", "ganyu", "varesa" }) {
            window.ApplyCharacterTheme(theme); window.Render(data, data.UpdatedAt); Layout(window);
            ScrollViewer scroll = Field<ScrollViewer>(window, "scroll");
            Check(scroll.ScrollableHeight > 200, "History should be accessible through scrolling.");
            Check(scroll.ExtentWidth <= scroll.ViewportWidth + 1, "Forecast content overflows horizontally.");
            string text = AllText(window);
            Check(text.Contains("53%") && text.Contains("78%") && text.Contains("站点未提供明确"), "Forecast summary values missing.");
            Check(text.Contains("个人账号额度") && text.Contains("codex-reset.com") && text.Contains("10 分钟"), "Scope, attribution, or refresh schedule missing.");
            foreach (string background in new[] { "ResetSurface", "ResetCard" })
            foreach (string foreground in new[] { "ResetText", "ResetMuted", "ResetAccent" }) {
                double a = Luma(((SolidColorBrush)window.Resources[background]).Color), b = Luma(((SolidColorBrush)window.Resources[foreground]).Color);
                Check((Math.Max(a, b) + .05) / (Math.Min(a, b) + .05) >= 4.5, "Theme text contrast too low: " + theme + "/" + foreground);
            }
            Render(window, "gpt-reset-" + theme);
        }
        object body = Field<StackPanel>(window, "body");
        window.ApplyCharacterTheme("ganyu");
        Check(Object.ReferenceEquals(body, Field<StackPanel>(window, "body")), "Theme switch rebuilt/reset content.");
        ScrollViewer historyScroll = Field<ScrollViewer>(window, "scroll"); historyScroll.ScrollToVerticalOffset(800); Layout(window);
        Render(window, "gpt-reset-history");
        window.Render(data, data.UpdatedAt); Layout(window); Check(historyScroll.VerticalOffset >= 799, "Data refresh lost scroll position.");
        historyScroll.ScrollToHome(); Layout(window);
        window.Width = 560; window.Height = 480; window.Render(data, data.UpdatedAt); Render(window, "gpt-reset-narrow");
        ScrollViewer narrow = Field<ScrollViewer>(window, "scroll"); Check(narrow.ExtentWidth <= narrow.ViewportWidth + 1, "Narrow layout has horizontal clipping.");
        narrow.ScrollToEnd(); Layout(window); Check(narrow.VerticalOffset > 200, "History cannot be reached using scrolling.");
        window.ApplyCharacterTheme("varesa"); Layout(window); Check(narrow.VerticalOffset > 200, "Theme switch lost the current scroll position.");
        narrow.ScrollToHome(); Layout(window);
        window.Width = 800; window.Height = 850; window.Render(data, data.UpdatedAt.AddHours(1)); Render(window, "gpt-reset-expired");
        Check(AllText(window).Contains("预测已过期"), "Expired source warning missing.");
        typeof(GptResetWindow).GetField("lastError", Private).SetValue(window, "同步失败，请检查网络后重试");
        window.Render(data, data.UpdatedAt); Render(window, "gpt-reset-cached-error"); Check(AllText(window).Contains("暂时显示上次数据"), "Offline cache warning missing.");
        typeof(GptResetWindow).GetField("lastError", Private).SetValue(window, null);
        GptResetData partial = GptResetData.Parse("{\"ok\":true,\"updated_at\":\"2026-10-27T08:54:08+08:00\",\"forecast\":{}}");
        window.Render(partial, partial.UpdatedAt); Render(window, "gpt-reset-partial");
        Check(AllText(window).Contains("站点暂未提供历史记录") && !AllText(window).Contains("0%"), "Missing values displayed as zero.");
        window.Render(null, partial.UpdatedAt); Render(window, "gpt-reset-empty"); Check(AllText(window).Contains("暂无预测数据"), "Empty state missing.");
        window.Close();
        Check(!Field<System.Windows.Threading.DispatcherTimer>(window, "refreshTimer").IsEnabled, "Refresh timer leaked after closing.");
    }

    [STAThread]
    private static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.Combine("artifacts", "tests"));
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string json = File.ReadAllText(Path.Combine("tests", "fixtures", "gpt-reset-radar-2026-10-27.json"));
        Parsing(json); Visual(json);
        if (Array.IndexOf(args, "--live") >= 0) {
            GptResetData live = GptResetService.FetchAsync(Path.Combine("artifacts", "tests", "gpt-reset-live-cache.json")).GetAwaiter().GetResult();
            Check(live.UpdatedAt != default(DateTimeOffset), "Live runtime request has no valid source timestamp.");
            Console.WriteLine("Live endpoint verified; source updated at Beijing " + GptResetData.BeijingTime(live.UpdatedAt) + "; history=" + live.History.Count + ".");
        }
        Console.WriteLine("PASS: " + checks + " GPT reset parser/cache/time/links/theme/layout checks; fixture screenshots rendered; no account access or user settings writes.");
        app.Shutdown();
    }
}
