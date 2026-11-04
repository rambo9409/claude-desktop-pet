using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudePet;

internal static class PointerInteractionTests
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks, saves;
    private static PetWindow pet;
    private static WindowAbilityController controller;
    private static readonly SafeDesktop desktop = new SafeDesktop();

    // Deliberately cannot enumerate or mutate any native window. This suite
    // drives the production pointer handlers on an unshown WPF pet.
    private sealed class SafeDesktop : IWindowAbilityDesktop
    {
        internal int Calls;
        private Exception Unexpected() { Calls++; return new Exception("Pointer input attempted to operate a desktop window."); }
        public List<AbilityWindow> Candidates() { throw Unexpected(); }
        public AbilityWindow Refresh(AbilityWindow window) { throw Unexpected(); }
        public bool RestoreWithoutActivation(AbilityWindow window) { throw Unexpected(); }
        public bool SetBounds(AbilityWindow window, Rect bounds) { throw Unexpected(); }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    private static T Get<T>(string field) { return (T)typeof(PetWindow).GetField(field, Private).GetValue(pet); }
    private static void Set(string field, object value) { typeof(PetWindow).GetField(field, Private).SetValue(pet, value); }
    private static void Call(string method, params object[] args) { typeof(PetWindow).GetMethod(method, Private).Invoke(pet, args); }

    private static Point Cursor()
    {
        NativeWindow.POINT cursor;
        if (!NativeWindow.GetCursorPos(out cursor)) throw new Exception("The read-only cursor query failed.");
        return Get<Matrix>("fromDevice").Transform(new Point(cursor.X, cursor.Y));
    }

    private static MouseButtonEventArgs Button(int count, MouseButton button)
    {
        MouseButtonEventArgs result = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button);
        result.RoutedEvent = Mouse.MouseDownEvent;
        // WPF normally fills this internal property from its system click-time
        // and position rules. Set only that input metadata, never a cursor or
        // operating-system button state.
        typeof(MouseButtonEventArgs).GetProperty("ClickCount").GetSetMethod(true).Invoke(result, new object[] { count });
        return result;
    }

    private static void Down(int count)
    {
        MouseButtonEventArgs input = Button(count, MouseButton.Left);
        Call("OnPointerDown", pet, input);
        Require(input.Handled && Get<bool>("pointerHeld"), "The actual left-down handler must start the gesture.");
    }
    private static void Up(int count, double displaced)
    {
        // Read the cursor immediately before delivery, so an unrelated user
        // pointer movement between test cases cannot create a synthetic drag.
        Set("pointerStart", Cursor() + new Vector(displaced, 0));
        MouseButtonEventArgs input = Button(count, MouseButton.Left);
        input.RoutedEvent = Mouse.MouseUpEvent;
        Call("OnPointerUp", pet, input);
        Require(!Get<bool>("pointerHeld"), "The actual left-up handler must end the gesture.");
    }
    private static void Tap(int count) { Down(count); Up(count, 0); }

    private static DateTime Reset(string character)
    {
        Call("CancelKoloPortal");
        Call("CancelKoloEasterEgg");
        Get<PetTapSequence>("tapSequence").Reset();
        Set("currentCharacter", character);
        Set("movement", "rest");
        Set("state", "idle");
        Set("pointerHeld", false);
        Set("dragged", false);
        pet.Left = 600; pet.Top = 300;
        Set("monitorWorkAreas", new List<Rect> { new Rect(0, 0, 1920, 1080) });
        Set("monitorsCachedAt", DateTime.Now);
        DateTime now = DateTime.UtcNow;
        WindowAbilityClock clock = new WindowAbilityClock(now, DateTime.MinValue);
        clock.Use(now);
        saves = 0;
        controller = new WindowAbilityController(desktop, clock, new Random(1), delegate(DateTime next) { saves++; });
        Set("windowAbility", controller);
        Get<SpeechWindow>("speechWindow").Text.Text = "before-input";
        return clock.NextUseUtc;
    }

    private static void CheckOrdinaryAndRepeatedClicks()
    {
        DateTime deadline = Reset("kolo");
        Tap(1);
        Require(controller.Clock.NextUseUtc == deadline && saves == 0, "An ordinary single click must not shorten cooldown.");
        string speech = Get<SpeechWindow>("speechWindow").Text.Text;
        Require(speech != "before-input" && !speech.Contains("冷却") && Get<string>("state") == "happy",
            "The ordinary single-click animation and dialogue must remain available.");
        for (int count = 2; count <= 6; count++)
        {
            Tap(count);
            int reductions = count / 2;
            Require(controller.Clock.NextUseUtc == deadline.AddMinutes(-reductions) && saves == reductions,
                "A rapid " + count + "-click sequence must consume disjoint pairs exactly once each.");
            if (count % 2 == 0)
                Require(Get<SpeechWindow>("speechWindow").Text.Text.Contains("冷却缩短 1 分钟"),
                    "Each completed double click must report its one-minute reduction.");
        }
        Tap(1); Tap(2);
        Require(controller.Clock.NextUseUtc == deadline.AddMinutes(-4) && saves == 4,
            "A new system double-click sequence must work after an earlier rapid sequence.");

        deadline = Reset("kolo");
        Tap(2);
        Require(controller.Clock.NextUseUtc == deadline && saves == 0,
            "An isolated second-click event without a prior tap must not shorten cooldown.");
        Tap(1); Tap(1);
        Require(controller.Clock.NextUseUtc == deadline && saves == 0,
            "Two clicks outside the system double-click timing remain ordinary single clicks.");
    }

    private static void CheckDragsAndCatchRelease()
    {
        DateTime deadline = Reset("kolo");
        Tap(1); Down(2); Up(2, 40);
        Require(controller.Clock.NextUseUtc == deadline && saves == 0,
            "A second release outside the tap threshold must never shorten cooldown.");

        deadline = Reset("kolo");
        Down(1); Up(1, 40); Tap(2);
        Require(controller.Clock.NextUseUtc == deadline && saves == 0,
            "A first-click drag cannot be reused as the first half of a double click.");

        deadline = Reset("kolo");
        Tap(1); Down(2);
        // MouseEventArgs.LeftButton reads the real device and has no setter.
        // Inject the recorded path excursion at its classifier boundary; then
        // use the real release handler at the original location. This avoids
        // synthesizing operating-system mouse events on the user's desktop.
        Get<PetTapSequence>("tapSequence").Move(40);
        Set("dragged", true);
        Up(2, 0);
        Require(controller.Clock.NextUseUtc == deadline && saves == 0,
            "Dragging away and returning to the initial point is still a drag, never a tap.");
        Require(Get<string>("movement") == "portal", "Returning dragged Kolo to the original point must still start the airborne portal.");
        Tap(3); Tap(4);
        Require(controller.Clock.NextUseUtc == deadline.AddMinutes(-1) && saves == 1,
            "A drag must not permanently poison a later complete pair of taps.");

        deadline = Reset("kolo");
        Set("movement", "fall");
        Down(1);
        Require(Get<string>("movement") == "held" && Get<bool>("dragged"), "Catching a falling character must enter the held pose.");
        Up(1, 0);
        Require(Get<string>("movement") == "portal" && controller.Clock.NextUseUtc == deadline,
            "A stationary first catch and release starts Kolo's portal without discounting cooldown.");
        Down(2); Up(2, 0);
        Require(Get<string>("movement") == "portal" && controller.Clock.NextUseUtc == deadline.AddMinutes(-1) && saves == 1,
            "A stationary airborne double click must reduce cooldown while retaining the portal on release.");

        foreach (string character in new[] { "hutao", "ganyu", "varesa", "gpt6a", "deepseek" })
        {
            Reset(character);
            Set("movement", "fall");
            Down(1); Up(1, 0);
            Require(Get<string>("movement") == "fall", character + " must retain ordinary falling after catch and release.");
        }
    }

    private static void CheckCharacterAndUnmatchedInput()
    {
        foreach (string character in new[] { "hutao", "ganyu", "varesa" })
        {
            DateTime deadline = Reset(character);
            for (int i = 1; i <= 6; i++) Tap(i);
            Require(controller.Clock.NextUseUtc == deadline && saves == 0,
                "Double clicks on " + character + " must not discount Kolo's ability.");
        }

        DateTime original = Reset("kolo");
        Tap(1);
        Call("SwitchCharacter", "ganyu");
        Call("SwitchCharacter", "kolo");
        Tap(2);
        Require(controller.Clock.NextUseUtc == original && saves == 0,
            "A switch away and back resets the earlier click even when WPF retains ClickCount 2.");
        Tap(3); Tap(4);
        Require(controller.Clock.NextUseUtc == original.AddMinutes(-1) && saves == 1,
            "A new complete double click remains usable after switching characters.");

        original = Reset("kolo");
        Call("OnPointerUp", pet, Button(2, MouseButton.Left));
        Require(controller.Clock.NextUseUtc == original && saves == 0 && !Get<bool>("pointerHeld"),
            "An unmatched mouse-up must not manufacture a completed tap.");
        Tap(1); Tap(2);
        DateTime afterPair = controller.Clock.NextUseUtc;
        Call("OnPointerUp", pet, Button(2, MouseButton.Left));
        Require(controller.Clock.NextUseUtc == afterPair && saves == 1,
            "A duplicated mouse-up must not replay the cooldown reduction.");

        original = Reset("kolo");
        Call("OnPointerDown", pet, Button(1, MouseButton.Right));
        Call("OnPointerUp", pet, Button(2, MouseButton.Right));
        Require(controller.Clock.NextUseUtc == original && saves == 0 && !Get<bool>("pointerHeld"),
            "Right-button input must stay outside the left double-click gesture.");
        Require(desktop.Calls == 0, "Pointer input must not enumerate, restore, resize, or move any application window.");
    }

    [STAThread]
    private static int Main()
    {
        string characterPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet", "character.txt");
        byte[] savedCharacter = File.Exists(characterPath) ? File.ReadAllBytes(characterPath) : null;
        try
        {
            pet = new PetWindow();
            Get<DispatcherTimer>("timer").Stop();
            Set("nextHighlightsRefresh", DateTime.MaxValue);
            CheckOrdinaryAndRepeatedClicks();
            CheckDragsAndCatchRelease();
            CheckCharacterAndUnmatchedInput();
            Console.WriteLine("Pointer interaction: " + checks + " checks passed (real WPF down/up handlers; read-only native cursor; no user windows changed).");
            return 0;
        }
        catch (Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException)
                Console.Error.WriteLine(current.Message + "\n" + current.StackTrace);
            return 1;
        }
        finally
        {
            if (pet != null) pet.Close();
            if (savedCharacter == null) { if (File.Exists(characterPath)) File.Delete(characterPath); }
            else File.WriteAllBytes(characterPath, savedCharacter);
        }
    }
}
