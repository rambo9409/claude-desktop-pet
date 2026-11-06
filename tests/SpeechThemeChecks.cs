using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudePet;

internal static class SpeechThemeChecks
{
    private static readonly string[] Characters = { "kolo", "gpt6a", "deepseek", "hutao", "ganyu", "varesa" };
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static RenderTargetBitmap Render(SpeechWindow window, Size work)
    {
        window.ConstrainToWorkArea(work);
        FrameworkElement content = (FrameworkElement)window.Content;
        WpfTestRender.Drain();
        content.Measure(new Size(Math.Max(1, work.Width - 10), Math.Max(1, work.Height - 10)));
        content.Arrange(new Rect(content.DesiredSize)); content.UpdateLayout();
        Require(content.ActualWidth <= work.Width - 10 + .01 && content.ActualHeight <= work.Height - 10 + .01,
            "Speech exceeded its display work area: " + window.ThemeKey);
        RenderTargetBitmap bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(content.ActualWidth)),
            Math.Max(1, (int)Math.Ceiling(content.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        WpfTestRender.Render(bitmap, content);
        return bitmap;
    }
    private static void Save(BitmapSource bitmap, string path)
    {
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream output = File.Create(path)) encoder.Save(output);
    }
    private static string Hash(BitmapSource bitmap)
    {
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(pixels));
    }
    private static double Channel(byte value) { double s = value / 255.0; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
    private static double Light(Color c) { return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B); }
    private static double Contrast(Color first, Color second)
    { double a = Light(first), b = Light(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05); }
    private static void Contact(List<BitmapSource> shortImages, List<BitmapSource> longImages, string path, bool dark)
    {
        DrawingVisual visual = new DrawingVisual();
        using (DrawingContext draw = visual.RenderOpen()) {
            draw.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#202B3B" : "#E8EDF3")), null, new Rect(0, 0, 1080, 640));
            for (int i = 0; i < Characters.Length; i++) {
                double x = i % 3 * 360, y = i / 3 * 320;
                FormattedText title = new FormattedText(PetWindow.CharacterDisplayName(Characters[i]), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 15,
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#DCE6F5" : "#41556E")), 1.0);
                draw.DrawText(title, new Point(x + 16, y + 12));
                draw.DrawImage(shortImages[i], new Rect(x + 12, y + 44, shortImages[i].Width, shortImages[i].Height));
                draw.DrawImage(longImages[i], new Rect(x + 12, y + 130, longImages[i].Width, longImages[i].Height));
            }
        }
        RenderTargetBitmap output = new RenderTargetBitmap(1080, 640, 96, 96, PixelFormats.Pbgra32);
        WpfTestRender.Render(output, visual); Save(output, path);
    }
    internal static void Run()
    {
        string directory = Path.Combine("artifacts", "v217"); Directory.CreateDirectory(directory);
        List<BitmapSource> shortImages = new List<BitmapSource>(), longImages = new List<BitmapSource>();
        HashSet<string> distinct = new HashSet<string>();
        SpeechWindow window = new SpeechWindow();
        try {
            Require(window.Text.FontSize == 16 && window.Text.FontWeight == FontWeights.Normal,
                "Speech body must remain readable without a heavy title-like weight.");
            Require(!window.ShowActivated && !window.ShowInTaskbar && !window.Focusable && !window.IsHitTestVisible,
                "Speech must not intercept focus or input.");
            foreach (string character in Characters) {
                window.ApplyCharacterTheme(character);
                Require(window.ThemeKey == character, "Theme did not follow character.");
                SpeechPalette palette = SpeechPalette.ForCharacter(character);
                Color foreground = ((SolidColorBrush)window.Text.Foreground).Color;
                foreach (GradientStop stop in ((GradientBrush)palette.Fill).GradientStops)
                    Require(Contrast(foreground, stop.Color) >= 4.5, "Speech contrast is too low: " + character);
                object content = window.Content, foregroundBrush = window.Text.Foreground;
                window.ApplyCharacterTheme(character);
                Require(Object.ReferenceEquals(content, window.Content) && Object.ReferenceEquals(foregroundBrush, window.Text.Foreground),
                    "Repeated theme application rebuilt the visible subtitle.");
                window.Text.Text = "今天也要加油。";
                RenderTargetBitmap shortImage = Render(window, new Size(1920, 1080));
                Require(distinct.Add(Hash(shortImage)), "Two characters share an identical speech surface.");
                shortImages.Add(shortImage); Save(shortImage, Path.Combine(directory, "speech-" + character + "-short.png"));
                window.Text.Text = "这段话会跟着角色一起移动。\n走路、拖拽和切换屏幕时，字幕依然清楚，靠近边缘也能读完。";
                RenderTargetBitmap longImage = Render(window, new Size(1920, 1080));
                Require(longImage.Height > shortImage.Height, "Long speech failed to wrap.");
                longImages.Add(longImage); Save(longImage, Path.Combine(directory, "speech-" + character + "-long.png"));
                window.Text.Text = "https://example.com/" + new string('x', 240) + "\n连续文字也要在窄屏上正常换行。";
                RenderTargetBitmap narrow = Render(window, new Size(160, 220));
                Save(narrow, Path.Combine(directory, "speech-" + character + "-narrow.png"));
                Render(window, new Size(100, 90));
            }
            window.ApplyCharacterTheme("unrecognized"); Require(window.ThemeKey == "kolo", "Unknown theme must fall back to Kolo.");
            Contact(shortImages, longImages, Path.Combine(directory, "speech-themes-v217-light.png"), false);
            Contact(shortImages, longImages, Path.Combine(directory, "speech-themes-v217-dark.png"), true);
        } finally { window.Close(); }
    }
}
