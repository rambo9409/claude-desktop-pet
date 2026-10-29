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
    internal sealed class HighlightItem
    {
        public string Source { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Url { get; set; }
        public string Hot { get; set; }
    }

    internal sealed class HighlightsResult
    {
        public int Version { get; set; }
        public string Day { get; set; }
        public string UpdatedAt { get; set; }
        public string Overview { get; set; }
        public List<HighlightItem> Items { get; set; }
        public List<string> Errors { get; set; }
        public List<HighlightSourceState> Sources { get; set; }

        public HighlightsResult()
        {
            Items = new List<HighlightItem>();
            Errors = new List<string>();
            Sources = new List<HighlightSourceState>();
        }
    }

    internal sealed class HighlightSourceState
    {
        public string Name { get; set; }
        public string Provider { get; set; }
        public string UpdatedAt { get; set; }
        public bool IsCached { get; set; }
        public string Error { get; set; }
    }

    internal sealed class HighlightsLayout
    {
        internal static readonly string[] Sources = { "微博热搜", "B站热门", "体育", "NBA", "GitHub", "AI", "股票", "汽车", "游戏", "美女·时尚" };
        public List<string> Order { get; set; }
        public List<string> Collapsed { get; set; }

        public HighlightsLayout()
        {
            Order = new List<string>(Sources);
            Collapsed = new List<string>();
        }

        internal void Normalize()
        {
            List<string> valid = new List<string>();
            if (Order != null)
                foreach (string source in Order)
                    if (Array.IndexOf(Sources, source) >= 0 && !valid.Contains(source)) valid.Add(source);
            foreach (string source in Sources)
                if (!valid.Contains(source)) valid.Add(source);
            Order = valid;
            valid = new List<string>();
            if (Collapsed != null)
                foreach (string source in Collapsed)
                    if (Array.IndexOf(Sources, source) >= 0 && !valid.Contains(source)) valid.Add(source);
            Collapsed = valid;
        }

        internal bool MoveBefore(string source, string before)
        {
            Normalize();
            if (!Order.Contains(source) || source == before || (before != null && !Order.Contains(before))) return false;
            List<string> previous = new List<string>(Order);
            Order.Remove(source);
            Order.Insert(before == null ? Order.Count : Order.IndexOf(before), source);
            return String.Join("|", previous.ToArray()) != String.Join("|", Order.ToArray());
        }
    }

    internal static class PetSettings
    {
        private static string RootDirectory
        {
            get
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KoloPet");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        internal static string CachePath
        {
            get { return Path.Combine(RootDirectory, "daily-highlights.json"); }
        }

        internal static HighlightsLayout LoadHighlightsLayout()
        {
            try
            {
                string path = Path.Combine(RootDirectory, "highlights-layout.json");
                HighlightsLayout value = File.Exists(path)
                    ? new JavaScriptSerializer().Deserialize<HighlightsLayout>(File.ReadAllText(path, Encoding.UTF8))
                    : new HighlightsLayout();
                if (value == null) value = new HighlightsLayout();
                value.Normalize();
                return value;
            }
            catch { return new HighlightsLayout(); }
        }

        internal static void SaveHighlightsLayout(HighlightsLayout value)
        {
            try
            {
                value.Normalize();
                PetStorage.WriteAllTextAtomic(Path.Combine(RootDirectory, "highlights-layout.json"), new JavaScriptSerializer().Serialize(value));
            }
            catch (Exception error) { PetStorage.Log("highlights-layout", error); }
        }

        internal static string LoadCharacter()
        {
            try
            {
                string path = Path.Combine(RootDirectory, "character.txt");
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Trim() : "kolo";
            }
            catch
            {
                return "kolo";
            }
        }

        internal static void SaveCharacter(string key)
        {
            PetStorage.TryWriteAllText(Path.Combine(RootDirectory, "character.txt"), key, "character");
        }

        internal static bool LoadQuietMode()
        {
            try
            {
                string path = Path.Combine(RootDirectory, "quiet-mode.txt");
                return File.Exists(path) && File.ReadAllText(path, Encoding.UTF8).Trim() == "1";
            }
            catch
            {
                return false;
            }
        }

        internal static void SaveQuietMode(bool enabled)
        {
            PetStorage.TryWriteAllText(Path.Combine(RootDirectory, "quiet-mode.txt"), enabled ? "1" : "0", "quiet-mode");
        }
    }

    internal sealed class SourceResult
    {
        internal string Name;
        internal List<HighlightItem> Items = new List<HighlightItem>();
        internal string Error;
        internal string Provider;
    }

    internal sealed class TimedWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = 8000;
            HttpWebRequest http = request as HttpWebRequest;
            if (http != null)
            {
                http.ReadWriteTimeout = 8000;
                http.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            return request;
        }
    }

    internal static class DailyHighlightsService
    {
        private static readonly object FetchLock = new object();
        private static JavaScriptSerializer Json { get { return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }; } }
        internal const int CurrentVersion = 3;

        internal static HighlightsResult LoadToday()
        {
            HighlightsResult result = LoadCached();
            return result != null && result.Day == DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ? result : null;
        }

        private static HighlightsResult LoadCached()
        {
            try
            {
                if (!File.Exists(PetSettings.CachePath)) return null;
                HighlightsResult result = Json.Deserialize<HighlightsResult>(File.ReadAllText(PetSettings.CachePath, Encoding.UTF8));
                DateTime updated;
                if (result == null || !DateTime.TryParse(result.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out updated) ||
                    updated < DateTime.Now.AddDays(-7)) return null;
                if (result.Items == null) result.Items = new List<HighlightItem>();
                if (result.Errors == null) result.Errors = new List<string>();
                if (result.Sources == null) result.Sources = new List<HighlightSourceState>();
                // Previous versions used a different NBA feed. Do not label those cached links as Hupu.
                if (result.Version < 2)
                {
                    result.Items.RemoveAll(delegate(HighlightItem item) { return item.Source == "NBA"; });
                    result.Overview = BuildOverview(result.Items);
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        internal static bool HasFreshCache(double hours)
        {
            HighlightsResult result = LoadToday();
            if (result == null || result.Version != CurrentVersion) return false;
            DateTime updated;
            return DateTime.TryParseExact(
                result.UpdatedAt,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out updated) && DateTime.Now - updated < TimeSpan.FromHours(hours);
        }

        internal static HighlightsResult FetchAndSave()
        {
            lock (FetchLock)
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                ServicePointManager.DefaultConnectionLimit = Math.Max(12, ServicePointManager.DefaultConnectionLimit);
                Task<SourceResult>[] tasks = new Task<SourceResult>[HighlightsLayout.Sources.Length];
                for (int i = 0; i < tasks.Length; i++) {
                    string source = HighlightsLayout.Sources[i];
                    tasks[i] = Task.Factory.StartNew(delegate { return FetchSource(source); });
                }
                Task[] waits = new Task[tasks.Length];
                for (int i = 0; i < tasks.Length; i++) waits[i] = tasks[i];
                Task.WaitAll(waits, 22000);
                List<SourceResult> completed = new List<SourceResult>();
                for (int i = 0; i < tasks.Length; i++)
                {
                    completed.Add(tasks[i].Status == TaskStatus.RanToCompletion ? tasks[i].Result :
                        new SourceResult { Name = HighlightsLayout.Sources[i], Error = "连接超时，请稍后刷新" });
                }
                HighlightsResult result = MergeResults(completed, LoadCached(), DateTime.Now);
                Save(result);
                return result;
            }
        }

        internal static HighlightsResult MergeResults(List<SourceResult> fetched, HighlightsResult cached, DateTime now)
        {
            string stamp = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            HighlightsResult result = new HighlightsResult { Version = CurrentVersion, Day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), UpdatedAt = stamp };
            foreach (string name in HighlightsLayout.Sources) {
                SourceResult source = fetched.Find(delegate(SourceResult value) { return value.Name == name; }) ?? new SourceResult { Name = name, Error = "尚未获取" };
                HighlightSourceState state = new HighlightSourceState { Name = name, Provider = source.Provider, UpdatedAt = stamp, Error = source.Error };
                if (source.Items != null && source.Items.Count > 0 && String.IsNullOrEmpty(source.Error)) {
                    foreach (HighlightItem item in source.Items)
                        if (item.Source == name && SafeHttpsUrl(item.Url) != null) result.Items.Add(item);
                } else {
                    state.UpdatedAt = null;
                    if (cached != null && cached.Items != null) {
                        HighlightSourceState previous = cached.Sources == null ? null : cached.Sources.Find(delegate(HighlightSourceState value) { return value.Name == name; });
                        DateTime cachedAt;
                        string previousStamp = previous == null ? cached.UpdatedAt : previous.UpdatedAt;
                        if (DateTime.TryParse(previousStamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out cachedAt) && cachedAt >= now.AddDays(-7)) {
                            foreach (HighlightItem item in cached.Items)
                                if (item.Source == name && SafeHttpsUrl(item.Url) != null) { result.Items.Add(item); state.IsCached = true; }
                            if (state.IsCached) { state.UpdatedAt = previousStamp; state.Provider = previous == null ? "历史来源" : previous.Provider; }
                        }
                    }
                    state.Error = String.IsNullOrEmpty(source.Error) ? "没有取到可用内容" : source.Error;
                    result.Errors.Add(name + "：" + state.Error + (state.IsCached ? "，显示 " + state.UpdatedAt + " 的缓存" : "，暂无缓存"));
                }
                result.Sources.Add(state);
            }
            result.Overview = BuildOverview(result.Items);
            return result;
        }

        internal static SourceResult FetchSource(string name)
        {
            if (name == "微博热搜") return SafeFetch(name, "微博热搜", FetchWeibo);
            if (name == "B站热门") return SafeFetch(name, "哔哩哔哩热门", FetchBilibili);
            if (name == "NBA") return SafeFetch(name, "虎扑 NBA", FetchHupuNba);
            if (name == "GitHub") return SafeFetch(name, "GitHub API · 近90天新项目 / 累计 Fork", FetchGitHub);
            string query = name == "AI" ? "人工智能 大模型" : name == "股票" ? "A股 股市" :
                name == "汽车" ? "汽车 新车" : name == "游戏" ? "电子游戏 新游" :
                name == "美女·时尚" ? "女星 时尚 写真" : "体育 赛事";
            SourceResult primary = SafeFetch(name, "Bing 新闻", delegate {
                return ParseRss(name, Download("https://www.bing.com/news/search?q=" + Uri.EscapeDataString(query) + "&format=rss&cc=us&setlang=zh-hans", "https://www.bing.com/"), "Bing 新闻");
            });
            if (primary.Items.Count >= 3) return primary;
            SourceResult fallback = SafeFetch(name, "Google 新闻（备用）", delegate {
                return ParseRss(name, Download("https://news.google.com/rss/search?q=" + Uri.EscapeDataString(query + " when:7d") + "&hl=zh-CN&gl=CN&ceid=CN%3Azh-Hans", "https://news.google.com/"), "Google 新闻");
            });
            if (primary.Items.Count > 0) {
                if (fallback.Items.Count > 0) {
                    foreach (HighlightItem item in fallback.Items) {
                        if (!primary.Items.Exists(delegate(HighlightItem earlier) { return earlier.Url == item.Url || earlier.Title == item.Title; })) primary.Items.Add(item);
                        if (primary.Items.Count >= 6) break;
                    }
                    primary.Provider = "Bing / Google 新闻";
                }
                return primary;
            }
            if (fallback.Items.Count == 0) fallback.Error = "Bing / Google 新闻均暂不可用（" + fallback.Error + "）";
            return fallback;
        }

        private static SourceResult SafeFetch(string name, string provider, Func<List<HighlightItem>> loader)
        {
            SourceResult result = new SourceResult { Name = name, Provider = provider };
            try
            {
                result.Items = loader();
                if (result.Items.Count == 0) result.Error = "没有取到内容";
            }
            catch (Exception ex)
            {
                result.Error = FriendlyError(ex, name);
            }
            return result;
        }

        private static string FriendlyError(Exception ex, string name)
        {
            WebException web = ex as WebException;
            if (web != null && web.Status == WebExceptionStatus.Timeout) return "连接超时";
            HttpWebResponse response = web == null ? null : web.Response as HttpWebResponse;
            if (name == "GitHub" && response != null && ((int)response.StatusCode == 403 || (int)response.StatusCode == 429))
                return "GitHub 匿名访问受限，请稍后重试（搜索接口最多10次/分钟）";
            if (ex is FormatException || ex is XmlException) return "来源格式变化，暂时无法读取";
            return "暂时无法连接";
        }

        private static List<HighlightItem> FetchBilibili()
        {
            string json = Download(
                "https://api.bilibili.com/x/web-interface/popular?ps=12&pn=1",
                "https://www.bilibili.com/");
            Dictionary<string, object> root = AsDictionary(Json.DeserializeObject(json));
            Dictionary<string, object> data = AsDictionary(Get(root, "data"));
            object[] list = AsArray(Get(data, "list"));
            List<HighlightItem> result = new List<HighlightItem>();

            for (int i = 0; i < list.Length && result.Count < 6; i++)
            {
                Dictionary<string, object> item = AsDictionary(list[i]);
                string title = Text(item, "title");
                string bvid = Text(item, "bvid");
                if (String.IsNullOrWhiteSpace(title) || String.IsNullOrWhiteSpace(bvid)) continue;
                Dictionary<string, object> owner = AsDictionary(Get(item, "owner"));
                Dictionary<string, object> stat = AsDictionary(Get(item, "stat"));
                string description = Clean(Text(item, "desc"));
                string author = Text(owner, "name");
                string summary = String.IsNullOrWhiteSpace(description)
                    ? "UP主：" + author
                    : Shorten(description, 86);
                result.Add(new HighlightItem {
                    Source = "B站热门",
                    Title = title,
                    Summary = summary,
                    Url = "https://www.bilibili.com/video/" + bvid,
                    Hot = "播放 " + FormatNumber(Get(stat, "view"))
                });
            }
            return result;
        }

        private static List<HighlightItem> FetchWeibo()
        {
            string json = Download("https://weibo.com/ajax/side/hotSearch", "https://weibo.com/");
            Dictionary<string, object> root = AsDictionary(Json.DeserializeObject(json));
            Dictionary<string, object> data = AsDictionary(Get(root, "data"));
            object[] list = AsArray(Get(data, "realtime"));
            List<HighlightItem> result = new List<HighlightItem>();

            for (int i = 0; i < list.Length && result.Count < 8; i++)
            {
                Dictionary<string, object> item = AsDictionary(list[i]);
                string word = Text(item, "word");
                if (String.IsNullOrWhiteSpace(word)) continue;
                string title = Text(item, "word_scheme");
                if (String.IsNullOrWhiteSpace(title)) title = word;
                string label = Text(item, "label_name");
                string hot = FormatNumber(Get(item, "num"));
                string summary = "热搜第 " + (result.Count + 1).ToString(CultureInfo.InvariantCulture) + " 位";
                if (!String.IsNullOrWhiteSpace(label)) summary += " · " + label;
                result.Add(new HighlightItem {
                    Source = "微博热搜",
                    Title = title,
                    Summary = summary,
                    Url = "https://s.weibo.com/weibo?q=" + Uri.EscapeDataString(word),
                    Hot = "热度 " + hot
                });
            }
            return result;
        }

        internal static List<HighlightItem> FetchHupuNba()
        {
            return ParseHupuNba(Download("https://voice.hupu.com/nba", "https://www.hupu.com/"));
        }

        internal static List<HighlightItem> ParseHupuNba(string html)
        {
            Match embedded = Regex.Match(html ?? String.Empty,
                @"<script\b[^>]*\bid\s*=\s*['""]__NEXT_DATA__['""][^>]*>(.*?)</script\s*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!embedded.Success) throw new FormatException("虎扑页面暂时无法解析");
            Dictionary<string, object> root = AsDictionary(Json.DeserializeObject(embedded.Groups[1].Value));
            Dictionary<string, object> props = AsDictionary(Get(root, "props"));
            Dictionary<string, object> page = AsDictionary(Get(props, "pageProps"));
            object[] list = AsArray(Get(page, "data"));
            List<HighlightItem> result = new List<HighlightItem>();
            HashSet<string> links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Length && result.Count < 6; i++)
            {
                Dictionary<string, object> item = AsDictionary(list[i]);
                string title = Clean(Text(item, "title"));
                string link = Text(item, "url");
                Uri uri;
                // Some feed entries only open the mobile app; keep browser-accessible Hupu stories.
                if (String.IsNullOrWhiteSpace(title) || !Uri.TryCreate(link, UriKind.Absolute, out uri) ||
                    (uri.Scheme != "https" && uri.Scheme != "http") ||
                    !(uri.Host.Equals("hupu.com", StringComparison.OrdinalIgnoreCase) ||
                      uri.Host.EndsWith(".hupu.com", StringComparison.OrdinalIgnoreCase))) continue;
                if (uri.Scheme == "http") link = new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri.AbsoluteUri;
                if (!links.Add(link)) continue;
                string time = Clean(Text(item, "time"));
                string origin = Clean(Text(item, "origin"));
                string summary = "虎扑 NBA";
                if (!String.IsNullOrWhiteSpace(time)) summary += " · " + time;
                if (!String.IsNullOrWhiteSpace(origin) && origin != "虎扑") summary += " · 原始来源：" + origin;
                result.Add(new HighlightItem {
                    Source = "NBA", Title = Shorten(title, 90), Summary = summary,
                    Url = link, Hot = "虎扑 · " + FormatNumber(Get(item, "replies")) + " 回复"
                });
            }
            return result;
        }

        internal static List<HighlightItem> FetchGitHub()
        {
            string query = "created:>=" + DateTime.Today.AddDays(-90).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " fork:false archived:false";
            return ParseGitHub(Download("https://api.github.com/search/repositories?q=" + Uri.EscapeDataString(query) + "&sort=forks&order=desc&per_page=6", "https://github.com/"));
        }

        internal static List<HighlightItem> ParseGitHub(string json)
        {
            Dictionary<string, object> root = AsDictionary(Json.DeserializeObject(json));
            if (!root.ContainsKey("items")) throw new FormatException("GitHub 数据缺少项目列表");
            List<Dictionary<string, object>> projects = new List<Dictionary<string, object>>();
            foreach (object value in AsArray(Get(root, "items"))) projects.Add(AsDictionary(value));
            projects.Sort(delegate(Dictionary<string, object> a, Dictionary<string, object> b) { return Number(Get(b, "forks_count")).CompareTo(Number(Get(a, "forks_count"))); });
            List<HighlightItem> result = new List<HighlightItem>();
            HashSet<string> links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Dictionary<string, object> item in projects) {
                string link = SafeHttpsUrl(Text(item, "html_url"));
                if (link == null || new Uri(link).Host != "github.com" || Get(item, "private") as bool? == true ||
                    Get(item, "fork") as bool? == true || Get(item, "archived") as bool? == true || !links.Add(link)) continue;
                string title = Clean(Text(item, "full_name"));
                if (String.IsNullOrEmpty(title)) continue;
                string language = Clean(Text(item, "language"));
                result.Add(new HighlightItem {
                    Source = "GitHub", Title = Shorten(title, 90), Url = link,
                    Summary = Shorten(Clean(Text(item, "description")), 100),
                    Hot = "Fork " + FormatNumber(Get(item, "forks_count")) + "  ·  Star " + FormatNumber(Get(item, "stargazers_count")) + "  ·  " + (String.IsNullOrEmpty(language) ? "未标注语言" : language)
                });
                if (result.Count == 6) break;
            }
            return result;
        }

        internal static List<HighlightItem> ParseRss(string sourceName, string xml, string provider, DateTimeOffset? referenceTime = null)
        {
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 };
            XDocument document;
            using (StringReader input = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(input, settings)) document = XDocument.Load(reader);
            if (document.Root == null || document.Root.Name.LocalName != "rss") throw new FormatException("并非新闻 RSS 数据");
            List<HighlightItem> result = new List<HighlightItem>();
            HashSet<string> links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTimeOffset now = referenceTime ?? DateTimeOffset.UtcNow;
            List<XElement> entries = new List<XElement>(document.Descendants("item"));
            entries.Sort(delegate(XElement a, XElement b) { return RssDate(b).CompareTo(RssDate(a)); });
            foreach (XElement node in entries)
            {
                if (result.Count >= 6) break;
                DateTimeOffset published = RssDate(node);
                // News search also returns evergreen stories. Never present years-old results as today's news.
                if (published < now.AddDays(-7) || published > now.AddDays(1)) continue;
                string title = Clean(Value(node.Element("title")));
                string link = NewsUrl(Value(node.Element("link")));
                string publisher = String.Empty;
                foreach (XElement child in node.Elements()) if (child.Name.LocalName == "source" || child.Name.LocalName == "Source") publisher = Clean(child.Value);
                string description = Clean(Value(node.Element("description")));
                if (String.IsNullOrWhiteSpace(title) || link == null || !links.Add(link)) continue;
                string summary = description;
                if (String.IsNullOrWhiteSpace(summary) || summary.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                    summary = "来源：" + (String.IsNullOrWhiteSpace(publisher) ? provider : publisher);
                string stamp = " · " + published.ToOffset(TimeSpan.FromHours(8)).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
                result.Add(new HighlightItem {
                    Source = sourceName,
                    Title = Shorten(title, 96),
                    Summary = Shorten(summary, 90),
                    Url = link,
                    Hot = (String.IsNullOrWhiteSpace(publisher) ? provider : publisher) + stamp
                });
            }
            return result;
        }

        private static DateTimeOffset RssDate(XElement item) { DateTimeOffset date; return DateTimeOffset.TryParse(Value(item.Element("pubDate")), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date) ? date : DateTimeOffset.MinValue; }

        private static double Number(object value) { double number; return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out number) ? number : 0; }

        internal static string SafeHttpsUrl(string value)
        {
            Uri uri;
            if (String.IsNullOrWhiteSpace(value) || value.Length > 4096 || !Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !String.IsNullOrEmpty(uri.UserInfo) || uri.IsLoopback ||
                uri.HostNameType != UriHostNameType.Dns || uri.Host.IndexOf('.') < 0 || !uri.IsDefaultPort) return null;
            return uri.AbsoluteUri;
        }

        internal static string NewsUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)) return null;
            if (uri.Scheme == "http") value = new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri.AbsoluteUri;
            uri = new Uri(value);
            if (uri.Host == "www.bing.com" && uri.AbsolutePath.StartsWith("/news/apiclick", StringComparison.OrdinalIgnoreCase)) {
                foreach (string field in uri.Query.TrimStart('?').Split('&')) {
                    int separator = field.IndexOf('=');
                    if (separator > 0 && field.Substring(0, separator).Equals("url", StringComparison.OrdinalIgnoreCase))
                        return NewsUrl(Uri.UnescapeDataString(field.Substring(separator + 1)));
                }
            }
            return SafeHttpsUrl(value);
        }

        private static string BuildOverview(List<HighlightItem> items)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < HighlightsLayout.Sources.Length; i++)
            {
                for (int j = 0; j < items.Count; j++)
                {
                    if (items[j].Source != HighlightsLayout.Sources[i]) continue;
                    parts.Add(HighlightsLayout.Sources[i] + "：" + Shorten(items[j].Title, 24));
                    break;
                }
            }
            return parts.Count == 0 ? "暂时没有今日内容。" : "今天先看  " + String.Join("  ·  ", parts.ToArray());
        }

        private static void Save(HighlightsResult result)
        {
            try { PetStorage.WriteAllTextAtomic(PetSettings.CachePath, Json.Serialize(result)); }
            catch (Exception error) { PetStorage.Log("highlights-cache", error); }
        }

        private static string Download(string url, string referer)
        {
            using (TimedWebClient client = new TimedWebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) GenshinLauncher/2.3";
                client.Headers[HttpRequestHeader.AcceptLanguage] = "zh-CN,zh;q=0.9";
                if (!String.IsNullOrWhiteSpace(referer)) client.Headers[HttpRequestHeader.Referer] = referer;
                return client.DownloadString(url);
            }
        }

        private static Dictionary<string, object> AsDictionary(object value)
        {
            return value as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static object[] AsArray(object value)
        {
            return value as object[] ?? new object[0];
        }

        private static object Get(Dictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value) ? value : null;
        }

        private static string Text(Dictionary<string, object> dictionary, string key)
        {
            object value = Get(dictionary, key);
            return value == null ? String.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string Value(XElement element)
        {
            return element == null ? String.Empty : element.Value;
        }

        private static string FormatNumber(object value)
        {
            double number;
            if (value == null || !Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
                return "--";
            if (number >= 100000000) return (number / 100000000d).ToString("0.#", CultureInfo.InvariantCulture) + "亿";
            if (number >= 10000) return (number / 10000d).ToString("0.#", CultureInfo.InvariantCulture) + "万";
            return number.ToString("0", CultureInfo.InvariantCulture);
        }

        private static string Clean(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;
            string value = WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", " "));
            return Regex.Replace(value, "\\s+", " ").Trim();
        }

        private static string Shorten(string text, int maxLength)
        {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;
            string value = text.Trim();
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 1) + "…";
        }
    }

}
