using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class GaitTests
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static void Set(PetWindow pet, string name, object value) { typeof(PetWindow).GetField(name, Private).SetValue(pet, value); }
    private static object Get(PetWindow pet, string name) { return typeof(PetWindow).GetField(name, Private).GetValue(pet); }
    private static object Call(PetWindow pet, string method, params object[] args) { return typeof(PetWindow).GetMethod(method, Private).Invoke(pet, args); }

    private static void Render(PetWindow pet)
    {
        Set(pet, "pointerHeld", true); // Pause physics only; run the real renderer.
        // Pose assertions concern the settled renderer. The separate effects
        // suite checks all intermediate human-to-butterfly frames.
        for (int step = 0; step < 24; step++)
        {
            Set(pet, "lastTick", DateTime.Now.AddMilliseconds(-50));
            Call(pet, "OnTick", null, EventArgs.Empty);
        }
        Set(pet, "pointerHeld", false);
    }

    private static void Support(PetWindow pet)
    {
        IntPtr handle = new IntPtr(424242);
        Set(pet, "platformCache", new List<PlatformEdge> {
            new PlatformEdge { Handle = handle, Left = 0, Right = 100000, Y = 1000, Bottom = 1500, IsFloor = false }
        });
        Set(pet, "platformCacheTime", DateTime.Now);
        Set(pet, "supportHandle", handle);
        pet.Left = 1000;
        pet.Top = 1000 - pet.Height + pet.Height * 0.038;
    }

    private static void Ready(PetWindow pet, string character)
    {
        Set(pet, "currentCharacter", character);
        Set(pet, "frames", Call(pet, "LoadCharacterFrames", character));
        Set(pet, "pointerHeld", false);
        Set(pet, "movement", "rest");
        Set(pet, "state", "idle");
        Set(pet, "stateEnds", DateTime.MaxValue);
        Call(pet, "CancelButterflyEffects");
        Set(pet, "nextHighlightsRefresh", DateTime.MaxValue);
        Set(pet, "random", new Random(42));
        Support(pet);
    }

    private static void CheckButterflyTransitions(PetWindow pet)
    {
        Ready(pet, "hutao");
        Set(pet, "quietMode", false);
        Image sprite = (Image)Get(pet, "sprite");
        Dictionary<string, BitmapSource[]> frames = (Dictionary<string, BitmapSource[]>)Get(pet, "frames");
        Call(pet, "StartWalking");
        Require(((SpeechWindow)Get(pet, "speechWindow")).Text.Text == "化作金蝶，飞过去啦。", "Hu Tao flight speaks a running line.");
        for (int wing = 0; wing < 4; wing++)
        {
            Set(pet, "runPhase", (wing + 0.2) / 4.0);
            Render(pet);
            Require(ReferenceEquals(sprite.Source, frames["walk"][wing]), "A butterfly wing phase was skipped.");
        }

        // Exercise real transition methods, including walls and falls; these
        // must restore the humanoid even while the selected gait is still run.
        Call(pet, "StartClimbing", new PlatformEdge { Handle = new IntPtr(424242) }, 1, true);
        Render(pet);
        Require(ReferenceEquals(sprite.Source, frames["climb-up"][0]), "Butterfly remained visible while climbing.");
        Ready(pet, "hutao");
        Call(pet, "StartWalking");
        Call(pet, "BeginFall", 40.0);
        Render(pet);
        Require(Array.IndexOf(frames["glide"], sprite.Source) >= 3 && Array.IndexOf(frames["glide"], sprite.Source) <= 5,
            "Hu Tao must glide in human form during a fall.");

        foreach (string interaction in new[] { "pet", "happy", "rose", "typing" })
        {
            Ready(pet, "hutao");
            Call(pet, "StartWalking");
            Call(pet, "Play", interaction, "test", 5000, 150);
            Render(pet);
            Require((string)Get(pet, "movement") == "rest" && ReferenceEquals(sprite.Source, frames[interaction][0]),
                "Butterfly remained visible during " + interaction + ".");
        }

        Ready(pet, "hutao");
        Call(pet, "StartWalking");
        Call(pet, "StartStrolling");
        Render(pet);
        Require(ReferenceEquals(sprite.Source, frames["stroll"][0]), "Switching to slow walking did not restore Hu Tao.");
        Call(pet, "StartWalking");
        Call(pet, "StopWalking");
        Render(pet);
        Require(ReferenceEquals(sprite.Source, frames["idle"][0]), "Stopping did not restore Hu Tao.");
    }

    [STAThread]
    private static void Main(string[] args)
    {
        foreach (double width in new[] { 100.0, 165.0, 450.0 })
        foreach (double variation in new[] { 0.0, 0.25, 0.73, 1.0 })
        {
            double normal = MovementGait.Speed("kolo", "run", variation, width);
            foreach (string character in new[] { "kolo", "hutao", "ganyu", "varesa" })
            {
                double stroll = MovementGait.Speed(character, "stroll", variation, width);
                Require(stroll == MovementGait.Speed("kolo", "stroll", variation, width), "Slow walk must have equal speed for all four.");
                Require(stroll < normal * 0.5, "Slow walk is not substantially slower than run.");
                Require(MovementGait.Speed(character, "run", variation, width) == normal * (character == "varesa" ? 4 : 1),
                    "Sprint multiplier failed at scaled size or variable speed.");
            }
        }
        Require(MovementGait.Normalize(null) == "stroll" && MovementGait.Normalize("bad") == "stroll", "Invalid preference needs safe slow default.");
        Require(MovementGait.Choose("mixed", 0) == "stroll" && MovementGait.Choose("mixed", 0.99) == "run", "Mixed mode must choose both gaits.");
        if (args.Length > 0 && args[0] == "--pure")
        {
            Console.WriteLine("PASS: " + checks + " speed/scale/selection checks (pure).");
            return;
        }

        string path = MovementGait.PreferencePath;
        byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        PetWindow pet = null;
        PetWindow reopened = null;
        try
        {
            if (File.Exists(path)) File.Delete(path);
            Require(MovementGait.LoadPreference() == "stroll", "Fresh install must default to slow walking.");
            pet = new PetWindow();
            ((DispatcherTimer)Get(pet, "timer")).Stop();
            Require((string)Get(pet, "preferredGait") == "stroll", "Window ignored default slow preference.");
            Image sprite = (Image)Get(pet, "sprite");
            double regularSpeed = 0;
            double[] regularAcceleration = new double[8];
            foreach (string character in new[] { "kolo", "hutao", "ganyu", "varesa" })
            {
                Ready(pet, character);
                Dictionary<string, BitmapSource[]> frames = (Dictionary<string, BitmapSource[]>)Get(pet, "frames");
                Require(frames["stroll"].Length == 4 && frames["walk"].Length == 4, character + " missing a gait cycle.");
                Call(pet, "StartStrolling");
                Require((string)Get(pet, "movement") == "walk" && (string)Get(pet, "activeGait") == "stroll", character + " manual stroll did not start.");
                Require((double)Get(pet, "moveSpeed") >= 30 && (double)Get(pet, "moveSpeed") <= 45, character + " stroll uses run speed.");
                Set(pet, "runPhase", 0.62);
                Render(pet);
                Require(ReferenceEquals(sprite.Source, frames["stroll"][2]), character + " does not render its slow gait.");

                Ready(pet, character);
                Call(pet, "StartWalking");
                double speed = (double)Get(pet, "moveSpeed");
                if (character == "kolo") regularSpeed = speed;
                Require(speed == regularSpeed * (character == "varesa" ? 4 : 1), character + " actual start speed multiplier is wrong.");
                Set(pet, "runPhase", 0.62);
                Render(pet);
                Require(ReferenceEquals(sprite.Source, frames["walk"][2]), character + " run no longer uses its original/special cycle.");

                // Quiet mode reduces activity, not per-gait velocity or Varesa's multiplier.
                Ready(pet, character);
                Set(pet, "quietMode", true);
                Call(pet, "StartWalking");
                Require((double)Get(pet, "moveSpeed") == speed, character + " quiet mode changed running speed.");
                Set(pet, "quietMode", false);

                Ready(pet, character);
                Call(pet, "StartWalking");
                for (int step = 0; step < regularAcceleration.Length; step++)
                {
                    Set(pet, "nextSpeedChange", DateTime.MinValue);
                    Set(pet, "platformCacheTime", DateTime.Now);
                    Call(pet, "UpdateMovement", DateTime.Now, 0.016);
                    double accelerated = (double)Get(pet, "moveSpeed");
                    if (character == "kolo") regularAcceleration[step] = accelerated;
                    Require(Math.Abs(accelerated - regularAcceleration[step] * (character == "varesa" ? 4 : 1)) < 0.000001,
                        character + " lost its speed multiplier during acceleration.");
                }

                int direction = (int)Get(pet, "direction");
                double left = pet.Left;
                Call(pet, "ApplyMovementMode", "stroll");
                Require((string)Get(pet, "activeGait") == "stroll" && (int)Get(pet, "direction") == direction && pet.Left == left,
                    "Mode change must switch gait without teleporting or reversing direction.");
                Render(pet);
                Require(ReferenceEquals(sprite.Source, frames["stroll"][0]), "Mode change left a sprint/mask frame visible.");

                foreach (string preference in new[] { "stroll", "run", "mixed" })
                {
                    Set(pet, "preferredGait", preference);
                    Set(pet, "movement", "rest");
                    Set(pet, "autoWalk", true);
                    Set(pet, "nextWalk", DateTime.MinValue);
                    Support(pet);
                    Call(pet, "UpdateMovement", DateTime.Now, 0.016);
                    Require((string)Get(pet, "movement") == "walk", "Automatic movement did not start.");
                    Require(preference == "mixed" || (string)Get(pet, "activeGait") == preference, "Automatic movement ignored preference.");
                }
                Call(pet, "StopWalking");
                Set(pet, "autoWalk", false);
                Set(pet, "nextWalk", DateTime.MinValue);
                Support(pet);
                Call(pet, "UpdateMovement", DateTime.Now, 0.016);
                Require((string)Get(pet, "movement") == "rest", "Disabled auto movement restarted.");
                Call(pet, "StartStrolling");
                Require((string)Get(pet, "movement") == "walk", "Manual walk must work with automatic movement disabled.");

                foreach (string motion in new[] { "rest", "held", "fall", "climb" })
                {
                    Set(pet, "activeGait", "run");
                    Set(pet, "movement", motion);
                    Set(pet, "climbVerticalDirection", -1);
                    Set(pet, "climbPhase", 0.1);
                    Render(pet);
                    bool aerial = character != "kolo" && (motion == "held" || motion == "fall");
                    Require(aerial ? Array.IndexOf(frames["glide"], sprite.Source) >= 3 && Array.IndexOf(frames["glide"], sprite.Source) <= 5 :
                        ReferenceEquals(sprite.Source, frames[motion == "climb" ? "climb-up" : "idle"][0]),
                        character + " is using sprint/mask while " + motion + ".");
                }
                Call(pet, "Play", "pet", "test", 1500, 150);
                Render(pet);
                Require(ReferenceEquals(sprite.Source, frames["pet"][0]), character + " is masked during interaction.");
            }

            CheckButterflyTransitions(pet);

            // Setting survives both character changes and reconstructing the window.
            Call(pet, "ApplyMovementMode", "mixed");
            Require(MovementGait.LoadPreference() == "mixed", "Mode was not persisted.");
            reopened = new PetWindow();
            ((DispatcherTimer)Get(reopened, "timer")).Stop();
            Require((string)Get(reopened, "preferredGait") == "mixed", "Restart lost the saved mode.");
            File.WriteAllText(path, "corrupted preference");
            Require(MovementGait.LoadPreference() == "stroll", "Corrupt preference did not recover.");
            Console.WriteLine("PASS: " + checks + " checks: four-character slow/run renderers, exact sprint speed, quiet mode, auto/manual choice, safe transitions, persistence, Hu Tao butterfly flight transitions, humanoid non-running states.");
        }
        finally
        {
            if (reopened != null) reopened.Close();
            if (pet != null) pet.Close();
            if (original == null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else File.WriteAllBytes(path, original);
        }
    }
}

