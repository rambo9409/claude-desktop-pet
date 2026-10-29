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
        CombatPlanInput loaded = CombatPlannerStorage.Load(path).Input;
        Check(loaded.Characters.Count == 4 && loaded.Characters[0].Name == "测试主C", "Local team save did not round-trip.");
        File.WriteAllText(path, "not-json");
        CombatPlanLoad recovered = CombatPlannerStorage.Load(path);
        Check(recovered.Input.Characters.Count == 4 && !String.IsNullOrEmpty(recovered.Notice), "Broken local save did not report recovery.");
        Check(Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + ".broken-*").Length > 0, "Broken save was not preserved.");
        Check(File.ReadAllText(path) == "not-json", "Loading a broken save destroyed the original.");
        CombatPlannerStorage.Save(path, input);
        Check(CombatPlannerStorage.Load(path).Input.Characters[0].Name == "测试主C", "Safe save after recovery failed.");
        string before = File.ReadAllText(path);
        using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
            Throws(delegate { PetStorage.WriteAllTextAtomic(path, "replacement"); }, "Locked original should not be overwritten.");
            Throws(delegate { CombatPlannerStorage.Save(path, input); }, "A failed backup must block replacement.");
        }
        Check(File.ReadAllText(path) == before, "Failed atomic replacement lost the original save.");
        Check(Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + ".*.tmp").Length == 0, "Failed writes leave temporary files.");
        input.Version = 1;
        CombatPlannerStorage.Save(path, input);
        Check(input.Version == 1 && CombatPlannerStorage.Load(path).Input.Version == CombatPlanInput.CurrentVersion, "Save mutated caller data or failed migration.");
        input.Characters[2] = null;
        CombatPlannerStorage.Save(path, input);
        Check(CombatPlannerStorage.Load(path).Input.Characters[2] != null, "Null slot fails graceful loading.");
    }

    private static void ModelBoundaries()
    {
        CombatPlanInput input = CombatPlanInput.CreateExample();
        input.Characters[0].Name = "  原名  ";
        CombatPlanner.Calculate(input);
        Check(input.Characters[0].Name == "  原名  ", "Calculate mutated input names.");
        input.Characters[1].CritRate = -1; input.Characters[2].EnergyRecharge = Double.PositiveInfinity;
        List<CombatFieldError> errors = CombatPlanner.Validate(input);
        Check(errors.Exists(delegate(CombatFieldError e) { return e.Slot == 2 && e.Field == "CritRate"; }) &&
            errors.Exists(delegate(CombatFieldError e) { return e.Slot == 3 && e.Field == "EnergyRecharge"; }), "Validation must report all mapped fields.");
        Check(CombatPlanner.Validate(null).Count > 0, "Null team lacks friendly error.");
        input = CombatPlanInput.CreateExample();
        CombatCharacterInput solo = input.Characters[0];
        input.Characters = new List<CombatCharacterInput> { solo };
        solo.FieldSeconds = 0; solo.ComboMultiplier = 0; solo.SkillCooldown = 6; solo.BurstCooldown = 20;
        CombatPlanResult result = CombatPlanner.Calculate(input);
        Check(result.Characters[0].SkillCasts == 4, "Twenty-second rotation must fit E at 0/6/12/18s.");
        Check(result.Steps.Exists(delegate(string s) { return s.Contains("18秒"); }), "Recast timing is absent from rotation.");
        solo.TeamBuffPercent = 40; solo.EffectDuration = 1.3;
        double shortBuff = CombatPlanner.TeamBuffFor(input.Characters, solo, 20);
        Check(Math.Abs(shortBuff - 20) < .001, "Timed buff coverage or self coverage is wrong.");
        solo.EffectDuration = 0;
        Check(CombatPlanner.TeamBuffFor(input.Characters, solo, 20) == 40, "Permanent team buff should cover its owner.");
        solo.FieldSeconds = 25;
        Check(CombatPlanner.Calculate(input).Characters[0].SkillCasts == 1, "Extra skills overlap occupied field time.");
        solo.BurstCost = 0;
        double noBurst = CombatPlanner.Calculate(input).CurrentDamage;
        solo.BurstMultiplier = 0;
        Check(Math.Abs(noBurst - CombatPlanner.Calculate(input).CurrentDamage) < .001, "An uncast burst contributes phantom damage.");
    }

    private static void Ocr()
    {
        PanelStatReading values = CombatPanelTextParser.Parse(new[] { "生命值上限 12,345", "攻击力 1,000 + 500", "防御力 ７８９",
            "暴 击 率 70.5 %", "暴击伤害", "140.2%", "元素充能效率 160.8%", "火元素伤害加成 46.6%", "元素精通 123" });
        Check(values.Health == 12345 && values.Attack == 1500 && values.Defense == 789, "Panel integer/base+bonus/full-width parse failed.");
        Check(values.CritRate == 70.5 && values.CritDamage == 140.2 && values.EnergyRecharge == 160.8, "Percentage/split-row parsing failed.");
        Check(values.CoreFor("health") == 12345 && values.BonusFor("火") == 46.6, "Scaling/element import selection failed.");
        Check(values.BonusFor("冰") == null, "OCR substituted the wrong element's bonus.");
        PanelStatReading english = CombatPanelTextParser.Parse(new[] { "ATK 2000", "CRIT Rate 65.0%", "CRIT DMG 130%", "Energy Recharge 140%", "Hydro DMG Bonus 46.6%" });
        Check(english.Attack == 2000 && english.CritRate == 65 && english.BonusFor("水") == 46.6, "English panel parse failed.");
        Check(CombatPanelTextParser.Parse(null).Count == 0 && CombatPanelTextParser.Parse(new[] { "角色等级 90", "UID 123456789" }).Count == 0, "Unrelated numbers were imported.");
        // A real local OCR call against a reproducible screenshot fixture.
        // This tests the Windows API + row reconstruction, not game accuracy.
        WindowsOcrPanelExtractor extractor = new WindowsOcrPanelExtractor();
        string unavailable = extractor.Unavailable();
        if (unavailable != null) { Console.WriteLine("SKIP native OCR: " + unavailable); return; }
        DrawingVisual visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen()) {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1100, 600));
            string[] labels = { "生命值上限", "攻击力", "防御力", "暴击率", "暴击伤害", "元素充能效率", "火元素伤害加成" };
            string[] numbers = { "12,345", "2,000", "789", "70.5%", "140.2%", "160.8%", "46.6%" };
            for (int i = 0; i < labels.Length; i++) {
                dc.DrawText(new FormattedText(labels[i], CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei"), 34, Brushes.Black, 1), new Point(50, 30 + i * 75));
                dc.DrawText(new FormattedText(numbers[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 34, Brushes.Black, 1), new Point(760, 30 + i * 75));
            }
        }
        RenderTargetBitmap panel = new RenderTargetBitmap(1100, 600, 96, 96, PixelFormats.Pbgra32); panel.Render(visual); panel.Freeze();
        string fixture = Path.Combine("artifacts", "tests", "combat-ocr-fixture.png");
        PngBitmapEncoder png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(panel));
        using (FileStream file = File.Create(fixture)) png.Save(file);
        PanelStatReading actual = extractor.ExtractAsync(CombatPanelImport.LoadFile(fixture)).GetAwaiter().GetResult();
        File.WriteAllLines(Path.Combine("artifacts", "tests", "combat-ocr-lines.txt"), actual.Lines);
        Check(actual.Attack == 2000 && actual.Health == 12345 && actual.CritRate == 70.5 && actual.EnergyRecharge == 160.8,
            "Real Windows OCR failed the clear panel fixture: " + String.Join(" / ", actual.Lines));
        Console.WriteLine("Native OCR: " + actual.Engine + "; " + actual.Count + " fields from a local screenshot fixture.");
        CombatTeamCatalog catalog = CombatTeamPresets.Current;
        Check(catalog.Teams.Count >= 4 && !String.IsNullOrEmpty(catalog.Notice), "Verified team catalog is missing.");
        foreach (CombatTeamPreset team in catalog.Teams) {
            Check(team.Members.Count == 4 && team.Sources.Count > 0 && team.Rotation.Count > 0, "Incomplete team reference: " + team.Id);
            foreach (CombatTeamSource source in team.Sources) Check(CombatTeamPresets.IsSafeLink(source.Url), "Unsafe source link.");
        }
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
                Check(text.Contains("战斗循环助手") && text.Contains("建议循环") && text.Contains("通用参考线"), "Planner sections missing for " + theme + ".");
                Check(text.Contains("不读取内存") && text.Contains("不自动按键"), "Safety boundary missing.");
                Check(text.Contains("当前期望") && text.Contains("达标后期望") && text.Contains("达标后每秒"), "Damage summary missing.");
                Check(Contrast((Brush)window.Resources["PlannerText"], (Brush)window.Resources["PlannerSurface"]) >= 4.5, "Theme contrast too low: " + theme);
                Check(Contrast(Field<Expander>(window, "enemySection").Foreground, (Brush)window.Resources["PlannerSurface"]) >= 4.5,
                    "Enemy expander title is unreadable: " + theme);
                List<CombatCharacterEditor> characterEditors = Field<List<CombatCharacterEditor>>(window, "editors");
                Check(Field<TextBox>(characterEditors[0], "name").FontWeight == FontWeights.Normal, "Selected tab makes input text bold: " + theme);
                ComboBox role = Field<ComboBox>(characterEditors[0], "role");
                Check(((SolidColorBrush)role.Background).Color == ((SolidColorBrush)window.Resources["PlannerCard"]).Color,
                    "Dropdown does not follow the theme: " + theme);
                ComboBoxItem choice = (ComboBoxItem)role.Items[0]; choice.ApplyTemplate();
                Check(((SolidColorBrush)choice.Foreground).Color == ((SolidColorBrush)window.Resources["PlannerText"]).Color,
                    "Dropdown item uses a system foreground: " + theme);
                SaveWindow(window, "combat-planner-" + theme + ".png");
            }
            StackPanel result = Field<StackPanel>(window, "resultBody");
            Check(result.Children.Count >= 9, "Planner result cards were not rendered.");
            List<CombatCharacterEditor> editors = Field<List<CombatCharacterEditor>>(window, "editors");
            Field<TextBox>(editors[0], "critRate").Text = "not a number";
            Field<TextBox>(editors[2], "energyRecharge").Text = "12";
            Field<TextBox>(window, "enemyLevel").Text = "2.5";
            CallWindow(window, "Generate", new[] { typeof(bool) }, new object[] { false });
            Check(Field<TextBox>(editors[0], "critRate").BorderThickness.Left == 2 &&
                Field<TextBox>(editors[2], "energyRecharge").BorderThickness.Left == 2 &&
                Field<TextBox>(window, "enemyLevel").BorderThickness.Left == 2, "Validation does not mark every failing field across tabs.");
            Check(Field<TextBox>(editors[0], "critRate").ToolTip != null && Field<TextBox>(editors[2], "energyRecharge").ToolTip != null,
                "Field error explanations missing.");
            Check(Field<Expander>(window, "enemySection").IsExpanded, "Global error stays hidden inside the collapsed enemy section.");
            SaveWindow(window, "combat-planner-errors.png");
            Check(AllText(result).Contains("3 项输入"), "Validation does not report all three errors together.");

            CallWindow(window, "Load", new[] { typeof(CombatPlanInput) }, new object[] { CombatPlanInput.CreateExample() });
            CombatCharacterInput before = editors[0].Read();
            editors[0].ApplyPanelValues(new Dictionary<string, double> { { "CoreStat", 2468 }, { "CritRate", 71.5 } });
            CombatCharacterInput after = editors[0].Read();
            Check(after.CoreStat == 2468 && after.CritRate == 71.5, "OCR selected values were not applied.");
            Check(after.CritDamage == before.CritDamage && after.EnergyRecharge == before.EnergyRecharge && after.DamageBonus == before.DamageBonus,
                "OCR changed fields not present in its accepted reading.");
            Check(after.Name == before.Name && after.Element == before.Element && after.Scaling == before.Scaling &&
                after.SkillMultiplier == before.SkillMultiplier && after.BurstMultiplier == before.BurstMultiplier &&
                after.SkillCooldown == before.SkillCooldown && after.BurstCost == before.BurstCost,
                "OCR changed identity or skill data.");

            ComboBox presets = Field<ComboBox>(window, "presets");
            CombatTeamPreset unsupported = null;
            foreach (CombatTeamPreset team in CombatTeamPresets.Current.Teams) if (!team.SupportsEstimate) { unsupported = team; break; }
            Check(unsupported != null, "No unsupported preset fixture.");
            foreach (ComboBoxItem item in presets.Items) if ((string)item.Tag == unsupported.Id) { presets.SelectedItem = item; break; }
            CallWindow(window, "ApplyPreset", Type.EmptyTypes, new object[0]);
            CallWindow(window, "Generate", new[] { typeof(bool) }, new object[] { false });
            SaveWindow(window, "combat-planner-reference.png");
            Check(AllText(result).Contains("此队仅提供手法参考") && !AllText(result).Contains("当前每秒") && !AllText(result).Contains("达标后期望"),
                "Unsupported character mechanics still show damage estimates.");
            Check(AllText(Field<StackPanel>(window, "recommendationBody")).Contains("推荐输出手法"), "Preset rotation card missing.");
            Check(Field<string>(window, "presetId") == unsupported.Id, "Applied preset identity not retained.");
            Field<TextBox>(editors[0], "name").Text = "自定义角色";
            CallWindow(window, "Generate", new[] { typeof(bool) }, new object[] { false });
            Check(Field<string>(window, "presetId") == "", "Editing preset members keeps stale team attribution.");

            CallWindow(window, "Load", new[] { typeof(CombatPlanInput) }, new object[] { CombatPlanInput.CreateExample() });
            CallWindow(window, "Generate", new[] { typeof(bool) }, new object[] { false });
            Field<Expander>(window, "enemySection").IsExpanded = false;
            window.Width = 860; window.Height = 620; SaveWindow(window, "combat-planner-small.png");
            Check(window.Owner == null, "Tool unexpectedly follows pet owner/topmost state.");
        } finally { window.Close(); }
    }

    private static object CallWindow(CombatRotationWindow window, string name, Type[] types, object[] args)
    { return typeof(CombatRotationWindow).GetMethod(name, Private, null, types, null).Invoke(window, args); }

    private static void SaveWindow(CombatRotationWindow window, string name)
    {
        FrameworkElement view = (FrameworkElement)window.Content;
        view.Measure(new Size(window.Width, window.Height)); view.Arrange(new Rect(0, 0, window.Width, window.Height)); view.UpdateLayout();
        RenderTargetBitmap image = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
        image.Render(view); PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (FileStream output = File.Create(Path.Combine("artifacts", "tests", name))) encoder.Save(output);
    }

    [STAThread]
    private static void Main()
    {
        Directory.CreateDirectory(Path.Combine("artifacts", "tests"));
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try {
            Console.WriteLine("Combat: core"); Core();
            Console.WriteLine("Combat: model boundaries"); ModelBoundaries();
            Console.WriteLine("Combat: persistence"); Storage();
            Console.WriteLine("Combat: OCR"); Ocr();
            Console.WriteLine("Combat: visual"); Visual();
        } catch (Exception error) {
            Console.WriteLine("FAIL " + error.GetType().FullName + ": " + error.Message);
            if (error.InnerException != null) Console.WriteLine("INNER " + error.InnerException.GetType().FullName + ": " + error.InnerException.Message);
            Environment.ExitCode = 1; app.Shutdown(); return;
        }
        Console.WriteLine("PASS: " + checks.ToString(CultureInfo.InvariantCulture) + " combat formula, rotation, targets, local save, safety text, themes, and layout checks.");
        app.Shutdown();
    }
}
