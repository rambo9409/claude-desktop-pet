using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class KoloEasterEggTests
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Private).GetValue(owner); }
    private static void Set(object owner, string name, object value) { owner.GetType().GetField(name, Private).SetValue(owner, value); }
    private static object Call(object owner, string name, params object[] args) { return owner.GetType().GetMethod(name, Private).Invoke(owner, args); }
    private static void Check(bool value, string description) { checks++; if (!value) throw new Exception(description); }
    private sealed class WinningRandom : Random { public override double NextDouble() { return 0; } }

    private static void Timing()
    {
        DateTime start = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
        int rolls = 0;
        KoloEasterEggSession session = new KoloEasterEggSession(start);
        Func<double> win = delegate { rolls++; return 0; };
        for (int second = 0; second < 300; second++)
            Check(!session.Observe(start.AddSeconds(second), true, win), "Startup grace triggered a transformation.");
        Check(rolls == 0 && session.Observe(start.AddSeconds(300), true, win) && rolls == 1, "Startup lottery deadline is wrong.");
        Check(session.Active && session.NextAllowedUtc == start.AddSeconds(600), "Starting an egg did not set a five minute cooldown.");
        HashSet<int> indices = new HashSet<int>(); int completions = 0;
        for (int i = 0; i < 120; i++) {
            indices.Add(session.FrameIndex(8));
            if (session.Advance(.05)) completions++;
        }
        Check(indices.Count == 8 && completions == 1 && !session.Active, "Eight visible stages or exactly-once completion failed.");
        for (int second = 307; second < 600; second++)
            Check(!session.Observe(start.AddSeconds(second), true, win), "Cooldown allowed a second costume change.");
        Check(session.Observe(start.AddSeconds(600), true, win), "Ready session cannot trigger again.");
        session.Cancel(start.AddSeconds(601));
        Check(!session.Active && !session.Advance(10000), "Cancelled animation produced a delayed completion.");

        session = new KoloEasterEggSession(start); rolls = 0;
        Func<double> miss = delegate { rolls++; return .9; };
        session.Observe(start.AddSeconds(288), true, miss);
        Check(!session.Observe(start.AddSeconds(300), true, miss) && rolls == 1, "Miss should consume one lottery.");
        for (int tick = 1; tick < 1800; tick++) session.Observe(start.AddSeconds(300 + tick / 60.0), true, miss);
        Check(rolls == 1, "Lottery is tied to frame frequency.");
        session.Observe(start.AddSeconds(330), true, miss);
        Check(rolls == 2, "Thirty second poll is missing.");
        session.Observe(start.AddSeconds(339), false, miss);
        session.Observe(start.AddSeconds(350), true, miss);
        session.Observe(start.AddSeconds(361), true, miss);
        Check(rolls == 2, "Busy time counted as continuous idle time.");
        session.Observe(start.AddSeconds(362), true, miss);
        Check(rolls == 3, "Twelve continuous idle seconds cannot reach the lottery.");
        session.Observe(start.AddDays(1), true, miss);
        Check(rolls == 4, "Missed lottery polls were replayed after sleep.");
        session.Observe(start.AddSeconds(1), true, win);
        Check(!session.Active && session.NextAllowedUtc == start.AddSeconds(301), "Clock reversal bypasses cooldown.");

        foreach (double sample in new[] { Double.NaN, -1, .12, 1 }) {
            session = new KoloEasterEggSession(start);
            session.Observe(start.AddSeconds(300), true, delegate { return sample; });
            Check(!session.Observe(start.AddSeconds(312), true, delegate { return sample; }), "Invalid or losing probability accepted.");
        }
        session = new KoloEasterEggSession(start);
        session.Observe(start.AddSeconds(300), true, win);
        session.Observe(start.AddSeconds(312), true, win);
        Check(!session.Advance(Double.NaN) && !session.Advance(-1) && session.Elapsed == 0, "Invalid delta changed animation time.");
        Check(!session.Advance(1000) && session.Elapsed == .1, "A stalled tick skipped the entire costume sequence.");
        session.Cancel(start.AddSeconds(313));
        Check(!session.Observe(start.AddSeconds(314), true, win), "Cancel bypasses active cooldown.");
    }

    private static void Settings(string directory)
    {
        string path = Path.Combine(directory, "preference.txt");
        if (File.Exists(path)) File.Delete(path);
        Check(KoloEasterEggSettings.Load(path), "Fresh installs must allow the requested egg.");
        Check(KoloEasterEggSettings.Save(path, false) && !KoloEasterEggSettings.Load(path), "Off does not survive reload.");
        using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Check(!KoloEasterEggSettings.Save(path, true), "Locked preference was replaced.");
        Check(!KoloEasterEggSettings.Load(path), "Failed save changed the old preference.");
        Check(KoloEasterEggSettings.Save(path, true) && KoloEasterEggSettings.Load(path), "On does not survive reload.");
        File.WriteAllText(path, "unrecognized");
        Check(!KoloEasterEggSettings.Load(path), "Malformed preference silently enables random changes.");
    }

    private sealed class Desktop : IWindowAbilityDesktop
    {
        internal int Calls;
        public List<AbilityWindow> Candidates() { Calls++; return new List<AbilityWindow>(); }
        public AbilityWindow Refresh(AbilityWindow value) { Calls++; return value; }
        public bool RestoreWithoutActivation(AbilityWindow value) { Calls++; return true; }
        public bool SetBounds(AbilityWindow value, Rect bounds) { Calls++; return true; }
    }

    private static void Ground(PetWindow pet)
    {
        Set(pet, "monitorWorkAreas", new List<Rect> { new Rect(0, 0, 1920, 1040) });
        Set(pet, "monitorsCachedAt", DateTime.MaxValue);
        Set(pet, "platformCache", new List<PlatformEdge> { new PlatformEdge { Left = 0, Right = 1920, Y = 1040, Bottom = 1040, IsFloor = true } });
        Set(pet, "platformCacheTime", DateTime.MaxValue);
        Set(pet, "supportHandle", IntPtr.Zero);
        pet.Left = 400; pet.Top = 1040 - pet.Height + pet.Height * .038;
    }

    private static DateTime Ready(PetWindow pet)
    {
        DateTime now = DateTime.UtcNow.AddSeconds(-20);
        Call(pet, "CancelKoloEasterEgg");
        Set(pet, "koloEasterEgg", new KoloEasterEggSession(now.AddMinutes(-6)));
        Set(pet, "random", new WinningRandom());
        Set(pet, "currentCharacter", "kolo"); Set(pet, "frames", PetAssets.Load("kolo"));
        Set(pet, "state", "idle"); Set(pet, "movement", "rest");
        Set(pet, "quietMode", false); Set(pet, "windowMagicEnabled", false);
        Set(pet, "koloEasterEggEnabled", true); Set(pet, "pointerHeld", false); Set(pet, "autoWalk", false);
        Ground(pet);
        Call(pet, "UpdateKoloEasterEgg", now, .01);
        Call(pet, "UpdateKoloEasterEgg", now.AddSeconds(12), .01);
        Check(Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active, "Eligible loaded idle pet cannot start the egg.");
        return now.AddSeconds(12);
    }

    private static void Capture(PetWindow pet, string destination)
    {
        WpfTestRender.Drain();
        FrameworkElement content = (FrameworkElement)pet.Content;
        content.Measure(new Size(330, 450)); content.Arrange(new Rect(0, 0, 330, 450)); content.UpdateLayout();
        DrawingVisual surface = new DrawingVisual();
        using (DrawingContext draw = surface.RenderOpen()) {
            draw.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 242, 250)), null, new Rect(0, 0, 330, 450));
            draw.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, 330, 450));
        }
        RenderTargetBitmap bitmap = new RenderTargetBitmap(330, 450, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(bitmap, surface);
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream file = File.Create(destination)) encoder.Save(file);
    }

    private static void Integration(string directory)
    {
        string characterPath = Path.Combine(PetStorage.RootDirectory, "character.txt");
        string preferencePath = KoloEasterEggSettings.DefaultPath;
        byte[] oldCharacter = File.Exists(characterPath) ? File.ReadAllBytes(characterPath) : null;
        byte[] oldPreference = File.Exists(preferencePath) ? File.ReadAllBytes(preferencePath) : null;
        PetWindow pet = new PetWindow { ShowActivated = false, Opacity = 0, Topmost = false };
        bool closed = false;
        try {
            Get<DispatcherTimer>(pet, "timer").Stop(); Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
            Get<SpeechWindow>(pet, "speechWindow").Opacity = 0;
            pet.Show(); pet.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
            Get<DispatcherTimer>(pet, "timer").Stop();
            Dictionary<string, BitmapSource[]> frames = PetAssets.Load("kolo");
            Check(frames.ContainsKey("disguise") && frames["disguise"].Length == 8, "Eight production costume stages are missing.");
            NotebookState notebook = Get<NotebookState>(pet, "notebook"); notebook.Detach();
            DetachedNotebookWindow book = new DetachedNotebookWindow(frames["notebook"][0], 70,
                delegate { return new Point(20, 20); }, delegate { return new Rect(0, 0, 1920, 1040); }, delegate { return false; }, delegate { });
            book.Opacity = 0; book.Topmost = false; book.Place(new Point(100, 100)); book.Show();
            Set(pet, "notebookWindow", book);
            DateTime began = Ready(pet);
            Check(notebook.Held && Get<DetachedNotebookWindow>(pet, "notebookWindow") == null && !book.IsVisible, "Starting the egg stranded the detached notebook.");
            KoloEasterEggSession session = Get<KoloEasterEggSession>(pet, "koloEasterEgg");
            frames = Get<Dictionary<string, BitmapSource[]>>(pet, "frames");
            Desktop desktop = new Desktop();
            Set(pet, "windowAbility", new WindowAbilityController(desktop,
                new WindowAbilityClock(DateTime.UtcNow.AddMinutes(-1), DateTime.MinValue), new Random(1), null));
            Set(pet, "windowMagicEnabled", true); notebook.Detach();
            Call(pet, "UpdateWindowAbility", DateTime.Now, .05);
            Check(desktop.Calls == 0, "An active costume sequence enumerated or changed windows.");
            notebook.Recover(); Set(pet, "windowMagicEnabled", false);
            for (int stage = 0; stage < 8; stage++) {
                while (session.FrameIndex(8) < stage) session.Advance(.02);
                BitmapSource held = (BitmapSource)Call(pet, "PrepareNotebookFrame", frames["idle"][0]);
                Check(Object.ReferenceEquals(Call(pet, "PrepareKoloEasterEggFrame", held), frames["disguise"][stage]), "Held notebook overrides a costume stage.");
                Call(pet, "OnTick", null, EventArgs.Empty);
                Check(Object.ReferenceEquals(Get<Image>(pet, "sprite").Source, frames["disguise"][session.FrameIndex(8)]), "Actual render pipeline omitted the costume frame.");
                Capture(pet, Path.Combine(directory, "costume-stage-" + (stage + 1) + ".png"));
            }
            for (int tick = 0; tick < 100 && session.Active; tick++)
                Call(pet, "UpdateKoloEasterEgg", DateTime.UtcNow.AddSeconds(tick * .05), .05);
            Check(!session.Active && Get<string>(pet, "currentCharacter") == "deepseek", "Completion did not switch to the real DeepSeek character.");
            Check(File.ReadAllText(characterPath).Trim() == "deepseek", "Completed costume change is not persisted.");
            Check(pet.Title.Contains("DeepSeek") && Get<Dictionary<string, BitmapSource[]>>(pet, "frames").ContainsKey("fall"), "DeepSeek title/assets were not installed.");

            Ready(pet); Set(pet, "quietMode", true); Call(pet, "UpdateKoloEasterEgg", DateTime.UtcNow, .05);
            Check(!Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active, "Quiet mode failed to cancel the sequence.");
            Ready(pet); pet.Top -= 20; Call(pet, "UpdateKoloEasterEgg", DateTime.UtcNow, .05);
            Check(!Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active, "Unsupported idle pose kept transforming in the air.");
            Ready(pet); notebook.Press(new Point(0, 0)); Call(pet, "UpdateKoloEasterEgg", DateTime.UtcNow, .05);
            Check(!Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active, "Notebook press failed to cancel the sequence."); notebook.Recover();
            Ready(pet); Call(pet, "SetKoloEasterEggEnabled", false);
            Check(!Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active && !KoloEasterEggSettings.Load(preferencePath), "Menu disable failed to cancel or persist.");
            double[] factors = { 1.08, .92, 1 };
            string[] resizeModes = { "enlarge", "shrink", "reset" };
            for (int mode = 0; mode < factors.Length; mode++) {
                Ready(pet);
                KoloEasterEggSession resizing = Get<KoloEasterEggSession>(pet, "koloEasterEgg");
                for (int frame = 0; frame < 24; frame++) resizing.Advance(.1);
                Image costumeSprite = Get<Image>(pet, "sprite");
                costumeSprite.Source = (BitmapSource)Call(pet, "PrepareKoloEasterEggFrame", costumeSprite.Source);
                Call(pet, "ResizePet", factors[mode], mode == 2);
                Check(!resizing.Active, "Resize failed to cancel immediately: " + resizeModes[mode]);
                Dictionary<string, BitmapSource[]> currentFrames = Get<Dictionary<string, BitmapSource[]>>(pet, "frames");
                Check(Object.ReferenceEquals(costumeSprite.Source, Call(pet, "PrepareNotebookFrame", currentFrames["idle"][0])),
                    "Resize left a partial costume visible: " + resizeModes[mode]);
                // Keep the resized pet eligible and advance beyond the full
                // animation duration to catch a pending completion callback.
                Ground(pet);
                DateTime resizedAt = DateTime.UtcNow;
                for (int tick = 0; tick < 110; tick++)
                    Call(pet, "UpdateKoloEasterEgg", resizedAt.AddSeconds(tick * .05), .05);
                Check(!resizing.Active && Get<string>(pet, "currentCharacter") == "kolo",
                    "Resize caused a delayed character switch: " + resizeModes[mode]);
            }
            Ready(pet); Call(pet, "SwitchCharacter", "ganyu");
            for (int i = 0; i < 110; i++) Call(pet, "UpdateKoloEasterEgg", DateTime.UtcNow.AddSeconds(i), .05);
            Check(Get<string>(pet, "currentCharacter") == "ganyu" && !Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active, "Manual selection was overwritten by delayed completion.");
            Ready(pet); pet.Hide();
            Check(!Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active && notebook.Held, "Hide left the sequence or notebook detached.");
            pet.Show(); Ready(pet); pet.WindowState = WindowState.Minimized;
            Check(!Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active, "Minimizing did not cancel immediately.");
            pet.WindowState = WindowState.Normal; Ready(pet);
            MouseButtonEventArgs down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
            pet.RaiseEvent(down);
            Check(!Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active, "Pointer-down did not interrupt the egg.");
            pet.ReleaseMouseCapture(); Set(pet, "pointerHeld", false);
            Ready(pet); pet.Close(); closed = true;
            Check(!Get<KoloEasterEggSession>(pet, "koloEasterEgg").Active, "Closing left a pending costume completion.");
        } finally {
            if (!closed) pet.Close();
            if (oldCharacter == null) { if (File.Exists(characterPath)) File.Delete(characterPath); } else File.WriteAllBytes(characterPath, oldCharacter);
            if (oldPreference == null) { if (File.Exists(preferencePath)) File.Delete(preferencePath); } else File.WriteAllBytes(preferencePath, oldPreference);
        }
    }

    [STAThread]
    private static int Main()
    {
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string directory = Path.Combine("artifacts", "v211", "easter-egg"); Directory.CreateDirectory(directory);
        try { Timing(); Settings(directory); Integration(directory); Console.WriteLine("PASS: " + checks + " idle lottery, cooldown, cancellation, notebook recovery, real switch, persistence and costume-render checks."); return 0; }
        catch (Exception error) { Console.WriteLine("FAIL: " + error); return 1; }
        finally { app.Shutdown(); }
    }
}
