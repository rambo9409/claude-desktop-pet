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
    public static int Main()
    {
        try { VerifyAll(); return 0; }
        catch (Exception error) {
            for (Exception current = error; current != null; current = current.InnerException)
                Console.Error.WriteLine(current.Message + "\n" + current.StackTrace);
            return 1;
        }
    }

    private static void VerifyAll()
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
        Rect narrowWork = new Rect(-300, 0, 300, 500);
        Rect wideHeadAtTop = new Rect(-260, 0, 220, 160);
        Point below = SpeechWindow.Place(wideHeadAtTop, speech, narrowWork);
        Require(below.Y >= wideHeadAtTop.Bottom + 5 && narrowWork.Contains(new Rect(below, speech)),
            "Top-edge speech must go below when neither side has room, rather than cover the head.");

        SpeechWindow window = new SpeechWindow();
        Require(window.Text.FontSize >= 15 && window.Text.FontSize <= 18 && !window.ShowActivated && !window.IsHitTestVisible,
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
        VerifyFollowingSpeech();
        SpeechThemeChecks.Run();
        Console.WriteLine("PASS: six speech themes, readable text, monitor corners, no focus/input, stable pose-independent anchors, immediate movement/resize following, negative-monitor crossing, expiry and visibility.");
    }

    private static void VerifyFollowingSpeech()
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
            ((DispatcherTimer)Field(pet, "timer")).Stop();
            Rect leftMonitor = new Rect(-1920, 0, 1920, 1080), rightMonitor = new Rect(0, 0, 1920, 1040);
            Set(pet, "monitorWorkAreas", new List<Rect> { leftMonitor, rightMonitor });
            Set(pet, "monitorsCachedAt", DateTime.MaxValue);
            Image sprite = (Image)Field(pet, "sprite");
            foreach (string character in new[] { "kolo", "gpt6a", "deepseek", "hutao", "ganyu", "varesa" })
            {
                Dictionary<string, BitmapSource[]> frames = (Dictionary<string, BitmapSource[]>)Call(pet, "LoadCharacterFrames", character);
                Set(pet, "currentCharacter", character); Set(pet, "frames", frames);
                pet.Width = 165; pet.Height = 225; pet.Left = 600; pet.Top = 430;
                sprite.Source = frames["idle"][0]; sprite.RenderTransform = Transform.Identity;
                pet.UpdateLayout();
                Call(pet, "ShowSpeech", "我会跟着你，动作变化也不抖动。");
                Set(pet, "bubbleEnds", DateTime.MaxValue);
                double anchorX = bubble.Left, anchorY = bubble.Top;
                Size initialSpeechSize = ((FrameworkElement)bubble.Content).DesiredSize;
                foreach (BitmapSource[] poses in frames.Values)
                foreach (BitmapSource pose in poses)
                {
                    sprite.Source = pose;
                    sprite.RenderTransform = new RotateTransform(8);
                    Call(pet, "UpdateSpeechPosition");
                    Require(Math.Abs(bubble.Left - anchorX) < .1 && Math.Abs(bubble.Top - anchorY) < .1,
                        "Animation changed the stable speech anchor: " + character + " from " + anchorX + "," + anchorY +
                        " to " + bubble.Left + "," + bubble.Top + " image=" + pose.PixelWidth + "x" + pose.PixelHeight +
                        " speech=" + initialSpeechSize + " => " + ((FrameworkElement)bubble.Content).DesiredSize);
                }
                pet.Left += 45; pet.Top += 30;
                Require(Math.Abs(bubble.Left - anchorX - 45) <= 1 && Math.Abs(bubble.Top - anchorY - 30) <= 1,
                    "Speech did not immediately follow real window movement: " + character);
                double beforeResizeX = bubble.Left, beforeResizeY = bubble.Top;
                pet.Width = 250; pet.Height = 341; pet.UpdateLayout(); Call(pet, "UpdateSpeechPosition");
                Require(Math.Abs(bubble.Left - beforeResizeX) > 1 || Math.Abs(bubble.Top - beforeResizeY) > 1,
                    "Speech kept its old anchor after resizing: " + character);
                pet.Left = -950; pet.Top = 350; Call(pet, "UpdateSpeechPosition");
                Require(leftMonitor.Contains(new Rect(new Point(bubble.Left, bubble.Top), ((FrameworkElement)bubble.Content).DesiredSize)),
                    "Speech failed to follow onto a negative-origin monitor: " + character);
                pet.Left = 700; Call(pet, "UpdateSpeechPosition");
                Require(rightMonitor.Contains(new Rect(new Point(bubble.Left, bubble.Top), ((FrameworkElement)bubble.Content).DesiredSize)),
                    "Speech remained on the old monitor after crossing back: " + character);
            }
            pet.WindowState = WindowState.Minimized;
            Require(!bubble.IsVisible, "Minimized pet left speech visible.");
            pet.WindowState = WindowState.Normal; Call(pet, "UpdateSpeechPosition");
            Require(bubble.IsVisible, "Restored pet did not restore active speech.");
            pet.Hide(); Require(!bubble.IsVisible, "Hidden pet left speech visible.");
            pet.Show(); Call(pet, "UpdateSpeechPosition");
            Set(pet, "bubbleEnds", DateTime.Now.AddSeconds(-1));
            Call(pet, "UpdateSpeechPosition");
            Require(!bubble.IsVisible, "Expired phrase remained visible.");
            Call(pet, "ShowSpeech", "新的文字可以再次出现。");
            Require(bubble.IsVisible, "New phrase did not appear.");
        }
        finally { pet.Close(); }
    }
}
