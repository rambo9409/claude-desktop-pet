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

internal static class KoloPortalTests
{
    private static int checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }

    private static void ClockAndPhysics()
    {
        foreach (double width in new[] { 100.0, 165.0, 450.0 })
        foreach (double dt in new[] { .01, .02, .05 })
        foreach (double release in new[] { -500.0, 0.0, 300.0, 900.0 }) {
            KoloPortalState state = new KoloPortalState(); state.Begin(900, release, width);
            double scale = Math.Max(.6, width / 165), distance = 0; int warps = 0;
            HashSet<KoloPortalPhase> phases = new HashSet<KoloPortalPhase>();
            for (int i = 0; state.Active && i < 400; i++) {
                phases.Add(state.Phase);
                Vector drift;
                if (state.Advance(dt, out drift)) warps++;
                Check(Math.Abs(drift.X) <= 40 * scale * dt + .00001, "Unbounded horizontal release momentum.");
                Check(drift.Y <= 42 * scale * dt + .00001 && drift.Y >= -15 * scale * dt - .00001, "Portal release still plummets or flies upward.");
                Check(state.Opening >= 0 && state.Opening <= 1 && state.BodyOpacity >= 0 && state.BodyOpacity <= 1, "Invalid portal presentation fraction.");
                distance += drift.Y;
            }
            Check(!state.Active && warps == 1, "Portal never completes or relocates more than once.");
            Check(phases.Count == 5, "Portal skipped a visible phase.");
            Check(Math.Abs(distance) < 11 * scale, "The stabilization phase drops too far.");
            Check(Math.Abs(state.Elapsed - KoloPortalState.Duration) < .000001, "Portal duration depends on cadence.");
        }
        KoloPortalState paused = new KoloPortalState(); paused.Begin(80, 300, 165);
        Vector unchanged;
        Check(!paused.Advance(0, out unchanged) && unchanged.Length == 0 && paused.Elapsed == 0, "Zero time advanced the portal.");
        Check(!paused.Advance(Double.NaN, out unchanged) && paused.Elapsed == 0, "NaN advanced the portal.");
        paused.Advance(100, out unchanged);
        Check(paused.Elapsed == .05, "A stalled timer skipped the entire portal.");
        paused.Cancel();
        Check(!paused.Active && paused.BodyOpacity == 1 && paused.BodyReveal == 1 && paused.Opening == 0, "Cancellation leaves a hidden body or visible portal.");
    }

    private static void Destinations()
    {
        List<Rect> monitors = new List<Rect> { new Rect(0,0,1920,1040), new Rect(-1600,-220,1600,1120), new Rect(1920,300,1280,984) };
        foreach (Rect monitor in monitors)
        foreach (Size pet in new[] { new Size(100,136), new Size(165,225), new Size(450,614) })
        foreach (double x in new[] { monitor.Left + 1, monitor.Left + monitor.Width / 2, monitor.Right - 1 }) {
            Point anchor = new Point(x, monitor.Top + 250);
            Point target = KoloPortalGeometry.FloorLocation(anchor, pet, monitors);
            Check(target.X >= monitor.Left && target.X + pet.Width <= monitor.Right, "Portal went to another monitor or crossed its side.");
            Check(Math.Abs(target.Y + pet.Height * .962 - monitor.Bottom) < .00001, "Portal did not put feet on monitor work-area bottom.");
        }
        Point removed = KoloPortalGeometry.FloorLocation(new Point(-900,200), new Size(165,225), new[] { monitors[0] });
        Check(removed.X == 0 && Math.Abs(removed.Y + 225 * .962 - 1040) < .00001, "Removed display did not recover on the nearest surviving display.");
        Rect changed = new Rect(-1280,-300,1280,800);
        Point resized = KoloPortalGeometry.FloorLocation(new Point(-900,200), new Size(330,450), new[] { changed });
        Check(resized.X >= changed.Left && resized.X + 330 <= changed.Right && Math.Abs(resized.Y + 450 * .962 - changed.Bottom) < .00001, "Resized monitor or pet used stale dimensions.");
    }

    private static void Setup(PetWindow pet, Rect monitor)
    {
        Call(pet, "CancelKoloPortal");
        Set(pet, "currentCharacter", "kolo");
        Set(pet, "frames", Call(pet, "LoadCharacterFrames", "kolo"));
        Set(pet, "monitorWorkAreas", new List<Rect> { monitor }); Set(pet, "monitorsCachedAt", DateTime.MaxValue);
        Set(pet, "platformCache", new List<PlatformEdge> { new PlatformEdge { IsFloor = true, Left = monitor.Left, Right = monitor.Right, Y = monitor.Bottom, Bottom = monitor.Bottom } });
        Set(pet, "platformCacheTime", DateTime.MaxValue); Set(pet, "supportHandle", IntPtr.Zero);
        Set(pet, "state", "idle"); Set(pet, "stateEnds", DateTime.MaxValue); Set(pet, "movement", "rest");
        Set(pet, "pointerHeld", false); Set(pet, "autoWalk", false);
        pet.Width = 165; pet.Height = 225; pet.Left = monitor.Left + 300; pet.Top = monitor.Top + 120;
        Dictionary<string, BitmapSource[]> frames = Get<Dictionary<string, BitmapSource[]>>(pet, "frames");
        Get<Image>(pet, "sprite").Source = (BitmapSource)Call(pet, "PrepareNotebookFrame", frames["idle"][0]);
    }

    private static int Snapshot(PetWindow pet, string file)
    {
        WpfTestRender.Drain();
        FrameworkElement content = (FrameworkElement)pet.Content;
        content.Measure(new Size(330,450)); content.Arrange(new Rect(0,0,330,450)); content.UpdateLayout();
        RenderTargetBitmap image = new RenderTargetBitmap(330,450,96,96,PixelFormats.Pbgra32); WpfTestRender.Render(image, content);
        byte[] pixels = new byte[330 * 450 * 4]; image.CopyPixels(pixels,330*4,0); int black = 0;
        for (int y = 418; y < 447; y++) for (int x = 80; x < 250; x++) {
            int p = (y * 330 + x) * 4;
            if (pixels[p+3] > 180 && pixels[p] < 16 && pixels[p+1] < 16 && pixels[p+2] < 16) black++;
        }
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (Stream stream = File.Create(file)) encoder.Save(stream);
        return black;
    }

    private static void Integration(PetWindow pet, string output)
    {
        Rect monitor = new Rect(-1600,-220,1600,1120); Setup(pet, monitor);
        Check(Get<KoloPortalVisual>(pet, "koloPortalVisual") != null, "Main window did not initialize portal renderer.");
        Image sprite = Get<Image>(pet, "sprite");
        // Exercise the production release handler without moving the physical pointer.
        Set(pet, "pointerHeld", true); Set(pet, "dragged", true); Set(pet, "lastDragTime", DateTime.Now);
        Set(pet, "dragVelocityX", 280.0); Set(pet, "dragVelocityY", 300.0);
        Set(pet, "pointerStart", Call(pet, "CursorPosition", new object[] { null }));
        Call(pet, "OnPointerUp", null, new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        KoloPortalState state = Get<KoloPortalState>(pet, "koloPortal");
        Check(state.Active && Get<string>(pet, "movement") == "portal", "Drag release did not enter the portal workflow.");
        double oldTop = pet.Top;
        for (int i = 0; i < 67; i++) Call(pet, "UpdateMovement", DateTime.Now, .02);
        Check(pet.Top - oldTop < 11, "Production release still accelerates downward.");
        Call(pet, "RenderKoloPortal");
        Check(Object.ReferenceEquals(sprite.Source, Get<Dictionary<string,BitmapSource[]>>(pet,"frames")["book-idle"][0]), "Portal lost the held notebook sprite.");
        int openBlack = Snapshot(pet, Path.Combine(output,"kolo-portal-opening.png"));
        Check(openBlack > 800, "Black portal was not visibly rendered below the feet.");
        while (state.Elapsed < 1.64) Call(pet, "UpdateMovement", DateTime.Now, .02);
        Call(pet, "RenderKoloPortal");
        Check(sprite.Opacity > 0 && sprite.Opacity < .8, "Entering body is not gradually dissolving.");
        Snapshot(pet, Path.Combine(output,"kolo-portal-entering.png"));
        while (state.Elapsed < 2.22) Call(pet, "UpdateMovement", DateTime.Now, .02);
        Call(pet, "RenderKoloPortal");
        Check(Math.Abs(pet.Top + pet.Height * .962 - monitor.Bottom) < .00001, "Emergence failed to use the negative-coordinate monitor bottom.");
        Snapshot(pet, Path.Combine(output,"kolo-portal-emerging.png"));
        while (state.Active) Call(pet, "UpdateMovement", DateTime.Now, .02);
        Check(Get<string>(pet,"movement") == "rest" && sprite.Opacity == 1 && sprite.Clip == null && sprite.RenderTransform.Value.IsIdentity, "Completion leaves movement or visual state behind.");
        Check(Snapshot(pet, Path.Combine(output,"kolo-portal-finished.png")) < openBlack / 3, "Portal was not removed after completion.");

        Setup(pet, monitor); Call(pet,"TryBeginKoloPortal",0.0,300.0);
        while (state.Elapsed < 1.65) Call(pet,"UpdateMovement",DateTime.Now,.02);
        Call(pet,"RenderKoloPortal");
        Call(pet,"OnPointerDown",null,new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        Check(!state.Active && Get<string>(pet,"movement") == "held" && sprite.Opacity == 1 && sprite.Clip == null && sprite.RenderTransform.Value.IsIdentity, "Catching a portal did not restore the body as a normal hold.");
        Set(pet,"pointerHeld",false); pet.ReleaseMouseCapture();

        Setup(pet, monitor); Get<NotebookState>(pet,"notebook").Detach();
        Dictionary<string,BitmapSource[]> free = Get<Dictionary<string,BitmapSource[]>>(pet,"frames");
        sprite.Source = free["idle"][0]; Call(pet,"TryBeginKoloPortal",-200.0,300.0);
        while (state.Elapsed < 1.4) Call(pet,"UpdateMovement",DateTime.Now,.02);
        Call(pet,"RenderKoloPortal");
        Check(Object.ReferenceEquals(sprite.Source,free["idle"][0]), "Portal forced an empty-handed Claude to hold a notebook.");
        Call(pet,"CancelKoloPortal");
        Check(!state.Active && sprite.Opacity == 1 && sprite.Clip == null && sprite.RenderTransform.Value.IsIdentity, "Explicit lifecycle cancellation did not restore body visibility.");

        Setup(pet, monitor); Call(pet,"TryBeginKoloPortal",0.0,0.0);
        Set(pet,"monitorWorkAreas",new List<Rect>{new Rect(0,0,1920,1040)});
        while (state.Active) Call(pet,"UpdateMovement",DateTime.Now,.02);
        Check(pet.Left >= 0 && Math.Abs(pet.Top+pet.Height*.962-1040)<.00001, "Display disappearance left the character offscreen.");
        Setup(pet, monitor); Call(pet,"TryBeginKoloPortal",0.0,0.0);
        while(state.Elapsed<2.12) Call(pet,"UpdateMovement",DateTime.Now,.02);
        Call(pet,"ResizePet",1.4,false);
        while(state.Active) Call(pet,"UpdateMovement",DateTime.Now,.02);
        Check(Math.Abs(pet.Top+pet.Height*.962-monitor.Bottom)<.00001, "Resize during emergence left feet floating.");
        Setup(pet, monitor); pet.Top = monitor.Bottom-pet.Height*.962-12;
        Check(!(bool)Call(pet,"TryBeginKoloPortal",0.0,300.0), "Near-floor release unnecessarily teleports.");
        Setup(pet, monitor);
        foreach(string other in new[]{"hutao","ganyu","varesa","gpt6a","deepseek"}) {
            Set(pet,"currentCharacter",other);
            Check(!(bool)Call(pet,"TryBeginKoloPortal",0.0,300.0), "Portal changed another character's fall behavior.");
        }
    }

    private static void Tick(PetWindow pet)
    {
        Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-20));
        Call(pet, "OnTick", null, EventArgs.Empty);
    }

    private static void NotebookPauseIntegration(PetWindow pet)
    {
        // A real HWND is needed for CaptureMouse. Keep this after the synthetic
        // monitor tests: showing the window enables native monitor refreshes.
        pet.ShowActivated = false; pet.Topmost = false; pet.Opacity = 0;
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
        Set(pet, "windowMagicEnabled", false);
        Get<SpeechWindow>(pet, "speechWindow").Opacity = 0;
        pet.Show(); pet.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
        Get<DispatcherTimer>(pet, "timer").Stop();

        foreach (bool takeBook in new[] { false, true }) {
            Get<NotebookState>(pet, "notebook").Recover();
            Setup(pet, new Rect(0, 0, 1920, 1040));
            Check((bool)Call(pet, "TryBeginKoloPortal", 0.0, 300.0), "Notebook regression did not start a portal.");
            KoloPortalState portal = Get<KoloPortalState>(pet, "koloPortal");
            while (portal.Elapsed < 1.1) Call(pet, "UpdateMovement", DateTime.Now, .02);
            Call(pet, "RenderKoloPortal"); pet.UpdateLayout();

            Image sprite = Get<Image>(pet, "sprite");
            BitmapSource bitmap = (BitmapSource)sprite.Source;
            Rect book = PetAssets.NotebookHitBox(bitmap);
            double scale = Math.Min(sprite.ActualWidth / bitmap.Width, sprite.ActualHeight / bitmap.Height);
            Point click = new Point((sprite.ActualWidth - bitmap.Width * scale) / 2 + (book.Left + book.Width / 2) * scale,
                (sprite.ActualHeight - bitmap.Height * scale) / 2 + (book.Top + book.Height / 2) * scale);
            Point press = new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height / 2);
            Check(!book.IsEmpty && (bool)Call(pet, "TryBeginNotebookPress", click, press), "Portal notebook hit was not consumed.");
            NotebookState notebook = Get<NotebookState>(pet, "notebook");
            Check(pet.IsMouseCaptured && notebook.Pressed && notebook.Held, "Portal book press did not establish real WPF capture.");
            Check(portal.Active && Get<string>(pet, "movement") == "portal", "Pressing the notebook cancelled the portal into free fall.");
            if (takeBook) {
                Call(pet, "MoveNotebookPress", new Point(pet.Left + pet.Width + 180, press.Y));
                DetachedNotebookWindow detached = Get<DetachedNotebookWindow>(pet, "notebookWindow");
                Check(!notebook.Held && notebook.Pressed && pet.IsMouseCaptured && detached != null && detached.IsVisible,
                    "Taking the portal notebook did not preserve the captured drag.");
            }

            // The production timer, rather than a direct state-model call, must
            // freeze the whole portal while a held or detached book is pressed.
            double elapsed = portal.Elapsed, top = pet.Top;
            for (int i = 0; i < 12; i++) {
                Tick(pet);
                Check(portal.Active && portal.Elapsed == elapsed && pet.Top == top && Get<string>(pet, "movement") == "portal",
                    "Holding the notebook advanced or cancelled the portal.");
            }
            Call(pet, "OnNotebookUp", null, new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) {
                RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
            Check(!notebook.Pressed && !pet.IsMouseCaptured && notebook.Held == !takeBook,
                "Portal notebook release lost its expected ownership/capture state.");
            Check(portal.Active && Get<string>(pet, "movement") == "portal", "Releasing the notebook switched to free fall.");
            Tick(pet);
            Check(portal.Elapsed > elapsed && portal.Active && Get<string>(pet, "movement") == "portal",
                "The production timer did not resume the paused portal.");
            while (portal.Active) Call(pet, "UpdateMovement", DateTime.Now, .02);
            Check(Get<string>(pet, "movement") == "rest" && sprite.Opacity == 1 && sprite.Clip == null,
                "The resumed portal did not finish safely at the floor.");
            Call(pet, "ReturnNotebook", false);
        }
        pet.Hide();
    }

    [STAThread]
    private static int Main()
    {
        PetWindow pet = null;
        try {
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            string output = "artifacts/v211/portal"; Directory.CreateDirectory(output);
            ClockAndPhysics(); Destinations();
            pet = new PetWindow(); Get<DispatcherTimer>(pet,"timer").Stop();
            Integration(pet,output);
            NotebookPauseIntegration(pet);
            Console.WriteLine("PASS: " + checks + " portal settling, ordered phases, monitor destination, interruption, notebook capture/pause/resume and WPF rendering checks.");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { if(pet!=null) pet.Close(); }
    }
}
