using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinImaging = Windows.Graphics.Imaging;
using WinOcr = Windows.Media.Ocr;

namespace ClaudePet
{
    // Stats read from a character attribute screenshot. Null means "not found".
    internal sealed class PanelStatReading
    {
        internal double? Attack, Health, Defense, ElementalMastery, CritRate, CritDamage, EnergyRecharge, PhysicalBonus;
        internal readonly Dictionary<string, double> ElementBonus = new Dictionary<string, double>();
        internal readonly List<string> Lines = new List<string>();
        internal string Engine = "";

        internal int Count
        {
            get
            {
                int count = ElementBonus.Count;
                foreach (double? value in new[] { Attack, Health, Defense, ElementalMastery, CritRate, CritDamage, EnergyRecharge, PhysicalBonus })
                    if (value.HasValue) count++;
                return count;
            }
        }

        internal double? CoreFor(string scaling)
        {
            return scaling == "health" ? Health : scaling == "defense" ? Defense : Attack;
        }

        // Never substitute a different element's bonus for an unreadable one.
        internal double? BonusFor(string element)
        {
            string key = (element ?? "").Trim();
            if (key.Length > 0 && ElementBonus.ContainsKey(key.Substring(0, 1))) return ElementBonus[key.Substring(0, 1)];
            return key == "物理" ? PhysicalBonus : null;
        }
    }

    // Extension point for "read stats from an image". The built-in
    // implementation uses Windows OCR on this machine. Another implementation
    // (for example a vision model) only needs to return a PanelStatReading;
    // CombatPanelTextParser can turn any recognised text lines into one.
    internal interface ICombatPanelImageExtractor
    {
        string Name { get; }
        // Null when ready; otherwise a user-facing reason.
        string Unavailable();
        // Must read the BitmapSource synchronously before the first await.
        Task<PanelStatReading> ExtractAsync(BitmapSource image);
    }

    internal static class CombatPanelTextParser
    {
        private static readonly Dictionary<string, string> EnglishElements = new Dictionary<string, string> {
            { "pyro", "火" }, { "hydro", "水" }, { "anemo", "风" }, { "electro", "雷" }, { "dendro", "草" }, { "cryo", "冰" }, { "geo", "岩" }
        };

        // Order matters: longer labels come before labels they contain.
        private static readonly string[][] Labels = {
            new[] { "Element", @"(?<e>[火水风雷草冰岩])元素伤害加成|(?<e>pyro|hydro|anemo|electro|dendro|cryo|geo)\s*dmg\s*bonus" },
            new[] { "Physical", @"物理伤害加成|physical\s*dmg\s*bonus" },
            new[] { "EnergyRecharge", @"元素充能效率|充能效率|energy\s*recharge" },
            new[] { "ElementalMastery", @"元素精通|elemental\s*mastery" },
            new[] { "CritRate", @"暴击率|crit\s*rate" },
            new[] { "CritDamage", @"暴击伤害|crit\s*dmg" },
            new[] { "Health", @"生命值上限|生命值|\bmax\s*hp\b|\bhp\b" },
            new[] { "Attack", @"攻击力|\batk\b" },
            new[] { "Defense", @"防御力|\bdef\b" },
            new[] { "Ignore", @"治疗加成|受治疗加成|护盾强效|冷却缩减|体力" }
        };

        private static readonly Regex Number = new Regex(@"(?<![\d.])(\d{1,3}(?:,\d{3})+|\d+)(\.\d+)?", RegexOptions.Compiled);
        private static readonly Regex Sum = new Regex(@"^\s*(\d[\d,]*(?:\.\d+)?)\s*\+\s*(\d[\d,]*(?:\.\d+)?)", RegexOptions.Compiled);
        private static readonly Regex Cjk = new Regex(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

        internal static string Normalize(string line)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in line ?? "") {
                if (c >= '\uFF01' && c <= '\uFF5E') builder.Append((char)(c - 0xFEE0));
                else if (c == '\u3000') builder.Append(' ');
                else if (c == '\u3002') builder.Append('.');
                else builder.Append(c);
            }
            string text = builder.ToString();
            text = Regex.Replace(text, @"(?<=[\u4e00-\u9fff])\s+(?=[\u4e00-\u9fff])", "");
            text = Regex.Replace(text, @"(?<=\d)\s*[·•]\s*(?=\d)", ".");
            text = Regex.Replace(text, @"(?<![\d.])(\d{1,3})\s+(\d{3})(?![\d.%])", "$1,$2");
            text = Regex.Replace(text, @"(?<=\d)\s*([,.])\s*(?=\d)", "$1");
            text = Regex.Replace(text, @"(?<=\d)\s+(?=%)", "");
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        internal static PanelStatReading Parse(IList<string> rawLines)
        {
            PanelStatReading reading = new PanelStatReading();
            List<string> lines = new List<string>();
            foreach (string raw in rawLines ?? new string[0]) { string line = Normalize(raw); if (line.Length > 0) lines.Add(line); }
            reading.Lines.AddRange(lines);
            for (int i = 0; i < lines.Count; i++) {
                string line = lines[i];
                List<Match> found = new List<Match>();
                List<string> kinds = new List<string>();
                foreach (string[] label in Labels)
                    foreach (Match match in Regex.Matches(line, label[1], RegexOptions.IgnoreCase)) {
                        bool overlaps = false;
                        foreach (Match other in found)
                            if (match.Index < other.Index + other.Length && other.Index < match.Index + match.Length) overlaps = true;
                        if (!overlaps) { found.Add(match); kinds.Add(label[0]); }
                    }
                for (int k = 0; k < found.Count; k++) {
                    if (kinds[k] == "Ignore") continue;
                    int start = found[k].Index + found[k].Length, end = line.Length;
                    foreach (Match other in found) if (other.Index >= start && other.Index < end) end = other.Index;
                    double? value = ReadValue(line.Substring(start, end - start));
                    if (!value.HasValue && found.Count == 1 && i + 1 < lines.Count && IsValueOnly(lines[i + 1]))
                        value = ReadValue(lines[i + 1]);
                    if (value.HasValue) Store(reading, kinds[k], found[k], value.Value);
                }
            }
            return reading;
        }

        private static bool IsValueOnly(string line) { return !Cjk.IsMatch(line) && Number.IsMatch(line) && !Regex.IsMatch(line, "[A-Za-z]{3,}"); }

        private static double? ReadValue(string text)
        {
            Match sum = Sum.Match(text);
            if (sum.Success) return Parse(sum.Groups[1].Value) + Parse(sum.Groups[2].Value);
            Match match = Number.Match(text);
            if (!match.Success) return null;
            return Parse(match.Value);
        }

        private static double Parse(string value)
        {
            return Double.Parse(value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void Store(PanelStatReading reading, string kind, Match label, double value)
        {
            switch (kind) {
                case "Attack": if (!reading.Attack.HasValue && value >= 1 && value <= 10000) reading.Attack = value; break;
                case "Health": if (!reading.Health.HasValue && value >= 1 && value <= 200000) reading.Health = value; break;
                case "Defense": if (!reading.Defense.HasValue && value >= 1 && value <= 10000) reading.Defense = value; break;
                case "ElementalMastery": if (!reading.ElementalMastery.HasValue && value <= 5000) reading.ElementalMastery = value; break;
                case "CritRate": if (!reading.CritRate.HasValue && value <= 150) reading.CritRate = value; break;
                case "CritDamage": if (!reading.CritDamage.HasValue && value <= 1000) reading.CritDamage = value; break;
                case "EnergyRecharge": if (!reading.EnergyRecharge.HasValue && value >= 50 && value <= 1000) reading.EnergyRecharge = value; break;
                case "Physical": if (!reading.PhysicalBonus.HasValue && value <= 500) reading.PhysicalBonus = value; break;
                case "Element":
                    string element = label.Groups["e"].Value.ToLowerInvariant();
                    if (EnglishElements.ContainsKey(element)) element = EnglishElements[element];
                    if (!reading.ElementBonus.ContainsKey(element) && value <= 500) reading.ElementBonus[element] = value;
                    break;
            }
        }
    }

    internal sealed class WindowsOcrPanelExtractor : ICombatPanelImageExtractor
    {
        private sealed class Prepared { internal byte[] Pixels; internal int Width, Height; }

        public string Name { get { return "Windows 本机文字识别"; } }

        private static WinOcr.OcrEngine CreateEngine()
        {
            foreach (string tag in new[] { "zh-Hans-CN", "zh-Hans", "zh-CN" }) {
                Windows.Globalization.Language language = new Windows.Globalization.Language(tag);
                if (WinOcr.OcrEngine.IsLanguageSupported(language)) return WinOcr.OcrEngine.TryCreateFromLanguage(language);
            }
            return WinOcr.OcrEngine.TryCreateFromUserProfileLanguages();
        }

        internal static string LanguageTag()
        {
            try { WinOcr.OcrEngine engine = CreateEngine(); return engine == null ? "" : engine.RecognizerLanguage.LanguageTag; }
            catch { return ""; }
        }

        public string Unavailable()
        {
            try {
                return CreateEngine() == null
                    ? "系统没有可用的文字识别语言。请在 设置 → 时间和语言 → 语言 中添加“中文（简体）”并安装“光学字符识别”。"
                    : null;
            } catch (Exception error) { return "当前系统不支持 Windows 文字识别：" + error.Message; }
        }

        public async Task<PanelStatReading> ExtractAsync(BitmapSource image)
        {
            if (image == null) throw new ArgumentNullException("image");
            Prepared plain = Prepare(image, false), inverted = Prepare(image, true);
            WinOcr.OcrEngine engine = CreateEngine();
            if (engine == null) throw new InvalidOperationException(Unavailable());
            PanelStatReading first = CombatPanelTextParser.Parse(await Recognize(engine, plain).ConfigureAwait(false));
            first.Engine = Name + " · " + engine.RecognizerLanguage.LanguageTag;
            if (first.Count >= 7) return first;
            PanelStatReading second = CombatPanelTextParser.Parse(await Recognize(engine, inverted).ConfigureAwait(false));
            second.Engine = first.Engine + " · 反色";
            return second.Count > first.Count ? second : first;
        }

        // Game panels use light text on dark art. Pass 1 keeps the image;
        // pass 2 is grayscale-inverted, which OCR often reads better.
        private static Prepared Prepare(BitmapSource source, bool invert)
        {
            double longest = Math.Max(source.PixelWidth, source.PixelHeight);
            double limit = Math.Min(4000, WinOcr.OcrEngine.MaxImageDimension);
            double scale = Math.Min(limit / Math.Max(1, longest), source.PixelWidth < 1400 ? 2.0 : 1.0);
            BitmapSource scaled = Math.Abs(scale - 1) < 0.01 ? source : new TransformedBitmap(source, new ScaleTransform(scale, scale));
            FormatConvertedBitmap bgra = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth, height = bgra.PixelHeight, stride = width * 4;
            byte[] pixels = new byte[stride * height];
            bgra.CopyPixels(pixels, stride, 0);
            if (invert)
                for (int i = 0; i < pixels.Length; i += 4) {
                    byte gray = (byte)(255 - (pixels[i] * 0.114 + pixels[i + 1] * 0.587 + pixels[i + 2] * 0.299));
                    pixels[i] = pixels[i + 1] = pixels[i + 2] = gray; pixels[i + 3] = 255;
                }
            else
                for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            return new Prepared { Pixels = pixels, Width = width, Height = height };
        }

        // Rebuilds visual rows from word boxes, so a label on the left and its
        // value far to the right end up on the same line.
        private static async Task<List<string>> Recognize(WinOcr.OcrEngine engine, Prepared image)
        {
            WinOcr.OcrResult result;
            using (WinImaging.SoftwareBitmap bitmap = WinImaging.SoftwareBitmap.CreateCopyFromBuffer(image.Pixels.AsBuffer(),
                WinImaging.BitmapPixelFormat.Bgra8, image.Width, image.Height, WinImaging.BitmapAlphaMode.Premultiplied))
                result = await engine.RecognizeAsync(bitmap).AsTask().ConfigureAwait(false);
            List<double[]> boxes = new List<double[]>();
            List<string> words = new List<string>();
            foreach (WinOcr.OcrLine line in result.Lines)
                foreach (WinOcr.OcrWord word in line.Words) {
                    var rect = word.BoundingRect;
                    boxes.Add(new[] { rect.X, rect.Y + rect.Height / 2, rect.Height });
                    words.Add(word.Text);
                }
            List<int> indexes = new List<int>();
            for (int i = 0; i < words.Count; i++) indexes.Add(i);
            indexes.Sort(delegate(int a, int b) { return boxes[a][1].CompareTo(boxes[b][1]); });
            List<List<int>> rows = new List<List<int>>();
            List<double> centers = new List<double>(), heights = new List<double>();
            foreach (int index in indexes) {
                int row = rows.Count - 1;
                if (row >= 0 && Math.Abs(boxes[index][1] - centers[row]) < 0.5 * Math.Max(boxes[index][2], heights[row])) {
                    rows[row].Add(index);
                    centers[row] = (centers[row] * (rows[row].Count - 1) + boxes[index][1]) / rows[row].Count;
                    heights[row] = Math.Max(heights[row], boxes[index][2]);
                } else { rows.Add(new List<int> { index }); centers.Add(boxes[index][1]); heights.Add(boxes[index][2]); }
            }
            List<string> lines = new List<string>();
            foreach (List<int> row in rows) {
                row.Sort(delegate(int a, int b) { return boxes[a][0].CompareTo(boxes[b][0]); });
                List<string> parts = new List<string>();
                foreach (int index in row) parts.Add(words[index]);
                lines.Add(String.Join(" ", parts.ToArray()));
            }
            return lines;
        }
    }

    internal static class CombatPanelImport
    {
        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp" };
        internal const long MaximumBytes = 30L * 1024 * 1024;

        // Replace to plug in another extractor (tests, or a future vision model).
        internal static ICombatPanelImageExtractor Extractor = new WindowsOcrPanelExtractor();

        internal static BitmapSource LoadFile(string path)
        {
            string extension = (Path.GetExtension(path) ?? "").ToLowerInvariant();
            if (Array.IndexOf(Extensions, extension) < 0) throw new InvalidDataException("只支持 PNG、JPG 或 BMP 截图。");
            FileInfo info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("找不到图片。", path);
            if (info.Length > MaximumBytes) throw new InvalidDataException("图片超过 30 MB。");
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.UriSource = new Uri(info.FullName, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }

        internal static BitmapSource FromClipboard()
        {
            if (!Clipboard.ContainsImage()) return null;
            BitmapSource image = Clipboard.GetImage();
            if (image != null && image.CanFreeze) image.Freeze();
            return image;
        }
    }
}
