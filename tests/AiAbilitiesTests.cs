using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudePet;

internal static class AiAbilitiesTests
{
    private static int checks;
    private static void Check(bool value, string description) { checks++; if (!value) throw new Exception(description); }
    private static void Reject(Action action, string description)
    { bool failed = false; try { action(); } catch { failed = true; } Check(failed, description); }
    private static void Focus()
    {
        DateTime now = new DateTime(2026, 10, 30, 2, 0, 0, DateTimeKind.Utc);
        foreach (int duration in new[] { 15, 25, 45 }) {
            DragonFocusSession session = new DragonFocusSession();
            Check(!session.Active && !session.Advance(now), "Inactive focus completes spuriously.");
            Check(session.Start(now, duration), "Focus cannot start.");
            Check(session.Remaining(now) == duration * 60 && session.Progress(now) == 0, "Wrong focus duration.");
            Check(!session.Start(now.AddSeconds(10), 15), "Repeated double-click resets an active focus.");
            Check(session.EndsUtc == now.AddMinutes(duration), "Repeated start changed the deadline.");
            Check(Math.Abs(session.Progress(now.AddMinutes(duration / 2.0)) - .5) < .00001, "Focus progress not deadline based.");
            Check(session.Remaining(now.AddHours(-1)) == duration * 60, "Clock reversal creates larger-than-start duration.");
            Check(session.RemainingText(now.AddMinutes(duration).AddSeconds(-1)) == "00:01", "Countdown formatting is wrong.");
            Check(!session.Advance(now.AddMinutes(duration).AddMilliseconds(-1)), "Focus completed early.");
            Check(session.Advance(now.AddMinutes(duration).AddSeconds(50)), "Suspended focus missed completion.");
            Check(!session.Active && !session.Advance(now.AddHours(5)), "Completion fires more than once.");
            Check(session.Start(now, duration), "Completed timer cannot restart.");
            session.Cancel(); Check(!session.Active && !session.Advance(now.AddHours(5)), "Cancel still produces completion.");
        }
        Reject(delegate { new DragonFocusSession().Start(now, 0); }, "Unsupported focus duration accepted.");
        Reject(delegate { new DragonFocusSession().Start(now, Int32.MaxValue); }, "Out-of-range focus duration accepted.");
    }
    private static BubbleNote Sample(string text)
    { return new BubbleNote { Id = Guid.NewGuid().ToString("N"), Text = text, UpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }; }
    private static void Storage(string directory)
    {
        string path = Path.Combine(directory, "notes-storage.json"), notice;
        Check(BubbleNoteStorage.Load(path, out notice).Notes.Count == 0 && notice == null, "New note book is not empty.");
        BubbleNoteBook book = BubbleNoteBook.Empty(); book.Notes.Add(Sample("一个灵感\n中文和English"));
        BubbleNoteStorage.Save(path, book);
        BubbleNoteBook loaded = BubbleNoteStorage.Load(path, out notice);
        Check(loaded.Notes.Count == 1 && loaded.Notes[0].Text == book.Notes[0].Text, "Notes do not roundtrip.");
        string previous = File.ReadAllText(path);
        using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Reject(delegate { BubbleNoteStorage.Save(path, BubbleNoteBook.Empty()); }, "Locked notes are overwritten.");
        Check(File.ReadAllText(path) == previous, "Failed note save changes original.");
        BubbleNoteBook oversized = book.Copy(); oversized.Notes[0].Text = new String('字', BubbleNoteStorage.MaximumText + 1);
        Reject(delegate { BubbleNoteStorage.Save(path, oversized); }, "Overlong notes accepted.");
        Check(File.ReadAllText(path) == previous, "Invalid notes replace valid content.");
        oversized = BubbleNoteBook.Empty(); for (int i = 0; i < 101; i++) oversized.Notes.Add(Sample("note"));
        Reject(delegate { BubbleNoteStorage.Save(path, oversized); }, "Maximum notes silently discards data.");
        BubbleNoteBook invalid = book.Copy(); invalid.Notes.Add(invalid.Notes[0].Copy());
        Reject(delegate { BubbleNoteStorage.Save(path, invalid); }, "Duplicate IDs allowed.");
        invalid = book.Copy(); invalid.Notes[0] = null;
        Reject(delegate { BubbleNoteStorage.Save(path, invalid); }, "Null item accepted.");
        invalid = book.Copy(); invalid.Version = 999;
        Reject(delegate { BubbleNoteStorage.Save(path, invalid); }, "Future schema silently overwritten.");
        File.WriteAllText(path, "{broken notes");
        loaded = BubbleNoteStorage.Load(path, out notice);
        Check(loaded.Notes.Count == 0 && !String.IsNullOrEmpty(notice), "Corrupt notes fail without notice.");
        Check(File.ReadAllText(path) == "{broken notes" && Directory.GetFiles(directory, "notes-storage.json.broken-*").Length > 0, "Corrupt notes not preserved.");
        BubbleNoteStorage.Save(path, book);
        Check(BubbleNoteStorage.Load(path, out notice).Notes.Count == 1, "Backed-up corrupt file cannot be recovered.");
    }
    private static void Capture(FrameworkElement view, double width, double height, string path)
    {
        WpfTestRender.Drain();
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        RenderTargetBitmap bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(bitmap, view);
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream file = File.Create(path)) encoder.Save(file);
    }
    private static void NoteWindow(string directory)
    {
        int saved = 0; string path = Path.Combine(directory, "window-notes.json"), notice;
        BubbleNotesWindow window = new BubbleNotesWindow(path, delegate { saved++; });
        window.DiscardConfirmation = delegate { return false; };
        try {
            Check(!window.SaveCurrent() && !File.Exists(path), "Empty editor writes a note.");
            window.Editor.Text = "明天的想法：把大任务拆成三个小步骤。";
            Check(window.SaveCurrent() && saved == 1 && window.NoteCount == 1, "Save button path did not write a note.");
            window.Editor.Text += "\n第一步先做五分钟。";
            Check(window.SaveCurrent() && window.NoteCount == 1, "Editing selected note creates duplicate.");
            Check(BubbleNoteStorage.Load(path, out notice).Notes[0].Text.Contains("五分钟"), "Saved edits not on disk.");
            window.NewNote(); window.Editor.Text = "第二颗灵感泡泡";
            Check(window.SaveCurrent() && window.NoteCount == 2, "New button does not create a separate note.");
            window.Editor.Text = "尚未保存的草稿不能默默丢掉";
            Check(!window.NewNote() && window.Editor.Text.Contains("草稿"), "New note silently discards an unsaved draft.");
            window.DiscardConfirmation = delegate { return true; };
            Check(window.NewNote(), "Explicit draft discard cannot start a new note.");
            window.Editor.Text = "第三颗泡泡"; Check(window.SaveCurrent() && window.NoteCount == 3, "Save after deliberate discard failed.");
            Capture((FrameworkElement)window.Content, 620, 600, Path.Combine(directory, "deepseek-notes.png"));
            Capture((FrameworkElement)window.Content, 430, 450, Path.Combine(directory, "deepseek-notes-small.png"));
            window.DiscardConfirmation = delegate { return false; };
            window.Editor.Text = "切换历史前尚未保存的修改";
            ListBox history = (ListBox)typeof(BubbleNotesWindow).GetField("history", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            history.SelectedIndex = 1;
            Check(window.Editor.Text.Contains("尚未保存"), "History selection silently discards unsaved edits.");
            bool closingCancelled = false;
            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { closingCancelled = e.Cancel; };
            window.Close();
            Check(closingCancelled && window.Editor.Text.Contains("尚未保存"), "Closing silently discards a draft after cancellation.");
            window.DiscardConfirmation = delegate { return true; };
            history.SelectedIndex = -1;
            history.SelectedIndex = 0;
            string oldText = window.Editor.Text;
            using (FileStream held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
                window.Editor.Text = "失败时必须保留这段输入";
                Check(!window.SaveCurrent(), "Locked UI save returned success.");
                Check(window.Editor.Text.Contains("保留") && window.NoteCount == 3, "Failed UI save lost draft or mutated history.");
                Check(!window.DeleteSelected() && window.NoteCount == 3, "Failed delete changed memory history.");
            }
            Check(BubbleNoteStorage.Load(path, out notice).Notes[0].Text == oldText, "Failed UI save altered persisted history.");
            Check(window.DeleteSelected() && window.NoteCount == 2, "Confirmed delete does not remove selected note.");
            Check(BubbleNoteStorage.Load(path, out notice).Notes.Count == 2, "Deleted note was not persisted.");
        } finally { window.Close(); }
    }
    private static int Pixels(FrameworkElement view)
    {
        WpfTestRender.Drain();
        view.Measure(new Size(330, 450)); view.Arrange(new Rect(0, 0, 330, 450)); view.UpdateLayout();
        RenderTargetBitmap bitmap = new RenderTargetBitmap(330, 450, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(bitmap, view);
        byte[] bytes = new byte[330 * 450 * 4]; bitmap.CopyPixels(bytes, 330 * 4, 0);
        int visible = 0; for (int i = 3; i < bytes.Length; i += 4) if (bytes[i] > 0) visible++;
        return visible;
    }
    private static void Effects(string directory)
    {
        AiCompanionVisual visual = new AiCompanionVisual();
        visual.Update("gpt6a", 4, true, .7, 0);
        Check(Pixels(visual) > 3000, "Dragon star ring has no visible pixels.");
        Capture(visual, 330, 450, Path.Combine(directory, "gpt6a-focus-ring.png"));
        visual.Update("gpt6a", 4, false, .7, 0); Check(Pixels(visual) == 0, "Cancelled ring remains visible.");
        visual.Update("deepseek", 3, false, 0, 6);
        Check(Pixels(visual) > 500, "Bubble effect has no visible pixels.");
        Capture(visual, 330, 450, Path.Combine(directory, "deepseek-bubbles.png"));
        visual.Update("deepseek", 5, false, 0, 0); Check(Pixels(visual) == 0, "Expired bubbles remain visible.");
        visual.Update("hutao", 5, true, .7, 6); Check(Pixels(visual) == 0, "New effects leak onto original characters.");
    }
    private static T Field<T>(object owner, string name)
    { return (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner); }
    private static void Composite(string directory)
    {
        // Render the real production controls without showing the native pet,
        // activating its movement timer, or changing saved character settings.
        PetWindow pet = new PetWindow();
        try {
            Field<System.Windows.Threading.DispatcherTimer>(pet, "timer").Stop();
            Grid scene = (Grid)((Viewbox)pet.Content).Child;
            scene.Background = new SolidColorBrush(Color.FromRgb(239, 247, 244));
            BitmapSource dragon = PetAssets.Load("gpt6a")["idle"][0];
            Field<Image>(pet, "sprite").Source = dragon;
            Border hud = Field<Border>(pet, "dragonFocusHud"); hud.Visibility = Visibility.Visible;
            Field<TextBlock>(pet, "dragonFocusLabel").Text = "龙息专注  25:00";
            Field<AiCompanionVisual>(pet, "aiCompanionVisual").Update("gpt6a", 2, true, .4, 0);
            Capture((FrameworkElement)pet.Content, 330, 450, Path.Combine(directory, "gpt6a-focus-composite.png"));
            Rect visible = SpeechWindow.FindVisiblePixels(dragon);
            double artTop = 120 + visible.Top * 330;
            Check(hud.Margin.Top + hud.Height < artTop, "Focus countdown covers the dragon's artwork.");
            hud.Visibility = Visibility.Collapsed;
            scene.Background = new SolidColorBrush(Color.FromRgb(243, 249, 255));
            Field<Image>(pet, "sprite").Source = PetAssets.Load("deepseek")["idle"][0];
            Field<AiCompanionVisual>(pet, "aiCompanionVisual").Update("deepseek", 2, false, 0, 5);
            Capture((FrameworkElement)pet.Content, 330, 450, Path.Combine(directory, "deepseek-bubbles-composite.png"));
        } finally { pet.Close(); }
    }
    [STAThread]
    private static int Main()
    {
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string directory = Path.Combine("artifacts", "v28", "abilities-" + DateTime.Now.ToString("HHmmssfff")); Directory.CreateDirectory(directory);
        try {
            Focus(); Storage(directory); NoteWindow(directory); Effects(directory); Composite(directory);
            Console.WriteLine("PASS: " + checks + " focus timing, cancellation, safe local notes, actual note editor and rendered AI effects checks. Previews: " + directory);
            File.WriteAllText(Path.Combine("artifacts", "v28", "abilities-preview-directory.txt"), Path.GetFullPath(directory)); return 0;
        } catch (Exception error) { Console.WriteLine("FAIL: " + error); return 1; }
        finally { app.Shutdown(); }
    }
}
