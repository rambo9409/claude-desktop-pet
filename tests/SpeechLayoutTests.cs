using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Threading;
using ClaudePet;

internal static class SpeechLayoutTests
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Field(PetWindow pet, string name) { return typeof(PetWindow).GetField(name, Private).GetValue(pet); }
    private static void Set(PetWindow pet, string name, object value) { typeof(PetWindow).GetField(name, Private).SetValue(pet, value); }
    private static object Call(PetWindow pet, string name, params object[] args) { return typeof(PetWindow).GetMethod(name, Private).Invoke(pet, args); }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    [STAThread]
    public static void Main()
    {
        // Transparent padding and faint shadows must not separate speech from the head.
        byte[] pixels = new byte[20 * 30 * 4];
        for (int y = 10; y < 29; y++)
            for (int x = 4; x < 16; x++) pixels[(y * 20 + x) * 4 + 3] = 255;
        pixels[3] = 10;
        BitmapSource bitmap = BitmapSource.Create(20, 30, 96, 96, PixelFormats.Bgra32, null, pixels, 80);
        Rect visible = SpeechWindow.FindVisiblePixels(bitmap);
        Require(Math.Abs(visible.Top - 10.0 / 30) < 0.0001, "Transparent head margin was included.");
        Require(Math.Abs(visible.Left - 0.2) < 0.0001, "Transparent side margin was included.");

        Size speech = new Size(210, 40);
        Rect monitor = new Rect(0, 0, 1920, 1040);
        double[] scales = { 100.0 / 165, 1, 450.0 / 165 };
        foreach (double scale in scales)
        {
            Rect character = new Rect(500, 400, 110 * scale, 150 * scale);
            Point position = SpeechWindow.Place(character, speech, monitor);
            Require(Math.Abs(character.Top - (position.Y + speech.Height) - 5) < 0.001,
                "Head gap changed with pet scale.");
        }
        foreach (Rect display in new[] { monitor, new Rect(-1920, 120, 1920, 1080) })
        {
            foreach (Point corner in new[] { display.TopLeft, new Point(display.Right - 80, display.Top),
                new Point(display.Left, display.Bottom - 100), new Point(display.Right - 80, display.Bottom - 100) })
            {
                Rect character = new Rect(corner, new Size(80, 100));
                Point position = SpeechWindow.Place(character, speech, display);
                Rect placed = new Rect(position, speech);
                Require(display.Contains(placed), "Speech clipped outside monitor work area.");
                if (character.Top == display.Top)
                    Require(!placed.IntersectsWith(character), "Speech covered the character at top edge.");
            }
        }

        SpeechWindow window = new SpeechWindow();
        Require(window.Text.FontSize == 15 && !window.ShowActivated && !window.IsHitTestVisible,
            "Speech must stay legible and must not take focus or mouse input.");
        window.Text.Text = "安静模式已开启，我会少跑动。";
        FrameworkElement content = (FrameworkElement)window.Content;
        WpfTestRender.Drain();
        content.Measure(new Size(300, double.PositiveInfinity));
        content.Arrange(new Rect(content.DesiredSize));
        RenderTargetBitmap rendered = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),
            (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        WpfTestRender.Render(rendered, content);
        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        using (FileStream file = File.Create("artifacts/tests/speech-bubble-preview.png")) encoder.Save(file);
        window.Close();
        VerifyStationarySpeech();
        Console.WriteLine("PASS: transparent padding, readable font, monitor corners, no focus/input, fixed screen position across all character frames/movement, fresh phrase reanchors and expires.");
    }

    private static void VerifyStationarySpeech()
    {
        PetWindow pet = new PetWindow { ShowActivated = false, Opacity = 0 };
        ((DispatcherTimer)Field(pet, "timer")).Stop();
        Set(pet, "autoWalk", false);
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
        SpeechWindow bubble = (SpeechWindow)Field(pet, "speechWindow");
        bubble.Opacity = 0;
        try
        {
            pet.Show(); pet.UpdateLayout();
            pet.Left = 430; pet.Top = 430;
            Call(pet, "ShowSpeech", "固定位置测试：动作变化也不要抖动。");
            double anchorX = bubble.Left, anchorY = bubble.Top;
            Image sprite = (Image)Field(pet, "sprite");
            foreach (string character in new[] { "kolo", "hutao", "ganyu", "varesa" })
            {
                Dictionary<string, BitmapSource[]> frames = (Dictionary<string, BitmapSource[]>)Call(pet, "LoadCharacterFrames", character);
                foreach (BitmapSource[] poses in frames.Values)
                foreach (BitmapSource pose in poses)
                {
                    sprite.Source = pose;
                    sprite.RenderTransform = new RotateTransform(8);
                    pet.Left += 1.25; pet.Top += 0.5;
                    Call(pet, "UpdateSpeechPosition");
                    Require(bubble.Left == anchorX && bubble.Top == anchorY, "Phrase moved with pose/character/world position.");
                }
            }
            pet.Width = 200; pet.Height = 273; pet.UpdateLayout();
            Call(pet, "UpdateSpeechPosition");
            Require(bubble.Left == anchorX && bubble.Top == anchorY, "Phrase moved during resize.");
            Call(pet, "ShowSpeech", "下一句重新定位。");
            Require(bubble.Left != anchorX || bubble.Top != anchorY, "New phrase kept a stale anchor.");
            Set(pet, "bubbleEnds", DateTime.Now.AddSeconds(-1));
            Call(pet, "UpdateSpeechPosition");
            Require(!bubble.IsVisible && !(bool)Field(pet, "speechPositionLocked"), "Expired phrase remained locked/visible.");
            Call(pet, "ShowSpeech", "新的文字可以再次出现。");
            Require(bubble.IsVisible && (bool)Field(pet, "speechPositionLocked"), "New phrase did not relock.");
        }
        finally { pet.Close(); }
    }
}
