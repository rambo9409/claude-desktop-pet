using System;

namespace ClaudePet
{
    // WPF owns the system double-click time/position rules. We additionally
    // require both releases to be taps, because a drag can also increment
    // ClickCount. A rapid sequence consumes pairs (2, 4, 6...), not overlaps.
    internal sealed class PetTapSequence
    {
        private string previousCharacter, pressedCharacter;
        private bool previousWasTap, moved, secondTap, pressed;
        private bool unmatchedFirstTap;
        private DateTime lastPairUtc = DateTime.MinValue;
        internal const double CostumePairGapSeconds = 1.5;
        internal int ConsecutiveDoubleClicks { get; private set; }
        internal bool FourDoubleClicksCompleted { get; private set; }

        internal void Begin(string character, int clickCount)
        { BeginAt(character, clickCount, DateTime.UtcNow); }

        internal void BeginAt(string character, int clickCount, DateTime nowUtc)
        {
            secondTap = (character == "kolo" || character == "gpt6a" || character == "deepseek") && previousWasTap && previousCharacter == character &&
                clickCount >= 2 && clickCount % 2 == 0;
            FourDoubleClicksCompleted = false;
            if (character != "kolo" || (previousCharacter != null && previousCharacter != character) ||
                (unmatchedFirstTap && !secondTap) || PairExpired(nowUtc)) ResetCostumePairs();
            pressedCharacter = character;
            previousWasTap = false;
            moved = false;
            pressed = true;
        }

        internal void Move(double distance)
        {
            if (pressed && (Double.IsNaN(distance) || Double.IsInfinity(distance) || distance > 5)) {
                moved = true; ResetCostumePairs();
            }
        }

        internal bool Complete(string character)
        { return CompleteAt(character, DateTime.UtcNow); }

        internal bool CompleteAt(string character, DateTime nowUtc)
        {
            FourDoubleClicksCompleted = false;
            if (!pressed) return false;
            pressed = false;
            previousWasTap = !moved && pressedCharacter == character;
            previousCharacter = character;
            bool pair = previousWasTap && secondTap;
            if (!previousWasTap || character != "kolo" || PairExpired(nowUtc)) ResetCostumePairs();
            unmatchedFirstTap = previousWasTap && !pair;
            if (pair && character == "kolo") {
                ConsecutiveDoubleClicks++;
                lastPairUtc = nowUtc;
                if (ConsecutiveDoubleClicks == 4) {
                    ResetCostumePairs();
                    FourDoubleClicksCompleted = true;
                }
            }
            return pair;
        }

        private bool PairExpired(DateTime nowUtc)
        { return lastPairUtc != DateTime.MinValue && (nowUtc < lastPairUtc || (nowUtc - lastPairUtc).TotalSeconds > CostumePairGapSeconds); }

        private void ResetCostumePairs()
        {
            ConsecutiveDoubleClicks = 0; lastPairUtc = DateTime.MinValue;
            FourDoubleClicksCompleted = false; unmatchedFirstTap = false;
        }

        internal void Reset()
        {
            previousWasTap = pressed = secondTap = false;
            previousCharacter = pressedCharacter = null;
            ResetCostumePairs();
        }
    }

    public sealed partial class PetWindow
    {
        private readonly PetTapSequence tapSequence = new PetTapSequence();
    }
}
