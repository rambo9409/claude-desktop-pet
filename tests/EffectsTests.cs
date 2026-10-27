using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class EffectsTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static object Get(PetWindow pet, string name) { return typeof(PetWindow).GetField(name, Private).GetValue(pet); }
    private static void Set(PetWindow pet, string name, object value) { typeof(PetWindow).GetField(name, Private).SetValue(pet, value); }
    private static object Call(PetWindow pet, string name, params object[] args) { return typeof(PetWindow).GetMethod(name, Private).Invoke(pet, args); }
    private static void Require(bool value, string text) { if (!value) throw new Exception(text); checks++; }
    private static Dictionary<string, BitmapSource[]> Frames(PetWindow pet) { return (Dictionary<string, BitmapSource[]>)Get(pet, "frames"); }
    private static ButterflyMorphState Morph(PetWindow pet) { return (ButterflyMorphState)Get(pet, "butterflyMorph"); }
    private static void Render(PetWindow pet, string source, double seconds)
    {
        TransformGroup transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform((int)Get(pet, "direction") > 0 ? 1 : -1, 1));
        Call(pet, "RenderButterflyEffects", Frames(pet)[source][0], transform, seconds);
    }
    private static void Settle(PetWindow pet, string source) { for (int i = 0; i < 24; i++) Render(pet, source, 0.05); }
    private static void StartFlight(PetWindow pet)
    {
        Set(pet, "currentCharacter", "hutao");
        Set(pet, "frames", Call(pet, "LoadCharacterFrames", "hutao"));
        Call(pet, "CancelButterflyEffects");
        Set(pet, "state", "idle");
        Set(pet, "movement", "walk");
        Set(pet, "activeGait", "run");
        Set(pet, "runPhase", 0.0);
        Set(pet, "direction", 1);
    }
    private static BitmapSource Capture(PetWindow pet, bool transparent = false)
    {
        FrameworkElement content = (FrameworkElement)pet.Content;
        content.Measure(new Size(330, 450));
        content.Arrange(new Rect(0, 0, 330, 450));
        content.UpdateLayout();
        RenderTargetBitmap bitmap = new RenderTargetBitmap(330, 450, 96, 96, PixelFormats.Pbgra32);
        DrawingVisual background = new DrawingVisual();
        using (DrawingContext dc = background.RenderOpen())
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(38, 32, 47)), null, new Rect(0, 0, 330, 450));
        if (!transparent) bitmap.Render(background);
        bitmap.Render(content);
        bitmap.Freeze();
        return bitmap;
    }
    private static void Save(BitmapSource source, string path)
    {
        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using (FileStream stream = File.Create(path)) encoder.Save(stream);
    }
    private static long Pixels(BitmapSource source)
    {
        byte[] bytes = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(bytes, source.PixelWidth * 4, 0);
        long hash = 17;
        foreach (byte value in bytes) hash = unchecked(hash * 31 + value);
        return hash;
    }

    private static BitmapSource CaptureTrack(PetWindow pet, string caption)
    {
        DrawingVisual track = new DrawingVisual();
        using (DrawingContext dc = track.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(38, 32, 47)), null, new Rect(0, 0, 980, 480));
            dc.DrawText(new FormattedText(caption, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei"), 23, Brushes.LightGoldenrodYellow, 1.0), new Point(25, 22));
            Pen ground = new Pen(new SolidColorBrush(Color.FromRgb(111, 94, 110)), 1);
            dc.DrawLine(ground, new Point(15, 433), new Point(965, 433));
            for (int x = 20; x < 980; x += 45) dc.DrawLine(ground, new Point(x, 433), new Point(x, 441));
            dc.DrawImage(Capture(pet, true), new Rect(pet.Left, 0, 330, 450));
        }
        RenderTargetBitmap frame = new RenderTargetBitmap(980, 480, 96, 96, PixelFormats.Pbgra32);
        frame.Render(track);
        frame.Freeze();
        return frame;
    }

    private static void BuildPreview(PetWindow pet)
    {
        Directory.CreateDirectory("artifacts/tests/effects-run-frames");
        List<BitmapSource> gallery = new List<BitmapSource>();
        List<string> captions = new List<string>();
        HashSet<int> samples = new HashSet<int>(new[] { 0, 6, 12, 18, 24, 32, 42, 54, 58, 62, 65, 69 });
        StartFlight(pet);
        pet.Width = 330;
        pet.Height = 450;
        pet.Left = 20;
        pet.Top = 1000 - pet.Height + pet.Height * 0.038;
        IntPtr support = new IntPtr(424242);
        Set(pet, "platformCache", new List<PlatformEdge> {
            new PlatformEdge { Handle = support, Left = 0, Right = 100000, Y = 1000, Bottom = 1500, IsFloor = false }
        });
        Set(pet, "supportHandle", support);
        Set(pet, "moveSpeed", 260.0);
        Set(pet, "targetMoveSpeed", 260.0);
        Set(pet, "walkEnds", DateTime.MaxValue);
        Set(pet, "nextSpeedChange", DateTime.MaxValue);
        Set(pet, "autoWalk", false);
        for (int step = 0; step < 96; step++)
        {
            double seconds = step == 0 ? 0 : 1.0 / 30;
            if (step <= 54)
            {
                Set(pet, "platformCacheTime", DateTime.Now);
                Call(pet, "UpdateMovement", DateTime.Now, seconds);
                int wing = MotionCycle.Frame((double)Get(pet, "runPhase"), 4, false);
                TransformGroup pose = new TransformGroup();
                pose.Children.Add(new ScaleTransform(1, 1));
                Call(pet, "RenderButterflyEffects", Frames(pet)["walk"][wing], pose, seconds);
            }
            else
            {
                Set(pet, "movement", "rest");
                Render(pet, "idle", seconds);
            }
            string stage = step > 54 ? "金蝶恢复人形" : Morph(pet).Progress == 0 ? "先跑起来" :
                Morph(pet).Progress < 1 ? "边跑边化作金蝶" : "金蝶飞行";
            Save(CaptureTrack(pet, stage), "artifacts/tests/effects-run-frames/" + step.ToString("D3") + ".png");
            if (samples.Contains(step))
            {
                gallery.Add(Capture(pet));
                captions.Add((step / 30.0).ToString("0.00", CultureInfo.InvariantCulture) + "s · " + stage);
            }
        }
        Require(pet.Left > 450, "Preview failed to move along the actual movement path.");
        DrawingVisual contact = new DrawingVisual();
        using (DrawingContext dc = contact.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(38, 32, 47)), null, new Rect(0, 0, 1200, 600));
            for (int i = 0; i < gallery.Count; i++)
            {
                int x = i % 6 * 200, y = i / 6 * 300;
                dc.DrawImage(gallery[i], new Rect(x, y + 20, 200, 273));
                dc.DrawText(new FormattedText(captions[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"), 12, Brushes.LightGoldenrodYellow, 1.0), new Point(x + 8, y + 9));
            }
        }
        RenderTargetBitmap preview = new RenderTargetBitmap(1200, 600, 96, 96, PixelFormats.Pbgra32);
        preview.Render(contact);
        Save(preview, "artifacts/tests/effects-transition-contact.png");
    }
    [STAThread]
    private static void Main()
    {
        ButterflyMorphState clock = new ButterflyMorphState();
        clock.Aim(true);
        clock.Advance(0.05);
        Require(clock.Progress == 0 && clock.RunUpElapsed == 0.05, "Entry must begin with an intact running human.");
        for (int i = 0; i < 8; i++) clock.Advance(0.05);
        Require(clock.Progress == 0, "Dissolution started before the human run-up completed.");
        clock.Advance(0.05);
        Require(clock.Progress > 0 && clock.Progress < 0.04, "Run-up must carry its remaining time into the dissolution.");
        for (int i = 0; i < 4; i++) clock.Advance(0.05);
        double beforeReverse = clock.Progress;
        clock.Aim(false);
        Require(clock.Progress == beforeReverse, "Reversal jumped to a fresh animation.");
        clock.Advance(0.05);
        Require(clock.Progress > 0 && clock.Progress < beforeReverse, "Reversal failed to retrace the current transformation.");
        beforeReverse = clock.Progress;
        clock.Aim(true);
        clock.Advance(0.05);
        Require(clock.Progress > beforeReverse, "Resuming a partial morph incorrectly repeated its run-up.");
        clock.Reset();
        Require(clock.Progress == 0 && !clock.InTransition && clock.RunUpElapsed == 0, "Cancellation retained an active transition.");

        PetWindow pet = new PetWindow();
        ((DispatcherTimer)Get(pet, "timer")).Stop();
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
        try
        {
            StartFlight(pet);
            Image sprite = (Image)Get(pet, "sprite");
            Image butterfly = (Image)Get(pet, "butterflyLayer");
            Render(pet, "walk", 0);
            Require(ReferenceEquals(sprite.Source, Frames(pet)["run-human"][0]), "The first frame must run as Hu Tao, not stand or flash a butterfly.");
            BitmapSource human = Capture(pet);
            for (int stride = 0; stride < 4; stride++)
            {
                Set(pet, "runPhase", (stride + 0.1) / 4);
                Render(pet, "walk", 0.05);
                Require(ReferenceEquals(sprite.Source, Frames(pet)["run-human"][stride]) && sprite.OpacityMask == null,
                    "Run-up froze or dissolved human stride " + stride + ".");
                Require(butterfly.Visibility == Visibility.Collapsed, "Gold butterfly appeared during the intact run-up.");
            }
            Require(Pixels(human) != Pixels(Capture(pet)), "Run-up frames did not change their actual painted legs.");
            for (int i = 0; i < 7; i++) Render(pet, "walk", 0.05);
            for (int stride = 0; stride < 4; stride++)
            {
                Set(pet, "runPhase", (stride + 0.1) / 4);
                Render(pet, "walk", 0.05);
                Require(ReferenceEquals(sprite.Source, Frames(pet)["run-human"][stride]) && sprite.OpacityMask != null,
                    "Dissolution froze the moving human stride " + stride + ".");
            }
            Set(pet, "direction", -1);
            Render(pet, "walk", 0);
            Require(((ScaleTransform)((TransformGroup)sprite.RenderTransform).Children[0]).ScaleX == -1,
                "The dissolving human did not face its running direction.");
            Set(pet, "direction", 1);
            Render(pet, "walk", 0);
            Require(butterfly.Visibility == Visibility.Visible && butterfly.Opacity > 0 && butterfly.Opacity < 1,
                "A smaller gold butterfly must emerge during the human dissolution.");
            Require(((Canvas)Get(pet, "butterflyParticles")).Visibility == Visibility.Visible,
                "Dissolution omitted the drifting small butterflies.");
            BitmapSource middle = Capture(pet);
            Require(Pixels(human) != Pixels(middle), "Rendered transition is visually identical to the starting pose.");
            double middleProgress = Morph(pet).Progress;
            Set(pet, "movement", "rest");
            Render(pet, "idle", 0.016);
            Require(Morph(pet).Progress < middleProgress && Morph(pet).Progress > middleProgress - 0.04,
                "Stopping halfway abruptly discarded the in-progress transformation.");
            Settle(pet, "idle");
            Require(Morph(pet).Progress == 0 && butterfly.Visibility == Visibility.Collapsed && sprite.OpacityMask == null,
                "Returning to human left a butterfly or dissolve mask behind.");
            Require(ReferenceEquals(sprite.Source, Frames(pet)["idle"][0]), "Restored human did not finish in the idle pose.");

            StartFlight(pet);
            Render(pet, "walk", 0.05);
            Set(pet, "movement", "rest");
            Render(pet, "idle", 0.05);
            Require(Morph(pet).Progress == 0 && Morph(pet).RunUpElapsed == 0 && ReferenceEquals(sprite.Source, Frames(pet)["idle"][0]),
                "Stopping during the run-up triggered a delayed transformation.");
            Set(pet, "movement", "walk");
            Render(pet, "walk", 0.05);
            Require(Morph(pet).Progress == 0 && Morph(pet).RunUpElapsed == 0.05,
                "A fresh run reused an interrupted run-up timer.");

            foreach (string destination in new[] { "idle", "stroll", "climb-up", "pet", "happy", "rose", "typing" })
            {
                StartFlight(pet);
                Settle(pet, "walk");
                Require(ReferenceEquals(sprite.Source, Frames(pet)["walk"][0]), "Steady flight does not use the actual wing frame.");
                Set(pet, "movement", destination == "stroll" ? "walk" : destination == "climb-up" ? "climb" : "rest");
                Set(pet, "state", destination == "pet" || destination == "happy" || destination == "rose" || destination == "typing" ? destination : "idle");
                Set(pet, "activeGait", "stroll");
                Render(pet, destination, 0);
                Require(ReferenceEquals(sprite.Source, Frames(pet)["walk"][0]), "Return flashed a human before advancing time.");
                Render(pet, destination, 0.016);
                Require(Morph(pet).Progress > 0.95 && butterfly.Visibility == Visibility.Visible,
                    "Flight ended instantly when changing to " + destination + ".");
                Settle(pet, destination);
                Require(ReferenceEquals(sprite.Source, Frames(pet)[destination][0]) && butterfly.Visibility == Visibility.Collapsed,
                    "Transition failed to finish into " + destination + ".");
            }
            foreach (string interruption in new[] { "held", "fall" })
            foreach (int elapsedFrames in new[] { 4, 14, 24 })
            {
                StartFlight(pet);
                for (int i = 0; i < elapsedFrames; i++) Render(pet, "walk", 0.05);
                Set(pet, "movement", interruption);
                Render(pet, "idle", 0.016);
                Require(Morph(pet).Progress == 0 && butterfly.Visibility == Visibility.Collapsed && sprite.OpacityMask == null &&
                    ReferenceEquals(sprite.Source, Frames(pet)["idle"][0]),
                    interruption + " retained a butterfly or ghost mask instead of the wing-ready human.");
            }
            StartFlight(pet);
            Settle(pet, "walk");
            Set(pet, "currentCharacter", "ganyu");
            Set(pet, "frames", Call(pet, "LoadCharacterFrames", "ganyu"));
            Render(pet, "idle", 0.016);
            Require(Morph(pet).Progress == 0 && butterfly.Visibility == Visibility.Collapsed && ReferenceEquals(sprite.Source, Frames(pet)["idle"][0]),
                "Switching character retained Hu Tao's effects.");

            BuildPreview(pet);
            Console.WriteLine("PASS: " + checks + " running lead-in, moving dissolution, reversal, interruption, actual movement and WPF visual checks.");
        }
        finally { pet.Close(); }
    }
}
