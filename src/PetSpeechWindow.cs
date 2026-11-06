using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    // A separate window keeps speech readable even when the pet's Viewbox is tiny.
    // It is owned by the pet and cannot take focus or intercept mouse interaction.
    internal sealed class SpeechWindow : Window
    {
        internal readonly TextBlock Text;
        private readonly TextBlock speaker;
        private readonly Grid layout;
        private readonly SpeechCard card;
        private string themeKey;
        internal string ThemeKey { get { return themeKey; } }

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
            SnapsToDevicePixels = true;
            Text = new TextBlock {
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 16,
                LineHeight = 24,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Left,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            speaker = new TextBlock {
                FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 10.5,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(15, 0, 22, 4),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            layout = new Grid { Margin = new Thickness(18, 10, 18, 17) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(speaker); Grid.SetRow(Text, 1); layout.Children.Add(Text);
            card = new SpeechCard { Child = layout, MinWidth = 132, MaxWidth = 336, ClipToBounds = true };
            Content = card;
            ApplyCharacterTheme("kolo");
            ConstrainToWorkArea(new Size(1920, 1080));
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

        internal void ApplyCharacterTheme(string character)
        {
            string key = character == "gpt6a" || character == "deepseek" || character == "hutao" ||
                character == "ganyu" || character == "varesa" ? character : "kolo";
            if (themeKey == key) return;
            themeKey = key;
            SpeechPalette palette = SpeechPalette.ForCharacter(key);
            card.SetTheme(key, palette);
            Text.Foreground = palette.Text;
            speaker.Foreground = palette.Muted;
            speaker.Text = key == "gpt6a" ? "龙女" : key == "deepseek" ? "小肥鱼" : PetWindow.CharacterDisplayName(key);
            // The notebook seam and rounded aquatic card get their own insets.
            double inset = key == "kolo" ? 22 : key == "deepseek" ? 20 : 18;
            layout.Margin = new Thickness(inset, 10, inset, 17);
            RefreshTextLayout();
        }

        internal void RefreshTextLayout()
        {
            // A visible Window may defer parent invalidation until the next
            // dispatcher pass. Measure the new text/insets on the first frame.
            layout.InvalidateMeasure();
            card.InvalidateMeasure();
        }

        internal void ConstrainToWorkArea(Size work)
        {
            double width = Math.Max(1, Math.Min(336, work.Width - 10));
            double height = Math.Max(1, work.Height - 10);
            double inset = layout.Margin.Left + layout.Margin.Right;
            card.MinWidth = Math.Min(132, width);
            card.MaxWidth = width;
            card.MaxHeight = height;
            Text.MaxWidth = Math.Max(1, width - inset);
            Text.MaxHeight = Math.Max(1, Math.Min(240, height - 45));
            // If the work area changes while a phrase is visible, WPF reflows
            // the same text. No font shrinking or per-frame theme rebuilding.
            MaxWidth = width;
            MaxHeight = height;
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
                else
                    y = character.Bottom + gap;
            }
            return new Point(
                Math.Max(work.Left + gap, Math.Min(x, work.Right - speech.Width - gap)),
                Math.Max(work.Top + gap, Math.Min(y, work.Bottom - speech.Height - gap)));
        }
    }

    internal sealed class SpeechPalette
    {
        internal readonly Brush Fill, Border, Text, Muted, Accent, Detail, Shadow;
        private SpeechPalette(string first, string second, string border, string text, string muted, string accent, string detail)
        {
            LinearGradientBrush fill = new LinearGradientBrush(ColorOf(first), ColorOf(second), 90);
            fill.Freeze(); Fill = fill;
            Border = BrushOf(border); Text = BrushOf(text); Muted = BrushOf(muted);
            Accent = BrushOf(accent); Detail = BrushOf(detail);
            Shadow = BrushOf("#16302942");
        }
        private static Color ColorOf(string color) { return (Color)ColorConverter.ConvertFromString(color); }
        private static Brush BrushOf(string color) { SolidColorBrush b = new SolidColorBrush(ColorOf(color)); b.Freeze(); return b; }
        internal static SpeechPalette ForCharacter(string key)
        {
            switch (key) {
                case "gpt6a": return new SpeechPalette("#FFFEFF", "#F1ECFC", "#B8A5D3", "#393052", "#69577F", "#8D72B9", "#DDD2ED");
                case "deepseek": return new SpeechPalette("#FFFFFF", "#E9F5FF", "#8CB9DA", "#184565", "#356C93", "#3A8DC4", "#C8E7FA");
                case "hutao": return new SpeechPalette("#35262D", "#261E25", "#A56470", "#FFF0E8", "#EDBDC5", "#F1979C", "#5F3542");
                case "ganyu": return new SpeechPalette("#FEFFFF", "#E8F4FC", "#9FCBDF", "#254C67", "#47768F", "#4585AB", "#D0E8F4");
                case "varesa": return new SpeechPalette("#493357", "#302239", "#B986C3", "#FFF3FD", "#E7BEDF", "#FFB3DE", "#715080");
                default: return new SpeechPalette("#FFFCF4", "#FFF1DB", "#D4AD7A", "#5A3B2C", "#89603D", "#C58338", "#EDDBC0");
            }
        }
    }

    // Native vectors keep the six silhouettes sharp at any display scale.
    // Decorations stay in the name strip and margins, never over the words.
    internal sealed class SpeechCard : Decorator
    {
        private string key;
        private SpeechPalette palette;
        internal void SetTheme(string character, SpeechPalette value)
        { key = character; palette = value; InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (palette == null || ActualWidth < 2 || ActualHeight < 10) return;
            double w = ActualWidth, h = ActualHeight - 8;
            Pen edge = new Pen(palette.Border, 1);
            Rect body = new Rect(1, 1, Math.Max(1, w - 2), Math.Max(1, h - 2));
            // A small in-bounds shadow leaves room for the pointer without
            // an effect bitmap or a wide transparent halo around the subtitle.
            dc.DrawRoundedRectangle(palette.Shadow, null, new Rect(2, 3, Math.Max(1, w - 4), Math.Max(1, h - 1)), 11, 11);
            if (key == "hutao") DrawCutCard(dc, body, palette.Fill, edge, 7, 13);
            else if (key == "ganyu") DrawCutCard(dc, body, palette.Fill, edge, 11, 11);
            else if (key == "varesa") DrawCutCard(dc, body, palette.Fill, edge, 3, 16);
            else dc.DrawRoundedRectangle(palette.Fill, edge, body, key == "deepseek" ? 23 : key == "gpt6a" ? 17 : 9,
                key == "deepseek" ? 23 : key == "gpt6a" ? 17 : 9);

            double pointerX = key == "deepseek" ? w * .30 : key == "varesa" ? w * .72 : w * .5;
            if (key == "deepseek") {
                dc.DrawEllipse(palette.Fill, edge, new Point(pointerX, h + 1), 4, 4);
                dc.DrawEllipse(palette.Detail, null, new Point(pointerX - 9, h + 5), 2, 2);
            } else {
                DrawPolygon(dc, palette.Fill, edge, new[] { new Point(pointerX - 5, h - 2), new Point(pointerX + 6, h - 2), new Point(pointerX, h + 6) });
                dc.DrawLine(new Pen(palette.Fill, 2), new Point(pointerX - 4, h - 2), new Point(pointerX + 5, h - 2));
            }

            Pen detail = new Pen(palette.Detail, 1);
            Pen accent = new Pen(palette.Accent, 1.3);
            double badgeX = (key == "kolo" ? 22 : key == "deepseek" ? 20 : 18) + 5;
            if (key == "kolo") {
                dc.DrawLine(detail, new Point(13, 10), new Point(13, h - 10));
                for (double y = 15; y < h - 9; y += 12) dc.DrawEllipse(palette.Accent, null, new Point(7, y), 1.3, 1.3);
                dc.DrawRoundedRectangle(palette.Detail, null, new Rect(w - 35, 0, 18, 5), 1.5, 1.5);
                Flower(dc, new Point(badgeX, 17), 3.1, palette.Accent);
                dc.DrawLine(detail, new Point(23, h - 7), new Point(w - 22, h - 7));
            } else if (key == "gpt6a") {
                dc.DrawRoundedRectangle(null, detail, new Rect(5, 5, Math.Max(1, w - 10), Math.Max(1, h - 10)), 13, 13);
                Star(dc, new Point(badgeX, 17), 4.7, palette.Accent);
                Star(dc, new Point(w - 22, 16), 3.5, palette.Accent);
                dc.DrawEllipse(palette.Detail, null, new Point(w - 16, 10), 1.4, 1.4);
            } else if (key == "deepseek") {
                dc.DrawEllipse(palette.Accent, null, new Point(badgeX, 17), 4.5, 3.2);
                DrawPolygon(dc, palette.Accent, null, new[] { new Point(badgeX + 3, 17), new Point(badgeX + 7, 13), new Point(badgeX + 7, 19) });
                dc.DrawEllipse(palette.Fill, null, new Point(badgeX - 2, 16), .8, .8);
                dc.DrawEllipse(null, detail, new Point(w - 23, 17), 4, 4);
                dc.DrawEllipse(palette.Detail, null, new Point(w - 16, 10), 2, 2);
            } else if (key == "hutao") {
                dc.DrawLine(new Pen(palette.Accent, 2.5), new Point(6, 14), new Point(6, h - 14));
                Flower(dc, new Point(badgeX, 17), 3.6, palette.Accent);
                dc.DrawLine(detail, new Point(w - 41, 24), new Point(w - 16, 10));
                Flower(dc, new Point(w - 25, 15), 4.1, palette.Accent);
                dc.DrawLine(new Pen(palette.Border, 1), new Point(w - 35, h - 6), new Point(w - 13, h - 6));
            } else if (key == "ganyu") {
                Snowflake(dc, new Point(badgeX, 17), 4.5, accent);
                dc.DrawLine(detail, new Point(34, 27), new Point(w - 24, 27));
                Star(dc, new Point(w - 19, 16), 5, palette.Detail);
                dc.DrawLine(new Pen(palette.Detail, 2), new Point(14, h - 5), new Point(28, h - 5));
                dc.DrawLine(new Pen(palette.Detail, 2), new Point(w - 28, h - 5), new Point(w - 14, h - 5));
            } else {
                DrawPolygon(dc, palette.Accent, null, new[] { new Point(badgeX + 1, 11), new Point(badgeX - 4, 18),
                    new Point(badgeX, 18), new Point(badgeX - 1, 23), new Point(badgeX + 5, 15), new Point(badgeX + 1, 15) });
                for (int i = 0; i < 3; i++) dc.DrawLine(new Pen(palette.Accent, 2), new Point(w - 32 + i * 6, 20), new Point(w - 27 + i * 6, 12));
                dc.DrawLine(new Pen(palette.Accent, 2), new Point(16, h - 5), new Point(Math.Min(w - 16, 57), h - 5));
            }
        }

        private static void DrawCutCard(DrawingContext dc, Rect box, Brush fill, Pen edge, double leftCut, double rightCut)
        {
            double l = box.Left, t = box.Top, r = box.Right, b = box.Bottom;
            DrawPolygon(dc, fill, edge, new[] { new Point(l + leftCut, t), new Point(r - rightCut, t),
                new Point(r, t + rightCut), new Point(r, b - leftCut), new Point(r - leftCut, b),
                new Point(l + rightCut, b), new Point(l, b - rightCut), new Point(l, t + leftCut) });
        }
        private static void DrawPolygon(DrawingContext dc, Brush fill, Pen pen, Point[] points)
        {
            StreamGeometry shape = new StreamGeometry();
            using (StreamGeometryContext context = shape.Open()) {
                context.BeginFigure(points[0], fill != null, true);
                for (int i = 1; i < points.Length; i++) context.LineTo(points[i], true, false);
            }
            shape.Freeze(); dc.DrawGeometry(fill, pen, shape);
        }
        private static void Star(DrawingContext dc, Point center, double size, Brush brush)
        {
            DrawPolygon(dc, brush, null, new[] { new Point(center.X, center.Y - size), new Point(center.X + size * .28, center.Y - size * .25),
                new Point(center.X + size, center.Y), new Point(center.X + size * .28, center.Y + size * .25),
                new Point(center.X, center.Y + size), new Point(center.X - size * .28, center.Y + size * .25),
                new Point(center.X - size, center.Y), new Point(center.X - size * .28, center.Y - size * .25) });
        }
        private static void Flower(DrawingContext dc, Point center, double size, Brush brush)
        {
            for (int i = 0; i < 5; i++) {
                double angle = i * Math.PI * 2 / 5 - Math.PI / 2;
                dc.DrawEllipse(brush, null, new Point(center.X + Math.Cos(angle) * size * .65,
                    center.Y + Math.Sin(angle) * size * .65), size * .52, size * .52);
            }
        }
        private static void Snowflake(DrawingContext dc, Point center, double size, Pen pen)
        {
            for (int i = 0; i < 3; i++) {
                double angle = i * Math.PI / 3;
                Vector v = new Vector(Math.Cos(angle) * size, Math.Sin(angle) * size);
                dc.DrawLine(pen, center - v, center + v);
            }
        }
    }

}
