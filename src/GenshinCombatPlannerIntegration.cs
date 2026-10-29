using System.Windows;

namespace ClaudePet
{
    public sealed partial class PetWindow : Window
    {
        private CombatRotationWindow combatPlannerWindow;

        private void OpenCombatPlanner()
        {
            if (combatPlannerWindow == null)
            {
                combatPlannerWindow = new CombatRotationWindow(currentCharacter);
                combatPlannerWindow.Owner = this;
                combatPlannerWindow.Closed += delegate { combatPlannerWindow = null; };
                combatPlannerWindow.Show();
            }
            else
            {
                combatPlannerWindow.ApplyCharacterTheme(currentCharacter);
                combatPlannerWindow.Activate();
            }
        }
    }
}
