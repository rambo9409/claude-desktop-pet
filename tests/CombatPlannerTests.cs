using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudePet;

internal static class CombatPlannerTests
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Private).GetValue(owner); }
    private static void Throws(Action action, string message) { bool failed = false; try { action(); } catch { failed = true; } Check(failed, message); }

    private static IEnumerable<TextBlock> Texts(DependencyObject parent)
    {
        TextBlock text = parent as TextBlock; if (text != null) yield return text;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (TextBlock child in Texts(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }

    private static string AllText(DependencyObject parent)
    {
        string value = ""; foreach (TextBlock text in Texts(parent)) value += text.Text + "\n"; return value;
    }

    private static double Channel(byte value) { double x = value / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
    private static double Luma(Color color) { return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B); }
    private static double Contrast(Brush a, Brush b)
    {
        double x = Luma(((SolidColorBrush)a).Color), y = Luma(((SolidColorBrush)b).Color);
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }

    private static void Core()
    {
        CombatPlanInput input = CombatPlanInput.CreateExample();
        CombatPlanResult result = CombatPlanner.Calculate(input);
        Check(result.Steps.Count >= 4, "Rotation steps missing.");
        Check(result.Steps[0].Contains("生存位") && result.Steps[3].Contains("主C"), "Safe setup must start before the on-field carry.");
        Check(result.RotationSeconds >= 20, "Cooldown floor was ignored.");
        Check(result.CurrentDamage > 0 && result.CurrentDps > 0, "Current damage was not calculated.");
        Check(result.TargetDamage >= result.CurrentDamage && result.TargetDps >= result.CurrentDps, "Minimum targets lowered the estimate.");
        Check(result.Characters.Count == 4, "Character target cards missing.");
        CombatCharacterResult support = result.Characters[2];
        Check(support.RequiredEnergyRecharge >= 200, "High-cost support received an implausibly low recharge target.");
        Check(support.Advice.Count > 0, "Artifact guidance missing.");
        Check(Math.Abs(CombatPlanner.DefenseMultiplier(90, 90, 0) - .5) < .0001, "Equal-level defense multiplier is wrong.");
        Check(Math.Abs(CombatPlanner.ResistanceMultiplier(10) - .9) < .0001, "Ordinary resistance multiplier is wrong.");
        Check(Math.Abs(CombatPlanner.ResistanceMultiplier(-20) - 1.1) < .0001, "Negative resistance rule is wrong.");
        Check(Math.Abs(CombatPlanner.ResistanceMultiplier(80) - (1.0 / 4.2)) < .0001, "High resistance rule is wrong.");

        CombatCharacterInput carry = input.Characters[0];
        input.ReactionMultiplier = 1; input.ReactionCoverage = 0;
        double plain = CombatPlanner.ExpectedDamage(input, carry, 0, carry.CoreStat, carry.CritRate, carry.CritDamage, carry.DamageBonus, 1);
        input.ReactionMultiplier = 2; input.ReactionCoverage = 50;
        double reaction = CombatPlanner.ExpectedDamage(input, carry, 0, carry.CoreStat, carry.CritRate, carry.CritDamage, carry.DamageBonus, 1);
        Check(Math.Abs(reaction / plain - 1.5) < .0001, "Reaction coverage multiplier is wrong.");
        input.Characters[0].CritRate = Double.NaN;
        Throws(delegate { CombatPlanner.Calculate(input); }, "NaN panel data was accepted.");
    }

    private static void Storage()
    {
        string path = Path.Combine("artifacts", "tests", "combat-plan-test.json");
        CombatPlanInput input = CombatPlanInput.CreateExample(); input.Characters[0].Name = "测试主C";
        CombatPlannerStorage.Save(path, input);
        CombatPlanInput loaded = CombatPlannerStorage.Load(path);
        Check(loaded.Characters.Count == 4 && loaded.Characters[0].Name == "测试主C", "Local team save did not round-trip.");
        File.WriteAllText(path, "not-json");
        Check(CombatPlannerStorage.Load(path).Characters.Count == 4, "Broken local save did not recover.");
    }

    private static void Visual()
    {
        CombatRotationWindow window = new CombatRotationWindow("kolo", false) { Width = 1050, Height = 780 };
        try {
            foreach (string theme in new[] { "kolo", "hutao", "ganyu", "varesa" }) {
                window.ApplyCharacterTheme(theme);
                FrameworkElement content = (FrameworkElement)window.Content;
                content.Measure(new Size(window.Width, window.Height)); content.Arrange(new Rect(0, 0, window.Width, window.Height)); content.UpdateLayout();
                string text = AllText(content);
                Check(text.Contains("战斗循环助手") && text.Contains("建议循环") && text.Contains("最低循环目标"), "Planner sections missing for " + theme + ".");
                Check(text.Contains("不读取内存") && text.Contains("不自动按键"), "Safety boundary missing.");
                Check(text.Contains("当前期望") && text.Contains("达标后期望"), "Damage summary missing.");
                Check(Contrast((Brush)window.Resources["PlannerText"], (Brush)window.Resources["PlannerSurface"]) >= 4.5, "Theme contrast too low: " + theme);
            }
            StackPanel result = Field<StackPanel>(window, "resultBody");
            Check(result.Children.Count >= 9, "Planner result cards were not rendered.");
            FrameworkElement view = (FrameworkElement)window.Content;
            RenderTargetBitmap image = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
            image.Render(view); PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream output = File.Create(Path.Combine("artifacts", "tests", "combat-planner-varesa.png"))) encoder.Save(output);
        } finally { window.Close(); }
    }

    [STAThread]
    private static void Main()
    {
        Directory.CreateDirectory(Path.Combine("artifacts", "tests"));
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Core(); Storage(); Visual();
        Console.WriteLine("PASS: " + checks.ToString(CultureInfo.InvariantCulture) + " combat formula, rotation, targets, local save, safety text, themes, and layout checks.");
        app.Shutdown();
    }
}
