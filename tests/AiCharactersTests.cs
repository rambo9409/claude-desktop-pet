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

internal static class AiCharactersTests
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static string lastStage = "not started";
    private static readonly bool traceEnabled = Environment.GetEnvironmentVariable("AI_CHARACTERS_TEST_TRACE") == "1";
    private static DispatcherTimer hostTimer;
    private static string originalCharacterPath;
    private static byte[] originalCharacterBytes;
    private static bool originalCharacterCaptured;
    private static void RestoreOriginalCharacter()
    {
        if (!originalCharacterCaptured) return;
        if (originalCharacterBytes == null) {
            if (File.Exists(originalCharacterPath)) File.Delete(originalCharacterPath);
        } else File.WriteAllBytes(originalCharacterPath, originalCharacterBytes);
    }
    private static void Stage(string text)
    {
        lastStage = text;
        if (!traceEnabled) return;
        Console.WriteLine("TRACE AiCharacters " + DateTime.UtcNow.ToString("O") + " [" + checks + "] " + text);
        Console.Out.Flush();
    }
    private static void LogException(string source, object value)
    {
        Exception error = value as Exception;
        Console.Error.WriteLine("EXCEPTION AiCharacters " + source + " [stage=" + lastStage + ", checks=" + checks + "]: " +
            (error == null ? "non-Exception value" : error.GetType().FullName + ": " + error.Message));
        Console.Error.Flush();
    }
    private static void DrainDispatcher()
    {
        // Application is constructed below but has no Run loop. Process queued
        // input/layout/render work before entering synchronous offscreen layout
        // or rendering, never from within WPF's processing-disabled callbacks.
        // Background uses the queue even on this thread; Send would run inline.
        // https://learn.microsoft.com/dotnet/api/system.windows.threading.dispatcherpriority
        if (hostTimer != null && hostTimer.IsEnabled)
            throw new InvalidOperationException("The offscreen test timer must remain stopped while pumping WPF messages.");
        WpfTestRender.Drain();
        if (hostTimer != null && hostTimer.IsEnabled)
            throw new InvalidOperationException("Pumping WPF messages unexpectedly restarted the pet timer.");
    }
    private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static Color ColorOf(object brush) { return ((SolidColorBrush)brush).Color; }
    private static double Linear(byte channel) { double v = channel / 255.0; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
    private static double Light(Color c) { return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B); }
    private static double Contrast(string foreground, string background)
    {
        double f = Light((Color)ColorConverter.ConvertFromString(foreground)), b = Light((Color)ColorConverter.ConvertFromString(background));
        return (Math.Max(f, b) + .05) / (Math.Min(f, b) + .05);
    }
    private static MenuItem Item(ContextMenu menu, string header)
    {
        foreach (object value in menu.Items) { MenuItem item = value as MenuItem; if (item != null && Convert.ToString(item.Header) == header) return item; }
        throw new Exception("Missing menu item: " + header);
    }
    private static void Capture(FrameworkElement view, double width, double height, string path)
    {
        Stage("capture layout " + Path.GetFileName(path));
        DrainDispatcher();
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        Stage("capture render " + Path.GetFileName(path));
        DrainDispatcher();
        RenderTargetBitmap bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(bitmap, view);
        Stage("capture encode " + Path.GetFileName(path));
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream output = File.Create(path)) encoder.Save(output);
        DrainDispatcher();
    }
    private static void Gestures()
    {
        foreach (string character in new[] { "kolo", "gpt6a", "deepseek" }) {
            PetTapSequence taps = new PetTapSequence();
            taps.Begin(character, 1); Check(!taps.Complete(character), "Single tap became a special ability.");
            taps.Begin(character, 2); Check(taps.Complete(character), "New AI double tap was rejected.");
            taps.Begin(character, 3); Check(!taps.Complete(character), "Rapid double taps overlap.");
            taps.Begin(character, 4); Check(taps.Complete(character), "Second rapid pair did not fire.");
            taps.Begin(character, 1); taps.Move(6); Check(!taps.Complete(character), "Drag fires ability.");
            taps.Begin(character, 2); Check(!taps.Complete(character), "A drag starts an ability pair.");
            taps.Begin(character, 1); taps.Complete(character); taps.Reset(); taps.Begin(character, 2);
            Check(!taps.Complete(character), "Character switch retained the previous tap.");
        }
        PetTapSequence mixed = new PetTapSequence(); mixed.Begin("gpt6a", 1); mixed.Complete("gpt6a"); mixed.Begin("deepseek", 2);
        Check(!mixed.Complete("deepseek"), "Cross-character clicks form a double tap.");
    }
    private static void Frames(string character, Dictionary<string, BitmapSource[]> frames, string directory)
    {
        foreach (string action in new[] { "idle", "happy", "pet", "rose", "typing", "walk", "stroll", "climb-up", "climb-down" }) {
            Check(frames.ContainsKey(action) && frames[action].Length > 0, character + " missing " + action);
            foreach (BitmapSource frame in frames[action]) {
                Check(frame.IsFrozen && frame.PixelWidth == 512 && frame.PixelHeight == 512, character + " invalid frame size/cache.");
                Rect bounds = SpeechWindow.FindVisiblePixels(frame);
                Check(bounds.Width > .1 && bounds.Height > .1 && bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= 1 && bounds.Bottom <= 1,
                    character + " has empty or invalid alpha bounds.");
            }
        }
        Check(frames["walk"].Length == 4 && frames["stroll"].Length == 4 && frames["climb-up"].Length == 4, character + " incomplete movement sequence.");
        for (int i = 0; i < 4; i++) Check(Object.ReferenceEquals(frames["climb-up"][i], frames["climb-down"][3 - i]), "Climb reverse lost its painted pose.");
        Check(!frames.ContainsKey("glide") && !frames.ContainsKey("glider") && !PetAssets.HasWindGlider(character), "AI character incorrectly uses a Genshin wind glider.");
        Grid sheet = new Grid { Background = Brushes.WhiteSmoke };
        for (int c = 0; c < 4; c++) sheet.ColumnDefinitions.Add(new ColumnDefinition());
        for (int r = 0; r < 3; r++) sheet.RowDefinitions.Add(new RowDefinition());
        string[] actions = { "stroll", "walk", "climb-up" };
        for (int row = 0; row < actions.Length; row++) for (int col = 0; col < 4; col++) {
            StackPanel cell = new StackPanel(); Grid.SetRow(cell, row); Grid.SetColumn(cell, col); sheet.Children.Add(cell);
            cell.Children.Add(new TextBlock { Text = actions[row] + " " + (col + 1), HorizontalAlignment = HorizontalAlignment.Center });
            cell.Children.Add(new Image { Source = frames[actions[row]][col], Width = 180, Height = 180 });
        }
        Capture(sheet, 800, 620, Path.Combine(directory, character + "-movement.png"));
    }
    private static void Tools(string character, BitmapSource portrait)
    {
        PetWindow.MenuPalette palette = PetWindow.MenuPalette.ForCharacter(character);
        HighlightsWindow highlights = new HighlightsWindow(character, portrait);
        CombatRotationWindow planner = new CombatRotationWindow(character, false);
        GptResetWindow reset = new GptResetWindow(character, false);
        try {
            Check(Get<TextBlock>(highlights, "companionText").Text.Contains(PetWindow.CharacterDisplayName(character)), "Daily highlights still names Kolo for a new AI.");
            Color surface = (Color)ColorConverter.ConvertFromString(palette.Surface);
            Check(ColorOf(highlights.Resources["WindowSurface"]) == surface, "Daily highlights lost the AI theme.");
            Check(ColorOf(planner.Resources["PlannerSurface"]) == surface, "Combat planner lost the AI theme.");
            Check(ColorOf(reset.Resources["ResetSurface"]) == surface, "GPT reset tool lost the AI theme.");
        } finally { highlights.Close(); planner.Close(); reset.Close(); }
        DrainDispatcher();
    }
    private static void Movement(PetWindow pet, Dictionary<string, BitmapSource[]> frames)
    {
        Image sprite = Get<Image>(pet, "sprite");
        Set(pet, "pointerHeld", true); Set(pet, "state", "idle"); Set(pet, "stateEnds", DateTime.MaxValue);
        foreach (string gait in new[] { "stroll", "run" }) {
            Set(pet, "activeGait", gait); Set(pet, "movement", "walk");
            for (int i = 0; i < 4; i++) {
                Set(pet, "runPhase", (i + .15) / 4); Call(pet, "OnTick", null, EventArgs.Empty);
                Check(Object.ReferenceEquals(sprite.Source, frames[MovementGait.FrameKey(gait)][i]), "A movement phase was skipped.");
            }
        }
        Set(pet, "movement", "climb"); Set(pet, "climbVerticalDirection", -1);
        for (int i = 0; i < 4; i++) {
            Set(pet, "climbPhase", (i + .15) / 4); Call(pet, "OnTick", null, EventArgs.Empty);
            Check(Object.ReferenceEquals(sprite.Source, frames["climb-up"][i]), "A climb/swim phase was skipped.");
        }
        foreach (string action in new[] { "held", "fall", "rest" }) { Set(pet, "movement", action); Call(pet, "OnTick", null, EventArgs.Empty); Check(sprite.Source != null, "Airborne AI lost its image."); }
        Set(pet, "pointerHeld", false); Set(pet, "movement", "fall");
        Set(pet, "monitorWorkAreas", new List<Rect> { new Rect(0, 0, 2000, 10000) }); Set(pet, "monitorsCachedAt", DateTime.MaxValue);
        Set(pet, "platformCache", new List<PlatformEdge>()); Set(pet, "platformCacheTime", DateTime.MaxValue);
        Set(pet, "fallVelocityX", 0.0); Set(pet, "fallVelocityY", 16.0); pet.Top = 200; pet.Left = 400;
        for (int i = 0; i < 100; i++) Call(pet, "UpdateFall", .016);
        Check(Get<double>(pet, "fallVelocityY") <= 89, "Fish/dragon natural buoyancy falls like a dropped stone.");
        Check(pet.Top > 200 && pet.Top < 350, "Natural descent did not advance smoothly.");
        Set(pet, "movement", "rest");
    }

    private static void FocusGesture(PetWindow pet, Dictionary<string, BitmapSource[]> frames)
    {
        Set(pet, "autoWalk", false); Set(pet, "movement", "rest"); Set(pet, "state", "idle");
        Get<PetTapSequence>(pet, "tapSequence").Reset();
        for (int count = 1; count <= 2; count++) {
            MouseButtonEventArgs down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left);
            down.RoutedEvent = Mouse.MouseDownEvent;
            typeof(MouseButtonEventArgs).GetProperty("ClickCount").GetSetMethod(true).Invoke(down, new object[] { count });
            Call(pet, "OnPointerDown", pet, down);
            NativeWindow.POINT cursor; Check(NativeWindow.GetCursorPos(out cursor), "Cannot query cursor for owned input test.");
            Set(pet, "pointerStart", Get<Matrix>(pet, "fromDevice").Transform(new Point(cursor.X, cursor.Y)));
            MouseButtonEventArgs up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left);
            up.RoutedEvent = Mouse.MouseUpEvent;
            typeof(MouseButtonEventArgs).GetProperty("ClickCount").GetSetMethod(true).Invoke(up, new object[] { count });
            Call(pet, "OnPointerUp", pet, up);
        }
        Check(Get<DragonFocusSession>(pet, "dragonFocus").Active, "Actual GPT double click did not start focus.");
        Check(Get<string>(pet, "state") == "idle", "First-click happy state swallowed the cast animation.");
        BitmapSource idle = frames["idle"][0];
        Check(Array.IndexOf(frames["ability"], Call(pet, "PrepareAiAbilityFrame", idle)) >= 0, "Focus startup did not use painted cast poses.");
        foreach (string movement in new[] { "walk", "held", "fall" }) {
            Set(pet, "movement", movement);
            Check(Object.ReferenceEquals(Call(pet, "PrepareAiAbilityFrame", idle), idle), "AI casting replaces locomotion/drag poses.");
        }
        Set(pet, "movement", "rest");
    }

    private static void DraftCloseProtection(PetWindow pet, string directory)
    {
        BubbleNotesWindow note = new BubbleNotesWindow(Path.Combine(directory, "draft-notes.json"), null);
        note.Closed += delegate { Set(pet, "bubbleNotesWindow", null); };
        Set(pet, "bubbleNotesWindow", note); note.Editor.Text = "unsaved test draft"; note.DiscardConfirmation = delegate { return false; };
        System.ComponentModel.CancelEventArgs close = new System.ComponentModel.CancelEventArgs();
        Call(pet, "OnClosing", close); Check(close.Cancel, "Main pet exit discarded an unsaved note without the editor's decision.");
        Call(pet, "CancelAiAbilities");
        Check(note.Editor.Text == "unsaved test draft", "Hiding or switching the pet discarded the note draft.");
        note.DiscardConfirmation = delegate { return true; }; close = new System.ComponentModel.CancelEventArgs();
        Call(pet, "OnClosing", close); Check(!close.Cancel, "Explicit discard still blocks exit.");
        Call(pet, "CloseAiAbilities"); Check(Get<BubbleNotesWindow>(pet, "bubbleNotesWindow") == null, "Pet close retained a hidden note window.");
    }
    [STAThread]
    private static int Main()
    {
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) {
            LogException("AppDomain terminating=" + e.IsTerminating, e.ExceptionObject);
            // Native WPF callback failures can bypass Main's finally. Restore
            // only the exact character-setting bytes captured by this test.
            try { RestoreOriginalCharacter(); }
            catch (Exception restoreError) { LogException("character restore failed", restoreError); }
        };
        Dispatcher.CurrentDispatcher.UnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) {
            // Diagnostic only: keep Handled false and let the test fail.
            LogException("Dispatcher", e.Exception);
        };
        Stage("application begin");
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        originalCharacterPath = Path.Combine(PetStorage.RootDirectory, "character.txt");
        originalCharacterBytes = File.Exists(originalCharacterPath) ? File.ReadAllBytes(originalCharacterPath) : null;
        originalCharacterCaptured = true;
        string directory = Path.Combine("artifacts", "v28", "ai-characters"); Directory.CreateDirectory(directory);
        PetWindow pet = null;
        try {
            Stage("gestures"); Gestures(); Stage("pet constructor");
            pet = new PetWindow(); hostTimer = Get<DispatcherTimer>(pet, "timer"); hostTimer.Stop(); Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
            Stage("pet initialized"); DrainDispatcher();
            HashSet<string> palettes = new HashSet<string>();
            foreach (string old in new[] { "kolo", "hutao", "ganyu", "varesa" }) palettes.Add(PetWindow.MenuPalette.ForCharacter(old).Surface);
            foreach (string character in new[] { "gpt6a", "deepseek" }) {
                Stage("switch " + character);
                Call(pet, "SwitchCharacter", character);
                if (character == "deepseek") Check(!Get<DragonFocusSession>(pet, "dragonFocus").Active, "Switching from GPT retained an active focus timer.");
                Check(Get<string>(pet, "currentCharacter") == character && pet.Title.Contains(PetWindow.CharacterDisplayName(character)), "New character failed to switch.");
                Stage("frames " + character);
                Dictionary<string, BitmapSource[]> frames = Get<Dictionary<string, BitmapSource[]>>(pet, "frames"); Frames(character, frames, directory);
                Stage("menu " + character);
                ContextMenu menu = pet.ContextMenu; PetWindow.MenuPalette palette = PetWindow.MenuPalette.ForCharacter(character);
                Check(palettes.Add(palette.Surface), "New character reuses another palette.");
                Check(Contrast(palette.Text, palette.Surface) >= 4.5 && Contrast(palette.Text, palette.Checked) >= 4.5 && Contrast(palette.Text, palette.Hover) >= 4.5, "Menu theme text contrast.");
                Check(Contrast(palette.Disabled, palette.Surface) >= 3, "Disabled menu contrast.");
                Check(Item(menu, "选择形象").Items.Count == 6 && Get<List<MenuItem>>(pet, "characterItems").Count == 6, "Menu does not contain exactly six characters.");
                if (character == "deepseek") { Item(menu, "慢慢走一会儿"); Item(menu, "立即跑步"); }
                DrainDispatcher(); menu.ApplyTemplate(); Capture(menu, 280, 760, Path.Combine(directory, character + "-menu.png"));
                Stage("tools " + character); Tools(character, frames["idle"][0]);
                Stage("movement " + character); Movement(pet, frames);
                if (character == "gpt6a") { Stage("focus gesture " + character); FocusGesture(pet, frames); }
                Stage("interaction complete " + character); DrainDispatcher();
                Stage("reopen " + character);
                PetWindow reopened = new PetWindow();
                try { Get<DispatcherTimer>(reopened, "timer").Stop(); Check(Get<string>(reopened, "currentCharacter") == character, "Saved AI character falls back to Kolo on restart."); }
                finally { reopened.Close(); }
                Stage("reopened close " + character); DrainDispatcher();
            }
            Stage("draft close protection");
            DraftCloseProtection(pet, directory);
            DrainDispatcher();
            Console.WriteLine("PASS: " + checks + " AI character registration, six-entry menus, themes, tool labels, assets, movement, natural descent and gestures. Previews: " + directory);
            return 0;
        } catch (Exception error) {
            LogException("Main catch", error);
            if (error.InnerException != null) LogException("Main cause", error.InnerException);
            return 1;
        }
        finally {
            Stage("pet close");
            try { if (pet != null) pet.Close(); }
            finally {
                try { RestoreOriginalCharacter(); }
                finally { Stage("application shutdown"); app.Shutdown(); }
            }
        }
    }
}
