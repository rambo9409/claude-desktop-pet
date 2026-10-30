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

        internal void Begin(string character, int clickCount)
        {
            secondTap = (character == "kolo" || character == "gpt6a" || character == "deepseek") && previousWasTap && previousCharacter == character &&
                clickCount >= 2 && clickCount % 2 == 0;
            pressedCharacter = character;
            previousWasTap = false;
            moved = false;
            pressed = true;
        }

        internal void Move(double distance)
        {
            if (pressed && (Double.IsNaN(distance) || Double.IsInfinity(distance) || distance > 5)) moved = true;
        }

        internal bool Complete(string character)
        {
            if (!pressed) return false;
            pressed = false;
            previousWasTap = !moved && pressedCharacter == character;
            previousCharacter = character;
            return previousWasTap && secondTap;
        }

        internal void Reset()
        {
            previousWasTap = pressed = secondTap = false;
            previousCharacter = pressedCharacter = null;
        }
    }

    public sealed partial class PetWindow
    {
        private readonly PetTapSequence tapSequence = new PetTapSequence();
    }
}
