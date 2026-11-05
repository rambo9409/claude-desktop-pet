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

    private static readonly Point InputPoint = new Point(650, 350);

    private static void WithSyntheticCursor(Action action)
    {
        Matrix previous = Get<Matrix>("fromDevice");
        List<Rect> monitors = Get<List<Rect>>("monitorWorkAreas");
        List<PlatformEdge> platforms = Get<List<PlatformEdge>>("platformCache");
        DateTime monitorTime = Get<DateTime>("monitorsCachedAt"), platformTime = Get<DateTime>("platformCacheTime");
        try {
            // Both production cursor reads map to one test coordinate even if
            // the user's physical pointer moves between down and up. This does
            // not move the OS cursor or synthesize any operating-system input.
            Set("fromDevice", new Matrix(0, 0, 0, 0, InputPoint.X, InputPoint.Y));
            // Keep monitor floors geometric while the cursor projection is
            // constant. Forced window refreshes reject zero-width projected
            // native rectangles through the normal minimum-window-size filter.
            Set("monitorWorkAreas", new List<Rect> { new Rect(0, 0, 1920, 1080) });
            Set("monitorsCachedAt", DateTime.MaxValue);
            Set("platformCache", new List<PlatformEdge> { new PlatformEdge { Left = 0, Right = 1920, Y = 1080, Bottom = 1080, IsFloor = true } });
            Set("platformCacheTime", DateTime.MaxValue);
            action();
        } finally {
            Set("fromDevice", previous); Set("monitorWorkAreas", monitors); Set("monitorsCachedAt", monitorTime);
            Set("platformCache", platforms); Set("platformCacheTime", platformTime);
        }
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
        WithSyntheticCursor(delegate { Call("OnPointerDown", pet, input); });
        Require(input.Handled && Get<bool>("pointerHeld"), "The actual left-down handler must start the gesture.");
    }
    private static void Up(int count, double displaced)
    {
        Set("pointerStart", InputPoint + new Vector(displaced, 0));
        MouseButtonEventArgs input = Button(count, MouseButton.Left);
        input.RoutedEvent = Mouse.MouseUpEvent;
        WithSyntheticCursor(delegate { Call("OnPointerUp", pet, input); });
        Require(!Get<bool>("pointerHeld"), "The actual left-up handler must end the gesture.");
    }
    private static void Tap(int count) { Down(count); Up(count, 0); }

    private static DateTime Reset(string character)
    {
        Call("CancelKoloPortal");
        Call("CancelKoloEasterEgg");
        Call("ResetKoloEasterEggUsage");
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

    private static void ClassifierTap(PetTapSequence taps, string character, int count, DateTime at, double distance)
    {
        taps.BeginAt(character, count, at);
        taps.Move(distance);
        taps.CompleteAt(character, at.AddMilliseconds(20));
    }

    private static void CheckCostumeClassifier()
    {
        DateTime at = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        PetTapSequence taps = new PetTapSequence();
        for (int click = 1; click <= 8; click++) {
            ClassifierTap(taps, "kolo", click, at.AddMilliseconds(click * 80), 0);
            Require(taps.FourDoubleClicksCompleted == (click == 8), "Costume needs exactly four disjoint pairs, not four clicks.");
        }
        taps.CompleteAt("kolo", at.AddSeconds(1));
        Require(!taps.FourDoubleClicksCompleted, "An unmatched release repeated the costume pulse.");
        taps.Reset();
        for (int pair = 0; pair < 4; pair++) {
            ClassifierTap(taps, "kolo", 1, at.AddMilliseconds(pair * 700), 0);
            ClassifierTap(taps, "kolo", 2, at.AddMilliseconds(pair * 700 + 100), 0);
            Require(taps.FourDoubleClicksCompleted == (pair == 3), "Separated system double-click sequences do not accumulate correctly.");
        }
        foreach (string interruption in new[] { "timeout", "drag", "unmatched", "character", "reset", "rollback" }) {
            taps.Reset();
            for (int pair = 0; pair < 3; pair++) {
                ClassifierTap(taps, "kolo", 1, at.AddMilliseconds(pair * 300), 0);
                ClassifierTap(taps, "kolo", 2, at.AddMilliseconds(pair * 300 + 80), 0);
            }
            DateTime next = at.AddSeconds(1);
            if (interruption == "timeout") next = at.AddSeconds(3);
            if (interruption == "rollback") next = at.AddMinutes(-1);
            if (interruption == "drag") ClassifierTap(taps, "kolo", 1, next.AddMilliseconds(-100), 12);
            if (interruption == "unmatched") ClassifierTap(taps, "kolo", 1, next.AddMilliseconds(-100), 0);
            if (interruption == "character") ClassifierTap(taps, "deepseek", 1, next.AddMilliseconds(-100), 0);
            if (interruption == "reset") taps.Reset();
            ClassifierTap(taps, "kolo", 1, next, 0); ClassifierTap(taps, "kolo", 2, next.AddMilliseconds(80), 0);
            Require(!taps.FourDoubleClicksCompleted && taps.ConsecutiveDoubleClicks == 1,
                "Interrupted chain leaked into the next pair: " + interruption);
        }
        foreach (string character in new[] { "deepseek", "gpt6a", "hutao", "ganyu", "varesa" }) {
            taps.Reset();
            for (int click = 1; click <= 8; click++) ClassifierTap(taps, character, click, at.AddMilliseconds(click * 80), 0);
            Require(!taps.FourDoubleClicksCompleted && taps.ConsecutiveDoubleClicks == 0, "Costume gesture leaked to " + character);
        }
    }

    private static void CheckCostumePointerRouting()
    {
        foreach (bool independentPairs in new[] { false, true }) {
            DateTime deadline = Reset("kolo");
            Set("koloEasterEggEnabled", false);
            for (int click = 1; click <= 8; click++) {
                Tap(independentPairs ? (click - 1) % 2 + 1 : click);
                Require(Get<KoloEasterEggSession>("koloEasterEgg").ManualRequested == (click == 8),
                    "Actual pointer handlers did not require eight valid taps.");
                if (click % 2 == 0 && click < 8)
                    Require(Get<SpeechWindow>("speechWindow").Text.Text.Contains("换装准备 " + (click / 2) + "/4"),
                        "A completed costume pair gives no visible progress feedback.");
            }
            Require(controller.Clock.NextUseUtc == deadline.AddMinutes(-4) && saves == 4,
                "Four costume pairs must still deduct all four minutes of ability cooldown.");
            Require(!Get<KoloEasterEggSession>("koloEasterEgg").Active,
                "An unshown pet must not start a delayed manual costume.");
            for (int click = 9; click <= 12; click++) {
                int extraClick = independentPairs ? (click - 1) % 2 + 1 : click;
                WithSyntheticCursor(delegate {
                    MouseButtonEventArgs down = Button(extraClick, MouseButton.Left);
                    Call("OnPointerDown", pet, down);
                    Require(down.Handled && !Get<bool>("pointerHeld") && Get<bool>("koloCostumeTapHeld"),
                        "An extra press started a normal hold instead of protecting the accepted request.");
                    MouseButtonEventArgs up = Button(extraClick, MouseButton.Left); up.RoutedEvent = Mouse.MouseUpEvent;
                    Call("OnPointerUp", pet, up);
                    Require(up.Handled && !Get<bool>("koloCostumeTapHeld") && !Get<bool>("pointerHeld"),
                        "The protected tap did not release its input state.");
                });
                Require(Get<KoloEasterEggSession>("koloEasterEgg").ManualRequested &&
                    !Get<KoloEasterEggSession>("koloEasterEgg").Active,
                    "Tapping past eight lost a manual request while waiting for safe support.");
                Require(controller.Clock.NextUseUtc == deadline.AddMinutes(-4) && saves == 4,
                    "Protected extra clicks repeated the cooldown deduction.");
            }
            Call("SwitchCharacter", "deepseek"); Call("SwitchCharacter", "kolo");
            Require(!Get<KoloEasterEggSession>("koloEasterEgg").ManualRequested,
                "Switching away and back retained a manual request.");
        }
        Reset("kolo");
        for (int click = 1; click <= 6; click++) Tap(click);
        Down(7); Up(7, 40); Tap(8);
        Require(!Get<KoloEasterEggSession>("koloEasterEgg").ManualRequested,
            "A drag cannot complete the fourth pair.");
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
            CheckCostumeClassifier();
            CheckCostumePointerRouting();
            Console.WriteLine("Pointer interaction: " + checks + " checks passed (real WPF down/up handlers; isolated logical cursor projection; no OS input or user windows changed).");
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
