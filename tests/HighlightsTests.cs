using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Threading.Tasks;

namespace ClaudePet
{
    internal static class HighlightsTests
    {
        private static int checks;

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }

        private static string Order(HighlightsLayout value) { return String.Join(",", value.Order.ToArray()); }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                HighlightsLayout layout = new HighlightsLayout {
                    Order = new List<string> { "NBA", "NBA", "unknown", "体育" },
                    Collapsed = new List<string> { "NBA", "NBA", "unknown" }
                };
                layout.Normalize();
                Check(Order(layout) == "NBA,体育,微博热搜,B站热门,GitHub,AI,股票,汽车,游戏,美女·时尚", "Old layout retains order and appends the six new sections.");
                Check(layout.Collapsed.Count == 1 && layout.Collapsed[0] == "NBA", "Only known unique collapse states survive.");
                Check(layout.MoveBefore("B站热门", "NBA"), "Last section can move to first.");
                Check(layout.Order[0] == "B站热门" && layout.Order[1] == "NBA" && layout.Order.Count == HighlightsLayout.Sources.Length, "Upward insertion is correct.");
                Check(layout.MoveBefore("B站热门", null), "First section can move to end.");
                Check(layout.Order[0] == "NBA" && layout.Order[layout.Order.Count - 1] == "B站热门", "Downward insertion is correct.");
                Check(layout.MoveBefore("NBA", "微博热搜"), "Can insert before a middle section.");
                Check(layout.Order[0] == "体育" && layout.Order[1] == "NBA" && layout.Order[2] == "微博热搜", "Destination is calculated after removing original section.");
                Check(!layout.MoveBefore("NBA", "NBA"), "Dropping onto self is unchanged.");
                Check(!layout.MoveBefore("NBA", "微博热搜"), "Dropping in original position is unchanged.");
                Check(!layout.MoveBefore("unknown", null), "Unknown section is rejected.");
                Check(!layout.MoveBefore("NBA", "unknown"), "Unknown destination is rejected.");
                JavaScriptSerializer json = new JavaScriptSerializer();
                HighlightsLayout roundtrip = json.Deserialize<HighlightsLayout>(json.Serialize(layout));
                roundtrip.Normalize();
                Check(Order(roundtrip) == Order(layout) && roundtrip.Collapsed.Contains("NBA"), "Order and collapse state survive JSON persistence.");
                layout.Order = null;
                layout.Collapsed = null;
                layout.Normalize();
                Check(layout.Order.Count == HighlightsLayout.Sources.Length && layout.Collapsed.Count == 0, "Null fields recover default layout.");

                string html = "<html><script type='application/json' id='__NEXT_DATA__'>" +
                    "{\"props\":{\"pageProps\":{\"data\":[" +
                    "{\"title\":\"App only\",\"url\":\"huputiyu://bball/live\"}," +
                    "{\"title\":\"Wrong host\",\"url\":\"https://hupu.com.example.com/1\"}," +
                    "{\"title\":\"A &amp; B\",\"url\":\"http://bbs.hupu.com/123.html\",\"origin\":\"ESPN\",\"time\":\"2分钟前\",\"replies\":42}," +
                    "{\"title\":\"Duplicate\",\"url\":\"https://bbs.hupu.com/123.html\"}," +
                    "{\"title\":\"Second\",\"url\":\"https://voice.hupu.com/nba/456.html\",\"origin\":\"虎扑\",\"replies\":0}" +
                    "]}}}</script></html>";
                List<HighlightItem> news = DailyHighlightsService.ParseHupuNba(html);
                Check(news.Count == 2, "Only distinct browser-accessible Hupu links are included.");
                Check(news[0].Url == "https://bbs.hupu.com/123.html", "Legacy HTTP links upgrade to HTTPS.");
                Check(news[0].Title == "A & B", "Titles decode HTML entities.");
                Check(news[0].Source == "NBA" && news[0].Summary.Contains("ESPN") && news[0].Summary.Contains("2分钟前"), "Feed attribution and time are preserved.");
                Check(news[0].Hot == "虎扑 · 42 回复", "Reply count identifies the Hupu feed.");
                bool rejected = false;
                try { DailyHighlightsService.ParseHupuNba("<html>layout changed</html>"); }
                catch (FormatException) { rejected = true; }
                Check(rejected, "Changed page schema reports failure instead of inventing news.");
                VerifyGitHub(); VerifyRss(); VerifyCacheMerge();

                if (args.Length > 0 && args[0] == "--live")
                {
                    System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072;
                    System.Net.ServicePointManager.DefaultConnectionLimit = 12;
                    List<Task<SourceResult>> tasks = new List<Task<SourceResult>>();
                    foreach (string name in HighlightsLayout.Sources) { string selected = name; tasks.Add(Task.Factory.StartNew(delegate { return DailyHighlightsService.FetchSource(selected); })); }
                    Task.WaitAll(tasks.ToArray());
                    foreach (Task<SourceResult> task in tasks) {
                        SourceResult source = task.Result;
                        Console.WriteLine("LIVE " + source.Name + " | " + source.Provider + " | " + source.Items.Count + " items | " + (source.Error ?? "OK"));
                        foreach (HighlightItem item in source.Items) Check(DailyHighlightsService.SafeHttpsUrl(item.Url) != null, "Live URLs must be safe HTTPS links.");
                    }
                }
                Console.WriteLine("PASS: " + checks + " checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void VerifyGitHub()
        {
            string json = "{\"items\":[" +
                "{\"full_name\":\"demo/first\",\"html_url\":\"https://github.com/demo/first\",\"description\":\"A &amp; B\",\"language\":\"C#\",\"forks_count\":50,\"stargazers_count\":200}," +
                "{\"full_name\":\"demo/second\",\"html_url\":\"https://github.com/demo/second\",\"forks_count\":100,\"stargazers_count\":12000}," +
                "{\"full_name\":\"Bad\",\"html_url\":\"https://github.com.example.com/x\",\"forks_count\":10000}," +
                "{\"full_name\":\"Duplicate\",\"html_url\":\"https://github.com/demo/first\"}," +
                "{\"full_name\":\"Private\",\"html_url\":\"https://github.com/demo/private\",\"private\":true}," +
                "{\"full_name\":\"Fork copy\",\"html_url\":\"https://github.com/demo/fork\",\"fork\":true}," +
                "{\"full_name\":\"Archived\",\"html_url\":\"https://github.com/demo/archive\",\"archived\":true}]}";
            List<HighlightItem> projects = DailyHighlightsService.ParseGitHub(json);
            Check(projects.Count == 2, "Only distinct public original GitHub projects are included.");
            Check(projects[0].Title == "demo/second", "GitHub projects sort by cumulative Fork count.");
            Check(projects[0].Hot.Contains("Fork 100") && projects[0].Hot.Contains("Star 1.2万"), "Fork and star metadata are shown.");
            Check(projects[1].Hot.Contains("C#") && projects[1].Summary == "A & B", "Language and cleaned description are preserved.");
            bool rejected = false; try { DailyHighlightsService.ParseGitHub("{\"message\":\"rate limit\"}"); } catch (FormatException) { rejected = true; }
            Check(rejected, "Unexpected API response is not reported as fresh empty data.");
        }

        private static void VerifyRss()
        {
            string rss = "<rss version='2.0' xmlns:News='urn:bing'><channel>" +
                "<item><title>A &amp; B</title><link>http://www.bing.com/news/apiclick.aspx?url=https%3A%2F%2Fexample.com%2Fstory</link><description>Useful summary</description><News:Source>新闻来源</News:Source><pubDate>Tue, 27 Oct 2026 00:00:00 GMT</pubDate></item>" +
                "<item><title>Duplicate</title><link>https://example.com/story</link><pubDate>Mon, 26 Oct 2026 23:30:00 GMT</pubDate></item>" +
                "<item><title>Unsafe</title><link>javascript:alert(1)</link><pubDate>Mon, 26 Oct 2026 23:30:00 GMT</pubDate></item>" +
                "<item><title>Local</title><link>https://localhost/news</link><pubDate>Mon, 26 Oct 2026 23:30:00 GMT</pubDate></item>" +
                "<item><title>Second</title><link>http://example.com/second</link><source>另一来源</source><pubDate>Mon, 26 Oct 2026 23:00:00 GMT</pubDate></item>" +
                "<item><title>Old news</title><link>https://example.com/old</link><pubDate>Mon, 01 Jan 2024 00:00:00 GMT</pubDate></item>" +
                "</channel></rss>";
            List<HighlightItem> items = DailyHighlightsService.ParseRss("AI", rss, "Bing 新闻", new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.FromHours(8)));
            Check(items.Count == 2, "RSS duplicates and unsafe protocols/local links are rejected.");
            Check(!items.Exists(delegate(HighlightItem item) { return item.Title == "Old news"; }), "Search results older than seven days do not appear as daily news.");
            Check(items[0].Title == "A & B" && items[0].Url == "https://example.com/story", "RSS entities and Bing redirect target are decoded.");
            Check(items[0].Hot == "新闻来源 · 10-27 08:00", "Publisher namespace and publication time in China are shown.");
            Check(items[1].Url == "https://example.com/second", "Legacy article links upgrade to HTTPS.");
            bool rejected = false; try { DailyHighlightsService.ParseRss("AI", "<html>Challenge</html>", "Bing 新闻"); } catch (FormatException) { rejected = true; }
            Check(rejected, "HTML challenge pages are treated as failed sources.");
            rejected = false; try { DailyHighlightsService.ParseRss("AI", "<!DOCTYPE rss [<!ENTITY xxe SYSTEM 'file:///C:/secret.txt'>]><rss><channel>&xxe;</channel></rss>", "Bing 新闻"); } catch (System.Xml.XmlException) { rejected = true; }
            Check(rejected, "External XML entities are prohibited.");
            foreach (string unsafeUrl in new string[] { "file:///C:/x", "https://127.0.0.1/a", "https://localhost/a", "https://user:pass@example.com/a", "https://example.com:8888/a", "ftp://example.com/a", "http://example.com/a", "not a URL" })
                Check(DailyHighlightsService.SafeHttpsUrl(unsafeUrl) == null, "Unsafe URL rejected: " + unsafeUrl);
        }

        private static void VerifyCacheMerge()
        {
            DateTime now = new DateTime(2026, 10, 27, 9, 0, 0);
            HighlightsResult cached = new HighlightsResult { Version = 2, Day = "2026-10-26", UpdatedAt = "2026-10-26 12:00:00" };
            cached.Items.Add(new HighlightItem { Source = "AI", Title = "Earlier AI item", Url = "https://example.com/ai" });
            cached.Items.Add(new HighlightItem { Source = "汽车", Title = "Old car item", Url = "https://example.com/car" });
            cached.Sources.Add(new HighlightSourceState { Name = "AI", Provider = "Bing 新闻", UpdatedAt = "2026-10-26 12:00:00" });
            cached.Sources.Add(new HighlightSourceState { Name = "汽车", Provider = "Bing 新闻", UpdatedAt = "2026-10-01 12:00:00" });
            SourceResult fresh = new SourceResult { Name = "GitHub", Provider = "GitHub API" };
            fresh.Items.Add(new HighlightItem { Source = "GitHub", Title = "Fresh repository", Url = "https://github.com/demo/new" });
            HighlightsResult result = DailyHighlightsService.MergeResults(new List<SourceResult> { fresh, new SourceResult { Name = "AI", Error = "连接超时" } }, cached, now);
            Check(result.Version == DailyHighlightsService.CurrentVersion && result.Sources.Count == HighlightsLayout.Sources.Length, "New cache schema tracks every source.");
            Check(result.Items.Count == 2, "Partial failure retains fresh results and valid source-specific cache only.");
            HighlightSourceState ai = result.Sources.Find(delegate(HighlightSourceState state) { return state.Name == "AI"; });
            Check(ai.IsCached && ai.UpdatedAt == "2026-10-26 12:00:00" && ai.Provider == "Bing 新闻", "Cache fallback preserves original age and provider.");
            Check(result.Errors.Exists(delegate(string error) { return error.Contains("AI") && error.Contains("缓存"); }), "Cached source is visibly identified.");
            Check(result.Errors.Exists(delegate(string error) { return error.Contains("汽车") && error.Contains("暂无缓存"); }), "Expired cache gets an explicit empty-state reason.");
            HighlightsResult retry = DailyHighlightsService.MergeResults(new List<SourceResult>(), result, now.AddHours(1));
            Check(retry.Sources.Find(delegate(HighlightSourceState state) { return state.Name == "AI"; }).UpdatedAt == ai.UpdatedAt, "Repeated failures never rejuvenate cached content.");
            Check(cached.Items.Count == 2 && !cached.Sources[0].IsCached, "Merging does not mutate previous cache.");
        }
    }
}
