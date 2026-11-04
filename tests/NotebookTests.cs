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

internal static class NotebookTests
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Private).GetValue(owner); }
    private static void Set(object owner, string name, object value) { owner.GetType().GetField(name, Private).SetValue(owner, value); }
    private static object Call(object owner, string name, params object[] args) { return owner.GetType().GetMethod(name, Private).Invoke(owner, args); }
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void MouseButton(Window target, RoutedEvent routedEvent, int clicks)
    {
        MouseButtonEventArgs e = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = routedEvent };
        typeof(MouseButtonEventArgs).GetProperty("ClickCount").GetSetMethod(true).Invoke(e, new object[] { clicks });
        target.RaiseEvent(e);
    }

    private static void DetachedEvents()
    {
        BitmapSource image = BitmapSource.Create(2, 3, 96, 96, PixelFormats.Bgra32, null, new byte[24], 8);
        Point cursor = new Point(-1100, 200); int returns = 0, drops = 0;
        Rect work = new Rect(-1280, -100, 1280, 960);
        DetachedNotebookWindow window = new DetachedNotebookWindow(image, 70, delegate { return cursor; },
            delegate { return work; }, delegate { drops++; return false; }, delegate { returns++; });
        try {
            window.Opacity = 0; window.Topmost = false;
            window.Place(new Point(-1150, 180));
            window.Show();
            MouseButton(window, UIElement.MouseLeftButtonDownEvent, 1);
            Check(window.Dragging && window.IsMouseCaptured, "Detached notebook down handler did not capture the pointer.");
            MouseButton(window, UIElement.MouseLeftButtonUpEvent, 1);
            Check(!window.Dragging && drops == 0, "A detached click remained dragging or falsely dropped.");
            MouseButton(window, UIElement.MouseLeftButtonDownEvent, 1);
            window.Place(new Point(-1800, -400));
            window.ReleaseMouseCapture();
            Check(!window.IsMouseCaptured && !window.Dragging && work.Contains(new Rect(window.Left, window.Top, window.Width, window.Height)), "Lost capture did not clean up and recover the detached notebook position.");
            MouseButton(window, UIElement.MouseLeftButtonDownEvent, 2);
            Check(returns == 1 && !window.Dragging, "Detached notebook double-click did not invoke return.");
        } finally { window.Close(); }
    }

    private sealed class Desktop : IWindowAbilityDesktop
    {
        internal int CandidatesCalled, Moves;
        internal AbilityWindow Target = new AbilityWindow { Handle = new IntPtr(87123), ProcessId = 45, Resizable = true,
            Bounds = new Rect(0, 0, 700, 500), WorkArea = new Rect(0, 0, 1920, 1040) };
        public List<AbilityWindow> Candidates() { CandidatesCalled++; return new List<AbilityWindow> { Target }; }
        public AbilityWindow Refresh(AbilityWindow value) { return Target; }
        public bool RestoreWithoutActivation(AbilityWindow value) { Moves++; return true; }
        public bool SetBounds(AbilityWindow value, Rect bounds) { Moves++; Target.Bounds = bounds; return true; }
    }

    private static void ModelAndGeometry()
    {
        NotebookState state = new NotebookState();
        Check(state.Held && state.BlocksMagic && !state.Pressed, "Startup must hold the notebook and block magic.");
        state.Press(new Point(20, 40)); Check(state.Held && state.Pressed, "Press alone released the notebook.");
        Check(!state.Move(new Point(23, 43)) && state.Held, "A small hand tremor detached the book.");
        Check(!state.Release() && state.Held, "A click released the notebook.");
        state.Press(new Point(20, 40)); Check(state.Move(new Point(40, 40)) && !state.Held && state.BlocksMagic, "Dragging must detach but keep magic paused during drag.");
        Check(!state.Move(new Point(55, 40)), "One drag detached twice.");
        Check(state.Release() && !state.BlocksMagic, "Completed detach did not restore eligibility.");
        state.Recover(); Check(state.Held && state.BlocksMagic && !state.Pressed, "Recovery did not reset the entire gesture.");
        foreach (Size size in new[] { new Size(330, 408), new Size(165, 204), new Size(600, 250) }) {
            double scale = Math.Min(size.Width / 512, size.Height / 512);
            Point expected = new Point(260, 330);
            Point local = new Point((size.Width - 512 * scale) / 2 + expected.X * scale, (size.Height - 512 * scale) / 2 + expected.Y * scale);
            Check((NotebookGeometry.ToImage(local, size, new Size(512, 512)) - expected).Length < .0001, "Letterboxing or scale broke notebook hit testing.");
        }
        foreach (Rect work in new[] { new Rect(0, 0, 1920, 1040), new Rect(-1280, -180, 1280, 984), new Rect(1920, -800, 2560, 1440) }) {
            foreach (Point corner in new[] { work.TopLeft, work.TopRight, work.BottomLeft, work.BottomRight }) {
                Size book = new Size(70, 100); Point safe = NotebookGeometry.Clamp(corner, book, work);
                Check(work.Contains(new Rect(safe, book)), "A notebook can be lost beyond a negative or offset monitor.");
            }
            Rect pet = new Rect(work.Left + 40, work.Top + 80, 165, 225);
            Check(NotebookGeometry.ReturnArea(pet).Contains(new Point(pet.Left + 82, pet.Bottom - 50)), "Return area does not include Claude's body.");
        }
    }

    private static void Capture(PetWindow pet, string path)
    {
        WpfTestRender.Drain();
        FrameworkElement view = (FrameworkElement)pet.Content;
        view.Measure(new Size(330, 450)); view.Arrange(new Rect(0, 0, 330, 450)); view.UpdateLayout();
        RenderTargetBitmap image = new RenderTargetBitmap(330, 450, 96, 96, PixelFormats.Pbgra32);
        DrawingVisual backdrop = new DrawingVisual(); using (DrawingContext draw = backdrop.RenderOpen()) draw.DrawRectangle(Brushes.Linen, null, new Rect(0, 0, 330, 450));
        WpfTestRender.Render(image, backdrop); WpfTestRender.Render(image, view);
        PngBitmapEncoder png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using (FileStream file = File.Create(path)) png.Save(file);
    }

    private static void Integration(string directory)
    {
        string settings = Path.Combine(PetStorage.RootDirectory, "character.txt");
        byte[] previous = File.Exists(settings) ? File.ReadAllBytes(settings) : null;
        PetWindow pet = new PetWindow { ShowActivated = false, Opacity = 0, Topmost = false };
        try {
            Get<DispatcherTimer>(pet, "timer").Stop(); Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
            Get<SpeechWindow>(pet, "speechWindow").Opacity = 0;
            Set(pet, "currentCharacter", "kolo");
            Dictionary<string, BitmapSource[]> frames = PetAssets.Load("kolo"); Set(pet, "frames", frames);
            NotebookState state = Get<NotebookState>(pet, "notebook");
            Check(frames.ContainsKey("notebook") && frames.ContainsKey("book-idle"), "Production book sprites are missing.");
            foreach (string action in new[] { "idle", "happy", "pet", "rose", "typing", "stroll", "walk", "climb-up", "climb-down" }) {
                Check(frames["book-" + action].Length == frames[action].Length, "Held sequence length differs: " + action);
                for (int i = 0; i < frames[action].Length; i++) {
                    BitmapSource held = (BitmapSource)Call(pet, "PrepareNotebookFrame", frames[action][i]);
                    Check(Object.ReferenceEquals(held, frames["book-" + action][i]), "Renderer ignores current held pose: " + action);
                    Rect hit = PetAssets.NotebookHitBox(held);
                    Check(!hit.IsEmpty && hit.Width > 20 && hit.Height > 20 && new Rect(0, 0, 512, 512).Contains(hit), "Held pose has unusable hitbox.");
                }
            }
            state.Detach(); Check(Object.ReferenceEquals(Call(pet, "PrepareNotebookFrame", frames["idle"][0]), frames["idle"][0]), "Empty hands still show a notebook."); state.Recover();
            Desktop desktop = new Desktop(); DateTime now = DateTime.UtcNow;
            WindowAbilityController controller = new WindowAbilityController(desktop, new WindowAbilityClock(now.AddMinutes(-1), DateTime.MinValue), new Random(1), null);
            Set(pet, "windowAbility", controller); Set(pet, "windowMagicEnabled", true);
            Set(pet, "quietMode", false); Set(pet, "autoWalk", false); Set(pet, "state", "idle"); Set(pet, "movement", "rest");
            pet.Show(); pet.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
            Get<DispatcherTimer>(pet, "timer").Stop(); pet.Left = 400; pet.Top = 200;
            Call(pet, "UpdateWindowAbility", DateTime.Now, .05);
            Check(desktop.CandidatesCalled == 0 && !controller.Active, "Holding a book enumerated or manipulated desktop windows.");
            state.Detach(); Set(pet, "windowMagicEnabled", false); Call(pet, "UpdateWindowAbility", DateTime.Now, .05);
            Check(desktop.CandidatesCalled == 0, "Taking the notebook bypassed disabled window magic.");
            Set(pet, "windowMagicEnabled", true); Set(pet, "quietMode", true); Call(pet, "UpdateWindowAbility", DateTime.Now, .05);
            Check(desktop.CandidatesCalled == 0, "Taking the notebook bypassed quiet mode.");
            Set(pet, "quietMode", false); Call(pet, "UpdateWindowAbility", DateTime.Now, .05);
            Check(desktop.CandidatesCalled == 1 && controller.Active, "Empty hands cannot use the existing ready spell.");
            DateTime cooldown = controller.Clock.NextUseUtc;
            Call(pet, "ReturnNotebook", false);
            Check(!controller.Active && state.Held && controller.Clock.NextUseUtc == cooldown, "Returning a book failed to cancel or rewrote the cooldown.");
            int calls = desktop.CandidatesCalled, moves = desktop.Moves;
            for (int i = 0; i < 50; i++) Call(pet, "UpdateWindowAbility", DateTime.Now, .05);
            Check(desktop.CandidatesCalled == calls && desktop.Moves == moves, "A cancelled spell continued manipulating the mock window.");

            Set(pet, "windowMagicEnabled", false);
            Image sprite = Get<Image>(pet, "sprite"); sprite.Source = frames["book-idle"][0]; pet.UpdateLayout();
            Rect box = PetAssets.NotebookHitBox((BitmapSource)sprite.Source);
            double scale = Math.Min(sprite.ActualWidth / 512, sprite.ActualHeight / 512);
            Point click = new Point((sprite.ActualWidth - 512 * scale) / 2 + (box.Left + box.Width / 2) * scale,
                (sprite.ActualHeight - 512 * scale) / 2 + (box.Top + box.Height / 2) * scale);
            Check(!(bool)Call(pet, "NotebookHit", new Point(-20, -20)), "Outside the book captured normal pet dragging.");
            PetTapSequence taps = Get<PetTapSequence>(pet, "tapSequence"); taps.Begin("kolo", 1); taps.Complete("kolo");
            Check((bool)Call(pet, "TryBeginNotebookPress", click, new Point(500, 400)), "The actual held book center is not interactive.");
            Check(pet.IsMouseCaptured && state.Pressed && state.Held, "A synchronous WPF capture update released the notebook press.");
            Check(!Get<bool>(pet, "pointerHeld"), "Notebook press became a pet drag.");
            Set(pet, "movement", "fall"); Set(pet, "fallVelocityY", 600.0); Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-50));
            double standingTop = pet.Top;
            Call(pet, "OnTick", null, EventArgs.Empty);
            Check(pet.Top == standingTop, "The pet moved away while its notebook was pressed: " + standingTop + " -> " + pet.Top + "; pressed=" + state.Pressed + "; held=" + state.Held + "; capture=" + pet.IsMouseCaptured);
            Set(pet, "movement", "rest");
            taps.Begin("kolo", 2); Check(!taps.Complete("kolo"), "Notebook press joined a pet double-click sequence.");
            Call(pet, "MoveNotebookPress", new Point(503, 401));
            Call(pet, "FinishNotebookPress", false); Check(state.Held && Get<DetachedNotebookWindow>(pet, "notebookWindow") == null, "A book click created a detached window.");
            Check((bool)Call(pet, "TryBeginNotebookPress", click, new Point(500, 400)), "Second notebook press failed.");
            Call(pet, "MoveNotebookPress", new Point(650, 400));
            DetachedNotebookWindow interrupted = Get<DetachedNotebookWindow>(pet, "notebookWindow");
            Check(interrupted != null && interrupted.IsVisible && pet.IsMouseCaptured && state.Pressed, "Showing the detached window interrupted the pet's active drag.");
            pet.ReleaseMouseCapture();
            Check(state.Held && !state.Pressed && !interrupted.IsVisible && Get<DetachedNotebookWindow>(pet, "notebookWindow") == null, "Losing the hand-to-desktop capture left a notebook or enabled magic.");
            Check((bool)Call(pet, "TryBeginNotebookPress", click, new Point(500, 400)), "Cannot start a fresh drag after losing capture.");
            Call(pet, "MoveNotebookPress", new Point(-800, 200));
            DetachedNotebookWindow floating = Get<DetachedNotebookWindow>(pet, "notebookWindow");
            Check(!state.Held && floating != null && state.Pressed, "Dragging does not produce a standalone notebook.");
            floating.Opacity = 0;
            Call(pet, "FinishNotebookPress", false);
            Check(!state.Held && !Get<bool>(pet, "windowMagicEnabled"), "Taking a book enabled magic without the user's switch.");
            MouseButton(floating, UIElement.MouseLeftButtonDownEvent, 1);
            Check(floating.Dragging && (bool)typeof(PetWindow).GetProperty("NotebookBlocksMagic", Private).GetValue(pet, null), "Dragging the detached window failed to block magic.");
            Set(pet, "windowMagicEnabled", true); calls = desktop.CandidatesCalled;
            Call(pet, "UpdateWindowAbility", DateTime.Now, .05);
            Check(desktop.CandidatesCalled == calls && !controller.Active, "An actual detached-window drag allowed a spell.");
            MouseButton(floating, UIElement.MouseLeftButtonUpEvent, 1);
            Check(!floating.Dragging, "Detached notebook up handler failed to release its state.");
            Set(pet, "windowMagicEnabled", false);
            Check(!(bool)Call(pet, "DropNotebook", new Point(-500, -500)), "An unrelated drop returned the notebook.");
            Check((bool)Call(pet, "DropNotebook", new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height * .7)), "Dropping onto Claude did not return the notebook.");
            Check(state.Held && Get<DetachedNotebookWindow>(pet, "notebookWindow") == null && !floating.IsVisible, "Returned book leaked a desktop window.");
            sprite.Source = frames["book-idle"][0];
            Call(pet, "TryBeginNotebookPress", click, new Point(500, 400)); Call(pet, "MoveNotebookPress", new Point(1000, 200)); Call(pet, "FinishNotebookPress", false);
            floating = Get<DetachedNotebookWindow>(pet, "notebookWindow");
            MouseButton(floating, UIElement.MouseLeftButtonDownEvent, 2);
            Check(state.Held && Get<DetachedNotebookWindow>(pet, "notebookWindow") == null, "Production detached-window double click did not return the book.");
            Call(pet, "ReturnNotebook", false); Check(state.Held, "Repeated return is not safe.");
            sprite.Source = frames["book-idle"][0]; sprite.RenderTransform = Transform.Identity;
            Capture(pet, Path.Combine(directory, "kolo-holding.png"));
            Transform priorTransform = sprite.RenderTransform;
            foreach (double facing in new[] { -1.0, 1.0 }) {
                TransformGroup transforms = new TransformGroup(); transforms.Children.Add(new ScaleTransform(facing, 1)); transforms.Children.Add(new RotateTransform(13));
                sprite.RenderTransform = transforms;
                GeneralTransform forward = sprite.TransformToAncestor(pet);
                Point drawn = forward.Transform(click), inverted = forward.Inverse.Transform(drawn);
                Check((bool)Call(pet, "NotebookHit", inverted), "Mirroring/tilting the sprite breaks WPF-local hit coordinates.");
            }
            sprite.RenderTransform = priorTransform;
            state.Detach(); sprite.Source = frames["idle"][0]; Capture(pet, Path.Combine(directory, "kolo-empty-hands.png"));
            Call(pet, "SwitchCharacter", "ganyu"); Check(state.Held, "Switching characters left the book released.");
            state.Detach(); pet.Hide(); Check(state.Held, "Hiding failed to recover the book.");
        } finally {
            pet.Close();
            if (previous == null) { if (File.Exists(settings)) File.Delete(settings); } else File.WriteAllBytes(settings, previous);
        }
    }
    [STAThread]
    private static int Main()
    {
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string directory = Path.Combine("artifacts", "v29", "notebook"); Directory.CreateDirectory(directory);
        try { ModelAndGeometry(); DetachedEvents(); Integration(directory); Console.WriteLine("PASS: " + checks + " notebook ownership, precise hitboxes, drag/return, magic gating, mock cancellation, cleanup and render checks."); return 0; }
        catch (Exception error) { Console.WriteLine("FAIL: " + error); return 1; }
        finally { app.Shutdown(); }
    }
}
