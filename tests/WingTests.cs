using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class WingTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static object Get(PetWindow pet, string name) { return typeof(PetWindow).GetField(name, Private).GetValue(pet); }
    private static void Set(PetWindow pet, string name, object value) { typeof(PetWindow).GetField(name, Private).SetValue(pet, value); }
    private static object Call(PetWindow pet, string name, params object[] args) { return typeof(PetWindow).GetMethod(name, Private).Invoke(pet, args); }
    private static void Require(bool value, string text) { if (!value) throw new Exception(text); checks++; }
    private static Dictionary<string, BitmapSource[]> Frames(PetWindow pet) { return (Dictionary<string, BitmapSource[]>)Get(pet, "frames"); }
    private static Image Body(PetWindow pet) { return (Image)Get(pet, "sprite"); }
    private static Image Wing(PetWindow pet, bool right) { return (Image)Get(pet, right ? "rightWingLayer" : "wingLayer"); }
    private static GlideAnimation Clock(PetWindow pet) { return (GlideAnimation)Get(pet, "glideAnimation"); }

    private static void Character(PetWindow pet, string character)
    {
        Set(pet, "currentCharacter", character);
        Set(pet, "frames", Call(pet, "LoadCharacterFrames", character));
        Call(pet, "CancelButterflyEffects");
        Set(pet, "state", "idle");
        Set(pet, "movement", "rest");
        Body(pet).Source = Frames(pet)["idle"][0];
        Body(pet).RenderTransform = Transform.Identity;
    }

    private static Transform Pose(string movement, int direction, int angleSign)
    {
        TransformGroup pose = new TransformGroup();
        pose.Children.Add(new ScaleTransform(direction * (movement == "held" ? 0.98 : 1), movement == "held" ? 1.035 : 1));
        if (movement != "rest") pose.Children.Add(new RotateTransform(angleSign * (movement == "held" ? 13 : 18)));
        if (movement == "held") pose.Children.Add(new TranslateTransform(0, 4));
        return pose;
    }

    private static void Render(PetWindow pet, Transform pose, double seconds)
    {
        BitmapSource requested = Frames(pet)["idle"][0];
        BitmapSource prepared = (BitmapSource)Call(pet, "PrepareGlidingFrame", requested, seconds);
        Call(pet, "RenderButterflyEffects", prepared, pose, seconds);
        Call(pet, "RenderWings", pose);
    }

    private static void Settle(PetWindow pet, Transform pose)
    {
        for (int i = 0; i < 16; i++) Render(pet, pose, 0.05);
    }

    private static void VerifyClock()
    {
        GlideAnimation animation = new GlideAnimation();
        Require(animation.Phase == GlidePhase.Hidden && animation.BodyFrame == -1, "A new glider is visible.");
        HashSet<int> bodyFrames = new HashSet<int>();
        animation.Advance(true, -1);
        Require(animation.Openness == 0, "Negative elapsed time moved the wings.");
        for (int step = 0; step < 100; step++)
        {
            animation.Advance(true, 0.02);
            bodyFrames.Add(animation.BodyFrame);
            Require(animation.Openness >= 0 && animation.Openness <= 1, "Deployment escaped its bounds.");
        }
        Require(animation.Phase == GlidePhase.Glide && animation.Openness == 1, "Deployment did not finish.");
        foreach (int frame in new[] { 0, 1, 2, 3, 4, 5 }) Require(bodyFrames.Contains(frame), "Air animation skipped body frame " + frame);
        List<int> cycle = new List<int>();
        for (int i = 0; i < 96; i++)
        {
            animation.Advance(true, 0.01);
            int frame = animation.BodyFrame;
            if (cycle.Count == 0 || cycle[cycle.Count - 1] != frame) cycle.Add(frame);
        }
        for (int i = 1; i < cycle.Count; i++) Require(cycle[i] == 3 || cycle[i - 1] == 3,
            "Gliding must return through neutral between left and right balance poses.");
        Require(cycle.Contains(4) && cycle.Contains(5), "Gliding did not balance to both sides.");
        animation.Advance(false, 0.02);
        Require(animation.Phase == GlidePhase.Retract && animation.BodyFrame == 6 && animation.Openness < 1, "Landing did not start a retract pose.");
        for (int step = 0; step < 22; step++)
        {
            animation.Advance(false, 0.02);
            bodyFrames.Add(animation.BodyFrame);
        }
        Require(bodyFrames.Contains(7) && animation.Phase == GlidePhase.Hidden, "Landing crouch or cleanup was skipped.");

        animation.Advance(true, 0.05);
        double partial = animation.Openness;
        animation.Advance(false, 0.01);
        Require(animation.Openness > 0 && animation.Openness < partial, "Short fall must fold from its current opening.");
        partial = animation.Openness;
        animation.Advance(true, 0.01);
        Require(animation.Openness > partial && animation.Openness < 0.2, "A re-grab restarted or fully opened a partial glider.");
        animation.Reset();
        animation.Advance(true, 10);
        Require(animation.Openness < 0.1, "A delayed timer tick skipped deployment.");
    }

    private static void VerifyPointerRegrab(PetWindow pet)
    {
        pet.Left = 500;
        pet.Top = 400;
        foreach (string character in new[] { "hutao", "ganyu", "varesa" })
        {
            Character(pet, character);
            foreach (string previous in new[] { "fall", "held" })
            {
                Set(pet, "movement", previous);
                Settle(pet, Transform.Identity);
                double airborneTime = Clock(pet).AirTime;
                Call(pet, "OnPointerDown", pet, PointerEvent(true));
                Require((string)Get(pet, "movement") == "held" && (bool)Get(pet, "dragged") && (bool)Get(pet, "pointerHeld"),
                    "Catching a " + previous + " character was treated as a ground click.");
                // No MouseMove is sent. Run 600ms of the real tick path while
                // the pointer remains at its initial location, beyond both
                // the retract and landing durations.
                for (int step = 0; step < 12; step++)
                {
                    Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-50));
                    Call(pet, "OnTick", null, EventArgs.Empty);
                    Require(Clock(pet).Phase == GlidePhase.Glide && Clock(pet).Openness == 1,
                        "A stationary midair catch folded the wings or replayed deployment.");
                }
                Require(Clock(pet).AirTime >= airborneTime + 0.59, "Catching reset the airborne animation clock.");
                Call(pet, "OnPointerUp", pet, PointerEvent(false));
                Require((string)Get(pet, "movement") == "fall" && (string)Get(pet, "state") == "idle" && !(bool)Get(pet, "pointerHeld"),
                    "Releasing a stationary midair catch did not continue falling.");
                Render(pet, Transform.Identity, 0.016);
                Require(Clock(pet).Phase == GlidePhase.Glide && Clock(pet).Openness == 1, "Release replayed the wing deployment.");
            }
            Call(pet, "CancelWings");
            Set(pet, "movement", "jump");
            Call(pet, "OnPointerDown", pet, PointerEvent(true));
            Require((string)Get(pet, "movement") == "held" && (bool)Get(pet, "dragged"), "Catching a jump was treated as landing.");
            Call(pet, "OnPointerUp", pet, PointerEvent(false));
            Require((string)Get(pet, "movement") == "fall", "Releasing a caught jump did not fall.");

            Set(pet, "movement", "rest");
            Call(pet, "OnPointerDown", pet, PointerEvent(true));
            Require((string)Get(pet, "movement") == "rest" && !(bool)Get(pet, "dragged"), "A ground click was misclassified as airborne.");
            Call(pet, "OnPointerUp", pet, PointerEvent(false));
            Require((string)Get(pet, "movement") == "rest" && (string)Get(pet, "state") == "happy" && !(bool)Get(pet, "pointerHeld"),
                "A normal ground click lost its original interaction.");
        }
    }

    private static MouseButtonEventArgs PointerEvent(bool pressed)
    {
        return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) {
            RoutedEvent = pressed ? Mouse.MouseDownEvent : Mouse.MouseUpEvent
        };
    }

    private static BitmapSource Capture(PetWindow pet)
    {
        FrameworkElement content = (FrameworkElement)pet.Content;
        content.Measure(new Size(pet.Width, pet.Height));
        content.Arrange(new Rect(0, 0, pet.Width, pet.Height));
        content.UpdateLayout();
        RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(pet.Width), (int)Math.Ceiling(pet.Height), 96, 96, PixelFormats.Pbgra32);
        DrawingVisual background = new DrawingVisual();
        using (DrawingContext dc = background.RenderOpen())
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 31, 45)), null, new Rect(0, 0, pet.Width, pet.Height));
        bitmap.Render(background);
        bitmap.Render(content);
        bitmap.Freeze();
        return bitmap;
    }

    private static int ChangedPixels(BitmapSource first, BitmapSource second, bool borderOnly)
    {
        int width = first.PixelWidth, height = first.PixelHeight, stride = width * 4;
        byte[] a = new byte[stride * height], b = new byte[stride * height];
        first.CopyPixels(a, stride, 0);
        second.CopyPixels(b, stride, 0);
        int changed = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (borderOnly && x >= 1 && x < width - 1 && y >= 1 && y < height - 1) continue;
                int i = y * stride + x * 4;
                if (Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2]) > 24) changed++;
            }
        return changed;
    }

    private static BitmapSource WithoutWings(PetWindow pet)
    {
        Wing(pet, false).Visibility = Wing(pet, true).Visibility = Visibility.Collapsed;
        BitmapSource body = Capture(pet);
        Wing(pet, false).Visibility = Wing(pet, true).Visibility = Visibility.Visible;
        return body;
    }

    private static void VerifyAnchor(PetWindow pet)
    {
        Grid root = (Grid)((Viewbox)pet.Content).Child;
        Image sprite = Body(pet);
        foreach (bool right in new[] { false, true })
        {
            Image wing = Wing(pet, right);
            Require(root.Children.IndexOf(wing) < root.Children.IndexOf(sprite), "Wing must stay behind the person.");
            Require(!wing.IsHitTestVisible, "Wing intercepted dragging.");
            Require(wing.Margin == sprite.Margin && wing.RenderTransformOrigin == sprite.RenderTransformOrigin &&
                Math.Abs(wing.ActualWidth - sprite.ActualWidth) < 0.01 && Math.Abs(wing.ActualHeight - sprite.ActualHeight) < 0.01,
                "Wing and body have different canvas layouts.");
            Point wingRoot = wing.TransformToAncestor(root).Transform(new Point(wing.ActualWidth * 0.5, wing.ActualHeight * 340.0 / 512.0));
            double attachmentY = (double)Call(pet, "WingAttachmentY");
            Point bodyRoot = sprite.TransformToAncestor(root).Transform(new Point(sprite.ActualWidth * 0.5, sprite.ActualHeight * attachmentY / 512.0));
            Require((wingRoot - bodyRoot).Length < 0.01, "Folding or pose transform detached the shoulder hinge.");
        }
        TransformGroup left = (TransformGroup)Wing(pet, false).RenderTransform;
        TransformGroup other = (TransformGroup)Wing(pet, true).RenderTransform;
        Require(Math.Abs(((RotateTransform)left.Children[1]).Angle + ((RotateTransform)other.Children[1]).Angle) < 0.0001,
            "The two wings must hinge in opposite directions.");
    }

    private static void Save(BitmapSource bitmap, string path)
    {
        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(path)) encoder.Save(stream);
    }

    private static BitmapSource Contact(IList<BitmapSource> images, IList<string> captions, int columns, int cellWidth)
    {
        int rows = (images.Count + columns - 1) / columns;
        int cellHeight = cellWidth * 450 / 330;
        DrawingVisual drawing = new DrawingVisual();
        using (DrawingContext dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 31, 45)), null, new Rect(0, 0, columns * cellWidth, rows * cellHeight));
            for (int i = 0; i < images.Count; i++)
            {
                int x = i % columns * cellWidth, y = i / columns * cellHeight;
                dc.DrawImage(images[i], new Rect(x, y, cellWidth, cellHeight));
                dc.DrawText(new FormattedText(captions[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"), cellWidth / 18.0, Brushes.WhiteSmoke, 1.0), new Point(x + 12, y + 18));
            }
        }
        RenderTargetBitmap bitmap = new RenderTargetBitmap(columns * cellWidth, rows * cellHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        bitmap.Freeze();
        return bitmap;
    }

    [STAThread]
    private static void Main()
    {
        try { Run(); }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error.GetType().FullName + ": " + error.Message);
            if (error.InnerException != null) Console.Error.WriteLine("Cause: " + error.InnerException.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void Run()
    {
        VerifyClock();
        PetWindow pet = new PetWindow();
        pet.Width = 330;
        pet.Height = 450;
        ((DispatcherTimer)Get(pet, "timer")).Stop();
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
        string[] characters = { "hutao", "ganyu", "varesa" };
        string[] names = { "胡桃", "甘雨", "瓦雷莎" };
        try
        {
            foreach (string character in characters)
            {
                Character(pet, character);
                Require(Frames(pet)["glide"].Length == 8, "Missing eight-frame body sequence: " + character);
                foreach (string movement in new[] { "held", "fall" })
                {
                    Call(pet, "CancelWings");
                    Set(pet, "movement", movement);
                    Transform pose = Pose(movement, -1, 1);
                    Render(pet, pose, 0.016);
                    Require(Clock(pet).Phase == GlidePhase.Deploy && ReferenceEquals(Body(pet).Source, Frames(pet)["glide"][0]),
                        character + " did not raise its arms before gliding.");
                    Require(Wing(pet, false).Opacity > 0 && Wing(pet, false).Opacity < 1, "Wing appeared at full opacity.");
                    Capture(pet);
                    VerifyAnchor(pet);
                    double clock = Clock(pet).AirTime;
                    for (int i = 0; i < 4; i++) Call(pet, "RenderWings", pose);
                    Require(Clock(pet).AirTime == clock, "Rendering advanced the wing clock twice.");
                    Settle(pet, pose);
                    Require(Clock(pet).Phase == GlidePhase.Glide && Wing(pet, false).Opacity == 1,
                        character + " did not settle into gliding.");
                    Require(ReferenceEquals(Body(pet).Source, Frames(pet)["glide"][Clock(pet).BodyFrame]), "Body and wings use different clocks.");
                    BitmapSource opened = Capture(pet), closed = WithoutWings(pet);
                    Require(ChangedPixels(opened, closed, false) > 1000, character + " wings are hidden by hair or outside the canvas.");
                    Require(ChangedPixels(opened, closed, true) == 0, character + " wing clipped at the boundary.");
                    VerifyAnchor(pet);
                }

                Set(pet, "movement", "held");
                Settle(pet, Transform.Identity);
                double airborneTime = Clock(pet).AirTime;
                Set(pet, "movement", "fall");
                Render(pet, Transform.Identity, 0.016);
                Require(Clock(pet).Phase == GlidePhase.Glide && Clock(pet).AirTime > airborneTime && Clock(pet).Openness == 1,
                    "Releasing drag replayed wing deployment.");
                Set(pet, "movement", "rest");
                Render(pet, Transform.Identity, 0.016);
                Require(Clock(pet).Phase == GlidePhase.Retract && ReferenceEquals(Body(pet).Source, Frames(pet)["glide"][6]),
                    "Landing skipped the person lowering their arms.");
                Capture(pet);
                VerifyAnchor(pet);
                Require((double)Call(pet, "WingAttachmentY") == (character == "hutao" ? 324 : character == "ganyu" ? 323 : 316),
                    "Retracting wings did not follow the painted shoulders.");
                Settle(pet, Transform.Identity);
                Require(Clock(pet).Phase == GlidePhase.Hidden && Wing(pet, false).Source == null &&
                    ReferenceEquals(Body(pet).Source, Frames(pet)["idle"][0]), "Landing retained the air pose.");

                foreach (string next in new[] { "walk", "climb", "jump" })
                {
                    Set(pet, "movement", "held");
                    Settle(pet, Transform.Identity);
                    Set(pet, "movement", next);
                    Render(pet, Transform.Identity, 0.016);
                    Require(Clock(pet).Phase == GlidePhase.Hidden && Wing(pet, true).Source == null, "Movement command kept wings: " + next);
                }
                Set(pet, "movement", "held");
                Settle(pet, Transform.Identity);
                Set(pet, "state", "happy");
                Render(pet, Transform.Identity, 0.016);
                Require(Clock(pet).Phase == GlidePhase.Hidden && Wing(pet, false).Source == null, "Interaction retained wings.");
                Set(pet, "state", "idle");

                foreach (double size in new[] { 100.0, 165.0, 330.0, 450.0 })
                foreach (string movement in new[] { "held", "fall" })
                foreach (int direction in new[] { -1, 1 })
                foreach (int angleSign in new[] { -1, 1 })
                {
                    pet.Width = size;
                    pet.Height = size * 450.0 / 330.0;
                    Call(pet, "CancelWings");
                    Set(pet, "movement", movement);
                    Transform pose = Pose(movement, direction, angleSign);
                    for (int phase = 0; phase < 3; phase++)
                    {
                        for (int step = 0; step < 5; step++) Render(pet, pose, 0.05);
                        BitmapSource opened = Capture(pet), closed = WithoutWings(pet);
                        Require(ChangedPixels(opened, closed, true) == 0, character + " wing clipped at size " + size + " / " + movement + " / phase " + phase);
                        VerifyAnchor(pet);
                    }
                }
                pet.Width = 330;
                pet.Height = 450;
            }

            Character(pet, "hutao");
            Set(pet, "movement", "held");
            Settle(pet, Transform.Identity);
            Character(pet, "ganyu");
            Render(pet, Transform.Identity, 0.016);
            Require(Clock(pet).Phase == GlidePhase.Hidden && Wing(pet, false).Source == null, "Switch retained Hu Tao wings.");
            Set(pet, "movement", "fall");
            Settle(pet, Transform.Identity);
            Require(ReferenceEquals(Wing(pet, true).Source, Frames(pet)["glider"][0]), "Ganyu got the previous palette.");
            Character(pet, "kolo");
            foreach (string movement in new[] { "held", "fall", "rest" })
            {
                Set(pet, "movement", movement);
                Settle(pet, Transform.Identity);
                Require(Clock(pet).Phase == GlidePhase.Hidden && Wing(pet, false).Source == null &&
                    ReferenceEquals(Body(pet).Source, Frames(pet)["idle"][0]), "Default Kolo gained wings or a gliding body.");
            }

            // Exercise the real OnTick integration, not only individual helpers.
            Character(pet, "ganyu");
            Set(pet, "movement", "held");
            Set(pet, "pointerHeld", true);
            Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-20));
            Call(pet, "OnTick", null, EventArgs.Empty);
            Require(Clock(pet).Phase == GlidePhase.Deploy && Clock(pet).Openness < 0.10 &&
                ReferenceEquals(Body(pet).Source, Frames(pet)["glide"][0]), "OnTick does not advance the shared body/wing clock once.");
            Set(pet, "pointerHeld", false);

            VerifyPointerRegrab(pet);

            Directory.CreateDirectory("artifacts/tests/wing-frames");
            List<BitmapSource>[] animation = new List<BitmapSource>[3];
            List<BitmapSource> contact = new List<BitmapSource>();
            List<string> contactLabels = new List<string>();
            int[] contactSteps = { 1, 5, 10, 17, 23, 35, 89, 94 };
            string[] poseNames = { "抬臂", "展开", "受风", "滑翔 A", "滑翔 B", "滑翔 C", "收翼", "落地" };
            for (int character = 0; character < characters.Length; character++)
            {
                Character(pet, characters[character]);
                Call(pet, "CancelWings");
                animation[character] = new List<BitmapSource>();
                for (int step = 0; step < 120; step++)
                {
                    string movement = step < 48 ? "held" : step < 88 ? "fall" : "rest";
                    Set(pet, "movement", movement);
                    // Preview uses the actual rendering path; the gentle angle
                    // keeps the newly painted arm and leg poses easy to inspect.
                    TransformGroup pose = new TransformGroup();
                    pose.Children.Add(new RotateTransform(movement == "rest" ? 0 : Math.Sin(step * 0.08) * 4));
                    Render(pet, pose, 0.04);
                    animation[character].Add(Capture(pet));
                }
                for (int i = 0; i < contactSteps.Length; i++)
                {
                    contact.Add(animation[character][contactSteps[i]]);
                    contactLabels.Add(names[character] + " / " + poseNames[i]);
                }
            }
            Save(Contact(contact, contactLabels, 8, 220), "artifacts/tests/wings-contact.png");
            for (int step = 0; step < 120; step++)
            {
                List<BitmapSource> row = new List<BitmapSource>();
                List<string> labels = new List<string>();
                for (int character = 0; character < characters.Length; character++)
                {
                    row.Add(animation[character][step]);
                    labels.Add(names[character] + (step < 14 ? " / 展翼" : step < 48 ? " / 拖拽滑翔" : step < 88 ? " / 下落滑翔" : step < 94 ? " / 收翼" : step < 99 ? " / 落地" : " / 待机"));
                }
                Save(Contact(row, labels, 3, 330), "artifacts/tests/wing-frames/" + step.ToString("D3") + ".png");
            }
            Console.WriteLine("PASS: " + checks + " glider phase/frame sequence, one shared clock, drag-to-fall continuity, short fall/re-grab, interruption, palettes, hinge anchors, all sizes/directions and WPF clipping checks.");
        }
        finally { pet.Close(); }
    }
}
