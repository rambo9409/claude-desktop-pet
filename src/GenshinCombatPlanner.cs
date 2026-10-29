using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ClaudePet
{

    internal sealed class CombatCharacterEditor
    {
        private readonly TextBox name, element, core, critRate, critDamage, damageBonus, energyRecharge;
        private readonly TextBox skillMultiplier, burstMultiplier, comboMultiplier, burstCost;
        private readonly TextBox skillCooldown, burstCooldown, fieldSeconds, teamBuff, effectDuration;
        private readonly ComboBox role, scaling;
        internal readonly TabItem Tab;
        private readonly Dictionary<string, Control> fields = new Dictionary<string, Control>();

        internal CombatCharacterEditor(int slot, CombatRotationWindow owner)
        {
            Tab = new TabItem { Header = "角色 " + slot.ToString(CultureInfo.InvariantCulture) };
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            StackPanel body = new StackPanel { Margin = new Thickness(14, 12, 14, 16) }; scroll.Content = body; Tab.Content = scroll;
            body.Children.Add(owner.SectionLabel("角色与职责"));
            UniformGrid identity = new UniformGrid { Columns = 2 };
            name = owner.Input(); identity.Children.Add(owner.Field("角色名", name));
            role = owner.Choice(new[] { "主C", "后台副C", "增伤辅助", "生存位" }, new[] { "main", "offfield", "support", "sustain" }); identity.Children.Add(owner.Field("队内职责", role));
            element = owner.Input(); identity.Children.Add(owner.Field("元素", element));
            scaling = owner.Choice(new[] { "攻击倍率", "生命倍率", "防御倍率" }, new[] { "attack", "health", "defense" }); identity.Children.Add(owner.Field("伤害基值", scaling));
            body.Children.Add(identity);

            body.Children.Add(owner.SectionLabel("当前面板"));
            UniformGrid stats = new UniformGrid { Columns = 3 };
            core = owner.Input(); stats.Children.Add(owner.Field("主要属性", core));
            critRate = owner.Input(); stats.Children.Add(owner.Field("暴击率 %", critRate));
            critDamage = owner.Input(); stats.Children.Add(owner.Field("暴伤 %", critDamage));
            damageBonus = owner.Input(); stats.Children.Add(owner.Field("伤害加成 %", damageBonus));
            energyRecharge = owner.Input(); stats.Children.Add(owner.Field("充能 %", energyRecharge));
            teamBuff = owner.Input(); stats.Children.Add(owner.Field("全队增伤 %", teamBuff));
            body.Children.Add(stats);

            body.Children.Add(owner.SectionLabel("技能与时间"));
            UniformGrid talents = new UniformGrid { Columns = 3 };
            skillMultiplier = owner.Input(); talents.Children.Add(owner.Field("E总倍率 %", skillMultiplier));
            burstMultiplier = owner.Input(); talents.Children.Add(owner.Field("Q总倍率 %", burstMultiplier));
            comboMultiplier = owner.Input(); talents.Children.Add(owner.Field("驻场连段 %", comboMultiplier));
            burstCost = owner.Input(); talents.Children.Add(owner.Field("大招能量", burstCost));
            skillCooldown = owner.Input(); talents.Children.Add(owner.Field("E冷却 秒", skillCooldown));
            burstCooldown = owner.Input(); talents.Children.Add(owner.Field("Q冷却 秒", burstCooldown));
            fieldSeconds = owner.Input(); talents.Children.Add(owner.Field("驻场 秒", fieldSeconds));
            effectDuration = owner.Input(); talents.Children.Add(owner.Field("效果持续 秒", effectDuration));
            body.Children.Add(talents);
            TextBlock hint = owner.Text("倍率请填一次完整动作的总倍率。多段技能要先相加。", 11, false, true);
            hint.Margin = new Thickness(6, 8, 6, 0); body.Children.Add(hint);
            string[] keys = { "Name", "Element", "Role", "Scaling", "CoreStat", "CritRate", "CritDamage", "DamageBonus", "EnergyRecharge", "TeamBuffPercent", "SkillMultiplier", "BurstMultiplier", "ComboMultiplier", "BurstCost", "SkillCooldown", "BurstCooldown", "FieldSeconds", "EffectDuration" };
            Control[] controls = { name, element, role, scaling, core, critRate, critDamage, damageBonus, energyRecharge, teamBuff, skillMultiplier, burstMultiplier, comboMultiplier, burstCost, skillCooldown, burstCooldown, fieldSeconds, effectDuration };
            for (int i = 0; i < keys.Length; i++) fields[keys[i]] = controls[i];
        }

        internal void Load(CombatCharacterInput value)
        {
            if (value == null) value = CombatPlanInput.EmptySlot(0);
            ClearErrors();
            name.Text = value.Name ?? ""; element.Text = value.Element ?? "";
            Select(role, value.Role); Select(scaling, value.Scaling);
            core.Text = Number(value.CoreStat); critRate.Text = Number(value.CritRate); critDamage.Text = Number(value.CritDamage);
            damageBonus.Text = Number(value.DamageBonus); energyRecharge.Text = Number(value.EnergyRecharge);
            skillMultiplier.Text = Number(value.SkillMultiplier); burstMultiplier.Text = Number(value.BurstMultiplier);
            comboMultiplier.Text = Number(value.ComboMultiplier); burstCost.Text = Number(value.BurstCost);
            skillCooldown.Text = Number(value.SkillCooldown); burstCooldown.Text = Number(value.BurstCooldown);
            fieldSeconds.Text = Number(value.FieldSeconds); teamBuff.Text = Number(value.TeamBuffPercent);
            effectDuration.Text = Number(value.EffectDuration);
        }

        internal CombatCharacterInput Read()
        {
            string characterName = (name.Text ?? "").Trim();
            if (characterName.Length == 0) return new CombatCharacterInput { Name = "" };
            return new CombatCharacterInput {
                Name = characterName, Role = Tag(role), Element = (element.Text ?? "").Trim(), Scaling = Tag(scaling),
                CoreStat = Value(core, "主要属性"), CritRate = Value(critRate, "暴击率"), CritDamage = Value(critDamage, "暴击伤害"),
                DamageBonus = Value(damageBonus, "伤害加成"), EnergyRecharge = Value(energyRecharge, "充能"),
                SkillMultiplier = Value(skillMultiplier, "E倍率"), BurstMultiplier = Value(burstMultiplier, "Q倍率"),
                ComboMultiplier = Value(comboMultiplier, "驻场连段倍率"), BurstCost = Value(burstCost, "大招能量"),
                SkillCooldown = Value(skillCooldown, "E冷却"), BurstCooldown = Value(burstCooldown, "Q冷却"),
                FieldSeconds = Value(fieldSeconds, "驻场时间"), TeamBuffPercent = Value(teamBuff, "全队增伤"),
                EffectDuration = Value(effectDuration, "效果持续时间")
            };
        }

        private static string Number(double value) { return value.ToString("0.##", CultureInfo.InvariantCulture); }
        private static double Value(TextBox box, string label)
        {
            double value;
            string text = (box.Text ?? "").Trim().Replace(',', '.');
            if (!Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return Double.NaN;
            return value;
        }
        internal string CharacterName { get { return (name.Text ?? "").Trim(); } }
        internal string Scaling { get { return Tag(scaling); } }
        internal string Element { get { return (element.Text ?? "").Trim(); } }
        internal void ClearErrors()
        {
            foreach (Control control in fields.Values) CombatRotationWindow.ClearError(control);
            Tab.ToolTip = null;
        }
        internal void MarkError(CombatFieldError error)
        {
            Control control;
            if (fields.TryGetValue(error.Field, out control)) CombatRotationWindow.MarkError(control, error.Message);
            Tab.ToolTip = "此角色有输入错误，请查看红框字段。";
        }
        internal void ApplyPanelValues(Dictionary<string, double> values)
        {
            foreach (KeyValuePair<string, double> pair in values) {
                Control control;
                if (fields.TryGetValue(pair.Key, out control) && control is TextBox) {
                    ((TextBox)control).Text = Number(pair.Value); CombatRotationWindow.ClearError(control);
                }
            }
        }
        private static string Tag(ComboBox box)
        {
            ComboBoxItem item = box.SelectedItem as ComboBoxItem;
            return item == null ? "" : Convert.ToString(item.Tag, CultureInfo.InvariantCulture);
        }
        private static void Select(ComboBox box, string tag)
        {
            for (int i = 0; i < box.Items.Count; i++) {
                ComboBoxItem item = (ComboBoxItem)box.Items[i];
                if (Convert.ToString(item.Tag, CultureInfo.InvariantCulture) == tag) { box.SelectedIndex = i; return; }
            }
            box.SelectedIndex = 0;
        }
    }

    internal sealed class CombatRotationWindow : Window
    {
        private readonly List<CombatCharacterEditor> editors = new List<CombatCharacterEditor>();
        private readonly TextBox characterLevel, enemyLevel, enemyResistance, defenseReduction, reactionCoverage;
        private readonly ComboBox reaction;
        private readonly StackPanel resultBody;
        private readonly TextBlock status;
        private readonly bool persist;
        private readonly TabControl tabs;
        private readonly Expander enemySection;
        private readonly StackPanel form, recommendationBody;
        private readonly ComboBox presets;
        private readonly Dictionary<string, Control> enemyFields = new Dictionary<string, Control>();
        private readonly CombatTeamCatalog catalog;
        private string presetId = "";
        private bool importing, closed;

        internal CombatRotationWindow(string character) : this(character, true) { }
        internal CombatRotationWindow(string character, bool usePersistence)
        {
            persist = usePersistence;
            Title = "原神启动 · 战斗循环助手";
            Width = Math.Min(1120, SystemParameters.WorkArea.Width - 32);
            Height = Math.Min(850, SystemParameters.WorkArea.Height - 48);
            MinWidth = 860; MinHeight = 620; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResizeWithGrip; FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13;
            ApplyCharacterTheme(character); ConfigureControls();

            Border shell = new Border { CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(22, 20, 22, 14) };
            shell.SetResourceReference(Border.BackgroundProperty, "PlannerSurface"); shell.SetResourceReference(Border.BorderBrushProperty, "PlannerBorder"); Content = shell;
            Grid root = new Grid(); shell.Child = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 14) }; root.Children.Add(header);
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel(); header.Children.Add(title);
            title.Children.Add(Text("战斗循环助手", 25, true, false));
            TextBlock subtitle = Text("截图识别 / 手动填值 / 配队与手法参考", 12, false, true); subtitle.Margin = new Thickness(0, 5, 0, 0); title.Children.Add(subtitle);
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            StackPanel headerButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(headerButtons, 1); header.Children.Add(headerButtons);
            headerButtons.Children.Add(ActionButton("恢复示例", ResetExample));
            headerButtons.Children.Add(ActionButton("关闭", delegate { Close(); }));

            Grid content = new Grid(); Grid.SetRow(content, 1); root.Children.Add(content);
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(510) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            ScrollViewer formScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 7, 0) };
            form = new StackPanel(); formScroll.Content = form; content.Children.Add(formScroll);
            TextBlock safe = Text("截图在本机识别；不读取内存，不自动按键。", 12, false, true);
            form.Children.Add(Card(safe));
            catalog = CombatTeamPresets.Current;
            form.Children.Add(SectionLabel("推荐配队"));
            presets = new ComboBox { Margin = new Thickness(4, 0, 4, 8) };
            presets.Items.Add(new ComboBoxItem { Content = "自定义配队", Tag = "" });
            foreach (CombatTeamPreset team in catalog.Teams) presets.Items.Add(new ComboBoxItem { Content = CombatTeamPresets.Label(team), Tag = team.Id });
            presets.SelectedIndex = 0; form.Children.Add(presets);
            Button applyPreset = ActionButton("载入成员（替换当前输入）", ApplyPreset);
            applyPreset.HorizontalAlignment = HorizontalAlignment.Stretch; applyPreset.Margin = new Thickness(4, 0, 4, 5); form.Children.Add(applyPreset);
            TextBlock presetHint = Text("载入成员会使用通用示例数字；请导入自己的面板并手填技能倍率。推荐手法与右侧粗算相互独立。", 11, false, true);
            presetHint.Margin = new Thickness(4, 0, 4, 4); form.Children.Add(presetHint);
            UniformGrid enemy = new UniformGrid { Columns = 2 };
            characterLevel = Input(); enemy.Children.Add(Field("角色等级", characterLevel));
            enemyLevel = Input(); enemy.Children.Add(Field("敌人等级", enemyLevel));
            enemyResistance = Input(); enemy.Children.Add(Field("敌人抗性 %", enemyResistance));
            defenseReduction = Input(); enemy.Children.Add(Field("防御降低 %", defenseReduction));
            reaction = Choice(new[] { "不算增幅反应", "1.5倍反应", "2.0倍反应" }, new[] { "1", "1.5", "2" }); enemy.Children.Add(Field("增幅反应", reaction));
            reactionCoverage = Input(); enemy.Children.Add(Field("反应覆盖 %", reactionCoverage));
            enemySection = new Expander { Header = "敌人与反应（展开编辑）", Content = enemy, Margin = new Thickness(4, 10, 4, 8) };
            enemySection.Header = Text("敌人与反应（展开编辑）", 12, true, false);
            enemySection.SetResourceReference(Control.ForegroundProperty, "PlannerText");
            form.Children.Add(enemySection);
            enemyFields["CharacterLevel"] = characterLevel; enemyFields["EnemyLevel"] = enemyLevel;
            enemyFields["EnemyResistance"] = enemyResistance; enemyFields["DefenseReduction"] = defenseReduction;
            enemyFields["ReactionMultiplier"] = reaction; enemyFields["ReactionCoverage"] = reactionCoverage;

            form.Children.Add(SectionLabel("当前配队"));
            StackPanel importButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            importButtons.Children.Add(ActionButton("从截图识别", delegate { ImportPanel(false); }));
            importButtons.Children.Add(ActionButton("粘贴截图识别", delegate { ImportPanel(true); }));
            form.Children.Add(importButtons);
            TextBlock importHint = Text("导入到当前角色页。仅填识别到的面板数值，预览核对后应用；角色、元素与技能需手动设置。", 11, false, true);
            importHint.Margin = new Thickness(4, 0, 4, 8); form.Children.Add(importHint);
            tabs = new TabControl { Height = 445, Margin = new Thickness(4, 0, 4, 10) };
            for (int i = 1; i <= 4; i++) { CombatCharacterEditor editor = new CombatCharacterEditor(i, this); editors.Add(editor); tabs.Items.Add(editor.Tab); }
            form.Children.Add(tabs);
            Button calculate = ActionButton("生成循环与目标", Generate); calculate.HorizontalAlignment = HorizontalAlignment.Stretch; calculate.Margin = new Thickness(4, 2, 4, 8); calculate.Padding = new Thickness(15, 11, 15, 11); form.Children.Add(calculate);

            ScrollViewer resultScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(2, 0, 8, 0) };
            Grid.SetColumn(resultScroll, 2); content.Children.Add(resultScroll);
            StackPanel rightBody = new StackPanel(); resultScroll.Content = rightBody;
            recommendationBody = new StackPanel(); rightBody.Children.Add(recommendationBody);
            resultBody = new StackPanel(); rightBody.Children.Add(resultBody);
            status = Text("计算只保存在本机 %LocalAppData%\\KoloPet。", 11, false, true); status.Margin = new Thickness(0, 12, 0, 0); Grid.SetRow(status, 2); root.Children.Add(status);

            presets.SelectionChanged += delegate { ShowRecommendation(); };
            CombatPlanLoad initial = persist ? CombatPlannerStorage.Load() : new CombatPlanLoad { Input = CombatPlanInput.CreateExample() };
            Load(initial.Input); Generate(false);
            if (!String.IsNullOrWhiteSpace(initial.Notice)) {
                status.Text = initial.Notice;
                TextBlock notice = Text(initial.Notice, 12, false, false); recommendationBody.Children.Insert(0, Card(notice));
            }
            Closed += delegate { closed = true; };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); };
        }

        internal void ApplyCharacterTheme(string character)
        {
            PetWindow.MenuPalette palette = PetWindow.MenuPalette.ForCharacter(character);
            string[] keys = { "PlannerSurface", "PlannerText", "PlannerMuted", "PlannerAccent", "PlannerBorder", "PlannerCard", "PlannerHover" };
            string[] values = { palette.Surface, palette.Text, palette.Muted, palette.Accent, palette.Border, palette.Checked, palette.Hover };
            for (int i = 0; i < keys.Length; i++) { SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i])); brush.Freeze(); Resources[keys[i]] = brush; }
            SetResourceReference(ForegroundProperty, "PlannerText");
        }

        private void ConfigureControls()
        {
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type Button}'><Setter Property='Foreground' Value='{DynamicResource PlannerText}'/><Setter Property='Background' Value='{DynamicResource PlannerCard}'/><Setter Property='BorderBrush' Value='{DynamicResource PlannerBorder}'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border x:Name='Chrome' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource PlannerHover}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource PlannerAccent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type TextBox}'><Setter Property='Foreground' Value='{DynamicResource PlannerText}'/><Setter Property='Background' Value='{DynamicResource PlannerCard}'/><Setter Property='BorderBrush' Value='{DynamicResource PlannerBorder}'/><Setter Property='Padding' Value='8,6'/><Setter Property='Margin' Value='0,4,0,0'/></Style>
<Style TargetType='{x:Type ComboBox}'>
<Setter Property='Foreground' Value='{DynamicResource PlannerText}'/><Setter Property='Background' Value='{DynamicResource PlannerCard}'/><Setter Property='BorderBrush' Value='{DynamicResource PlannerBorder}'/><Setter Property='FontWeight' Value='Normal'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='8,6'/><Setter Property='Margin' Value='0,4,0,0'/><Setter Property='MaxDropDownHeight' Value='300'/>
<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'><Grid>
<ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border x:Name='Chrome' Background='{DynamicResource PlannerCard}' BorderBrush='{DynamicResource PlannerBorder}' BorderThickness='1' CornerRadius='5'><TextBlock Text='⌄' Foreground='{DynamicResource PlannerMuted}' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,9,0'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource PlannerHover}'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource PlannerAccent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton>
<Border BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='5' IsHitTestVisible='False'><ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='8,6,27,6' VerticalAlignment='Center'/></Border>
<Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'><Border MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='{TemplateBinding MaxDropDownHeight}' Background='{DynamicResource PlannerSurface}' BorderBrush='{DynamicResource PlannerBorder}' BorderThickness='1' Padding='3'><ScrollViewer CanContentScroll='True' VerticalScrollBarVisibility='Auto'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup>
</Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ComboBoxItem}'><Setter Property='Foreground' Value='{DynamicResource PlannerText}'/><Setter Property='Background' Value='{DynamicResource PlannerSurface}'/><Setter Property='Padding' Value='8,7'/><Setter Property='FontWeight' Value='Normal'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBoxItem}'><Border x:Name='Item' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' CornerRadius='4'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Item' Property='Background' Value='{DynamicResource PlannerHover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='Item' Property='Background' Value='{DynamicResource PlannerCard}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type TabControl}'><Setter Property='Foreground' Value='{DynamicResource PlannerText}'/><Setter Property='Background' Value='{DynamicResource PlannerCard}'/><Setter Property='BorderBrush' Value='{DynamicResource PlannerBorder}'/></Style>
<Style TargetType='{x:Type TabItem}'><Setter Property='Foreground' Value='{DynamicResource PlannerText}'/><Setter Property='Background' Value='{DynamicResource PlannerCard}'/><Setter Property='BorderBrush' Value='{DynamicResource PlannerBorder}'/><Setter Property='Padding' Value='12,7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabItem}'><Border x:Name='Tab' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1,1,1,0' CornerRadius='6,6,0,0' Padding='{TemplateBinding Padding}'><ContentPresenter x:Name='HeaderHost' ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='Tab' Property='Background' Value='{DynamicResource PlannerHover}'/><Setter TargetName='HeaderHost' Property='TextElement.FontWeight' Value='SemiBold'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Tab' Property='Background' Value='{DynamicResource PlannerHover}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ScrollBar}'><Setter Property='Width' Value='11'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='{DynamicResource PlannerBorder}' CornerRadius='4' Margin='3,0'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
            Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(xaml));
        }

        private void Load(CombatPlanInput value)
        {
            foreach (Control control in enemyFields.Values) ClearError(control);
            presetId = value.PresetId ?? "";
            Select(presets, presetId); ShowRecommendation();
            characterLevel.Text = value.CharacterLevel.ToString(CultureInfo.InvariantCulture);
            enemyLevel.Text = value.EnemyLevel.ToString(CultureInfo.InvariantCulture);
            enemyResistance.Text = value.EnemyResistance.ToString("0.##", CultureInfo.InvariantCulture);
            defenseReduction.Text = value.DefenseReduction.ToString("0.##", CultureInfo.InvariantCulture);
            reactionCoverage.Text = value.ReactionCoverage.ToString("0.##", CultureInfo.InvariantCulture);
            Select(reaction, value.ReactionMultiplier.ToString("0.##", CultureInfo.InvariantCulture));
            for (int i = 0; i < editors.Count; i++) editors[i].Load(i < value.Characters.Count ? value.Characters[i] : CombatPlanInput.EmptySlot(i));
        }

        private CombatPlanInput ReadInput()
        {
            CombatPlanInput value = new CombatPlanInput {
                Version = CombatPlanInput.CurrentVersion, PresetId = presetId,
                CharacterLevel = Level(characterLevel),
                EnemyLevel = Level(enemyLevel),
                EnemyResistance = Numeric(enemyResistance, "敌人抗性"),
                DefenseReduction = Numeric(defenseReduction, "防御降低"),
                ReactionMultiplier = NumericTag(reaction),
                ReactionCoverage = Numeric(reactionCoverage, "反应覆盖"),
                Characters = new List<CombatCharacterInput>()
            };
            foreach (CombatCharacterEditor editor in editors) value.Characters.Add(editor.Read());
            return value;
        }

        private void Generate() { Generate(true); }
        private void Generate(bool save)
        {
            foreach (Control control in enemyFields.Values) ClearError(control);
            foreach (CombatCharacterEditor editor in editors) editor.ClearErrors();
            try {
                CombatPlanInput input = ReadInput();
                CombatTeamPreset applied = catalog.Find(presetId);
                if (applied != null) {
                    bool sameMembers = true;
                    for (int i = 0; i < editors.Count; i++) {
                        string expected = i < applied.Members.Count ? applied.Members[i].Name.Trim() : "";
                        if (editors[i].CharacterName != expected) { sameMembers = false; break; }
                    }
                    if (!sameMembers) { presetId = ""; input.PresetId = ""; }
                }
                CombatPlanResult result = CombatPlanner.Calculate(input);
                Render(result);
                status.Text = save && persist ? "已计算，并保存当前配队。" : "已按当前面板计算。";
                if (save && persist) {
                    try { CombatPlannerStorage.Save(input); }
                    catch (Exception saveError) { PetStorage.Log("combat-plan-save", saveError); status.Text = "计算完成，但保存失败：" + saveError.Message + " 当前输入仍保留在界面。"; }
                }
            } catch (CombatValidationException error) {
                resultBody.Children.Clear();
                resultBody.Children.Add(Card(Text("请修正以下 " + error.Errors.Count + " 项输入\n" + error.Message, 13, false, false)));
                int firstSlot = 0;
                foreach (CombatFieldError fieldError in error.Errors) {
                    Control control;
                    if (fieldError.Slot == 0 && enemyFields.TryGetValue(fieldError.Field, out control)) { enemySection.IsExpanded = true; MarkError(control, fieldError.Message); }
                    if (fieldError.Slot > 0 && fieldError.Slot <= editors.Count) {
                        editors[fieldError.Slot - 1].MarkError(fieldError); if (firstSlot == 0) firstSlot = fieldError.Slot;
                    }
                }
                if (firstSlot > 0) tabs.SelectedIndex = firstSlot - 1;
                status.Text = "错误字段已标红；鼠标停在红框上可查看该字段说明。其他角色页的错误也已列出。";
            } catch (Exception error) {
                PetStorage.Log("combat-planner-ui", error);
                resultBody.Children.Clear();
                TextBlock message = Text("无法计算\n" + error.Message, 15, true, false); resultBody.Children.Add(Card(message));
                status.Text = "当前输入已保留。请检查输入或重试。";
            }
        }

        internal void Render(CombatPlanResult value)
        {
            resultBody.Children.Clear();
            CombatTeamPreset applied = catalog.Find(presetId);
            if (applied != null && !applied.SupportsEstimate) {
                resultBody.Children.Add(SectionLabel("此队仅提供手法参考"));
                resultBody.Children.Add(Card(Text("当前通用计算尚未覆盖此队的特殊机制，暂不显示伤害与达标后每秒。面板仍可手填、截图导入和保存。\n" + applied.ModelNote, 13, false, false)));
                return;
            }
            resultBody.Children.Add(SectionLabel("建议循环"));
            resultBody.Children.Add(Text("以下为按职责生成的通用估算顺序；角色专属输出手法请看上方攻略卡片。", 11, false, true));
            TextBlock steps = Text(String.Join("\n", value.Steps.ToArray()), 14, true, false); steps.LineHeight = 25; resultBody.Children.Add(Card(steps));
            resultBody.Children.Add(SectionLabel("伤害粗算"));
            UniformGrid summary = new UniformGrid { Columns = 2 };
            summary.Children.Add(Metric("完整循环", value.RotationSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " 秒"));
            summary.Children.Add(Metric("当前期望", Number(value.CurrentDamage)));
            summary.Children.Add(Metric("当前每秒", Number(value.CurrentDps)));
            summary.Children.Add(Metric("达标后期望", Number(value.TargetDamage)));
            summary.Children.Add(Metric("达标后每秒", Number(value.TargetDps)));
            resultBody.Children.Add(summary);
            resultBody.Children.Add(SectionLabel("通用参考线"));
            foreach (CombatCharacterResult character in value.Characters) {
                StackPanel content = new StackPanel();
                content.Children.Add(Text(character.Name + "  ·  " + CombatPlanner.RoleLabel(character.Role), 16, true, false));
                string targets = CombatPlanner.ScalingLabel(character.Scaling) + " " + character.TargetCoreStat.ToString("#,##0", CultureInfo.InvariantCulture)
                    + "  ·  充能 " + character.RequiredEnergyRecharge.ToString("0", CultureInfo.InvariantCulture) + "%";
                if (character.TargetCritRate > 0) targets += "  ·  双暴 " + character.TargetCritRate.ToString("0", CultureInfo.InvariantCulture) + "/" + character.TargetCritDamage.ToString("0", CultureInfo.InvariantCulture);
                TextBlock target = Text(targets, 12, false, true); target.Margin = new Thickness(0, 6, 0, 5); content.Children.Add(target);
                content.Children.Add(Text("本角色当前期望 " + Number(character.CurrentDamage) + "  →  达标后 " + Number(character.TargetDamage), 12, true, false));
                TextBlock advice = Text("建议：" + String.Join(" ", character.Advice.ToArray()), 12, false, true); advice.Margin = new Thickness(0, 7, 0, 0); content.Children.Add(advice);
                resultBody.Children.Add(Card(content));
            }
            resultBody.Children.Add(SectionLabel("说明"));
            TextBlock warnings = Text(String.Join("\n", value.Warnings.ToArray()) + "\n参考线按职责设置，并非具体角色的毕业标准。特殊反应与角色机制不用于此处精确排名。", 11.5, false, true);
            resultBody.Children.Add(Card(warnings));
        }

        private void ResetExample()
        {
            if (importing) return;
            if (MessageBox.Show("恢复示例会盖掉当前输入。", "战斗循环助手", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            Load(CombatPlanInput.CreateExample()); Generate();
        }

        private CombatTeamPreset SelectedPreset()
        {
            ComboBoxItem item = presets.SelectedItem as ComboBoxItem;
            return item == null ? null : catalog.Find(Convert.ToString(item.Tag, CultureInfo.InvariantCulture));
        }

        private void ApplyPreset()
        {
            CombatTeamPreset selected = SelectedPreset();
            if (selected == null) { status.Text = "先选择一个推荐队伍；自定义配队可以直接在角色页修改。"; return; }
            Load(CombatTeamPresets.Apply(ReadInput(), selected));
            resultBody.Children.Clear();
            resultBody.Children.Add(Card(Text("已载入「" + selected.Name + "」的成员。\n当前数字为通用示例，并非角色实测面板。先导入或填写自己的面板、技能倍率和时间，再生成估算。", 13, false, false)));
            status.Text = "已载入成员，尚未保存。示例数字需核对替换；点击生成后保存。";
        }

        private void ShowRecommendation()
        {
            recommendationBody.Children.Clear();
            recommendationBody.Children.Add(SectionLabel(CombatTeamPresets.Header(catalog)));
            recommendationBody.Children.Add(Text(catalog.Notice, 11, false, true));
            CombatTeamPreset selected = SelectedPreset();
            if (selected == null) return;
            StackPanel card = new StackPanel();
            card.Children.Add(Text(selected.Name, 18, true, false));
            List<string> members = new List<string>();
            foreach (CombatTeamMember member in selected.Members) members.Add(member.Name + " · " + CombatPlanner.RoleLabel(member.Role));
            card.Children.Add(Text(String.Join(" / ", members.ToArray()), 12, false, true));
            card.Children.Add(Text(selected.VersionScope + "  ·  核对 " + selected.VerifiedAt, 11, false, true));
            card.Children.Add(SectionLabel("推荐输出手法"));
            card.Children.Add(Text(String.Join("\n", selected.Rotation.ToArray()), 13, false, false));
            card.Children.Add(Text("手法依据：" + selected.RotationBasis, 11, false, true));
            card.Children.Add(SectionLabel("充能与操作提示"));
            card.Children.Add(Text(selected.Energy + "\n" + selected.Tips, 12, false, false));
            foreach (CombatTeamMember member in selected.Members)
                if (!String.IsNullOrWhiteSpace(member.Note)) card.Children.Add(Text(member.Name + "：" + member.Note, 11, false, true));
            card.Children.Add(SectionLabel("适用范围"));
            card.Children.Add(Text(selected.ModelNote, 11, false, true));
            foreach (CombatTeamSource source in selected.Sources) {
                string url = source.Url;
                Button link = ActionButton("来源 · " + source.Title, delegate { OpenSource(url); });
                link.Content = Text("来源 · " + source.Title, 12, false, false);
                link.HorizontalAlignment = HorizontalAlignment.Stretch; link.Margin = new Thickness(0, 6, 0, 0);
                link.ToolTip = url; card.Children.Add(link);
            }
            recommendationBody.Children.Add(Card(card));
        }

        private void OpenSource(string url)
        {
            if (!CombatTeamPresets.IsSafeLink(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception error) { PetStorage.Log("combat-source-link", error); status.Text = "无法打开来源：" + error.Message; }
        }

        private async void ImportPanel(bool clipboard)
        {
            if (importing || closed) return;
            CombatCharacterEditor editor = editors[Math.Max(0, tabs.SelectedIndex)];
            try {
                string unavailable = CombatPanelImport.Extractor.Unavailable();
                if (unavailable != null) { status.Text = unavailable + " 也可直接手动填写。"; return; }
                BitmapSource image;
                if (clipboard) image = CombatPanelImport.FromClipboard();
                else {
                    OpenFileDialog picker = new OpenFileDialog { Title = "选择角色属性面板截图", Filter = "角色截图|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true, Multiselect = false };
                    if (picker.ShowDialog(this) != true) return;
                    image = CombatPanelImport.LoadFile(picker.FileName);
                }
                if (image == null) { status.Text = "剪贴板中没有图片。可先复制面板截图，或选择图片文件。"; return; }
                importing = true; form.IsEnabled = false;
                status.Text = "正在本机识别截图…";
                PanelStatReading reading = await CombatPanelImport.Extractor.ExtractAsync(image);
                if (closed) return;
                Dictionary<string, double> values = PreviewReading(editor, reading, image);
                if (values == null) { status.Text = "已取消导入，原输入未改动。"; return; }
                editor.ApplyPanelValues(values);
                resultBody.Children.Clear();
                resultBody.Children.Add(Card(Text("已导入 " + values.Count + " 项面板数值。\n确认角色、元素和伤害基值正确后，填写技能倍率并重新生成。", 13, false, false)));
                status.Text = "已填入当前角色页，尚未保存；可以继续手动修正。";
            } catch (Exception error) {
                PetStorage.Log("combat-panel-import", error);
                if (!closed) status.Text = "截图识别失败：" + error.Message + " 可直接手动填值。";
            } finally { importing = false; if (!closed) form.IsEnabled = true; }
        }

        private Dictionary<string, double> PreviewReading(CombatCharacterEditor editor, PanelStatReading reading, BitmapSource image)
        {
            Dictionary<string, double> candidates = new Dictionary<string, double>();
            AddReading(candidates, "CoreStat", reading.CoreFor(editor.Scaling));
            AddReading(candidates, "CritRate", reading.CritRate); AddReading(candidates, "CritDamage", reading.CritDamage);
            AddReading(candidates, "EnergyRecharge", reading.EnergyRecharge); AddReading(candidates, "DamageBonus", reading.BonusFor(editor.Element));
            Window preview = new Window { Title = "核对截图识别结果", Owner = this, Width = 570, Height = 700,
                MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 48), WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily = FontFamily, FontSize = 13, ResizeMode = ResizeMode.CanResize };
            preview.Resources.MergedDictionaries.Add(Resources);
            preview.SetResourceReference(Window.BackgroundProperty, "PlannerSurface");
            preview.SetResourceReference(Window.ForegroundProperty, "PlannerText");
            DockPanel shell = new DockPanel { Margin = new Thickness(18) }; preview.Content = shell;
            StackPanel footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); shell.Children.Add(footer);
            TextBlock notice = Text("只应用勾选的项目；未识别字段保留原值。可在此纠正数字。", 12, false, true); footer.Children.Add(notice);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; footer.Children.Add(buttons);
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; shell.Children.Add(scroll);
            StackPanel body = new StackPanel(); scroll.Content = body;
            body.Children.Add(Text("导入到：" + (editor.CharacterName.Length == 0 ? "当前空角色页" : editor.CharacterName), 19, true, false));
            body.Children.Add(Text("伤害基值：" + CombatPlanner.ScalingLabel(editor.Scaling) + "  ·  元素：" + editor.Element + "\n" + reading.Engine, 12, false, true));
            body.Children.Add(new Image { Source = image, Height = 160, Stretch = Stretch.Uniform, Margin = new Thickness(0, 10, 0, 12) });
            string[] keys = { "CoreStat", "CritRate", "CritDamage", "EnergyRecharge", "DamageBonus" };
            string[] labels = { CombatPlanner.ScalingLabel(editor.Scaling), "暴击率 %", "暴击伤害 %", "元素充能效率 %", "对应元素/物理加成 %" };
            Dictionary<string, TextBox> inputs = new Dictionary<string, TextBox>();
            Dictionary<string, CheckBox> selected = new Dictionary<string, CheckBox>();
            for (int i = 0; i < keys.Length; i++) {
                if (!candidates.ContainsKey(keys[i])) continue;
                Grid row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                CheckBox check = new CheckBox { Content = labels[i], IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
                check.SetResourceReference(Control.ForegroundProperty, "PlannerText"); row.Children.Add(check);
                TextBox input = Input(); input.Text = candidates[keys[i]].ToString("0.##", CultureInfo.InvariantCulture); Grid.SetColumn(input, 1); row.Children.Add(input);
                inputs[keys[i]] = input; selected[keys[i]] = check; body.Children.Add(row);
            }
            if (candidates.Count == 0) body.Children.Add(Text("未识别到适用于此角色的面板字段。请核对元素和伤害基值，或使用清晰的详细属性截图。", 13, false, false));
            Expander raw = new Expander { Header = "识别原文（供核对）", Margin = new Thickness(0, 12, 0, 12), Content = Text(String.Join("\n", reading.Lines.ToArray()), 11, false, true) }; body.Children.Add(raw);
            raw.Header = Text("识别原文（供核对）", 12, false, false); raw.SetResourceReference(Control.ForegroundProperty, "PlannerText");
            Dictionary<string, double> accepted = null;
            buttons.Children.Add(ActionButton("取消", delegate { preview.Close(); }));
            Button apply = ActionButton("应用勾选数值", delegate {
                Dictionary<string, double> pending = new Dictionary<string, double>(); bool valid = true;
                foreach (KeyValuePair<string, TextBox> item in inputs) {
                    ClearError(item.Value); if (selected[item.Key].IsChecked != true) continue;
                    double value = Numeric(item.Value, item.Key);
                    double min = item.Key == "CoreStat" ? 1 : item.Key == "EnergyRecharge" ? 100 : item.Key == "DamageBonus" ? -90 : 0;
                    double max = item.Key == "CoreStat" ? 100000 : item.Key == "CritRate" ? 100 : item.Key == "EnergyRecharge" ? 400 : 500;
                    if (Double.IsNaN(value) || Double.IsInfinity(value) || value < min || value > max) {
                        MarkError(item.Value, "范围应为 " + min + "–" + max); valid = false;
                    } else pending[item.Key] = value;
                }
                if (!valid || pending.Count == 0) { notice.Text = "请勾选至少一项，并修正红框内的数值。"; return; }
                accepted = pending; preview.Close();
            });
            apply.IsEnabled = candidates.Count > 0; buttons.Children.Add(apply);
            preview.ShowDialog(); return accepted;
        }

        private static void AddReading(Dictionary<string, double> values, string field, double? value)
        { if (value.HasValue) values[field] = value.Value; }

        internal TextBox Input() { return new TextBox { MinWidth = 70 }; }
        internal ComboBox Choice(string[] labels, string[] values)
        {
            ComboBox box = new ComboBox();
            for (int i = 0; i < labels.Length; i++) box.Items.Add(new ComboBoxItem { Content = labels[i], Tag = values[i] });
            box.SelectedIndex = 0; return box;
        }
        internal Border Field(string label, Control control)
        {
            StackPanel panel = new StackPanel(); panel.Children.Add(Text(label, 11, false, true)); panel.Children.Add(control);
            return new Border { Child = panel, Margin = new Thickness(4, 3, 4, 7) };
        }
        internal TextBlock SectionLabel(string value)
        {
            TextBlock block = Text(value, 14, true, false); block.Margin = new Thickness(4, 9, 4, 8); return block;
        }
        internal TextBlock Text(string value, double size, bool bold, bool muted)
        {
            TextBlock block = new TextBlock { Text = value ?? "", FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.5 };
            block.SetResourceReference(TextBlock.ForegroundProperty, muted ? "PlannerMuted" : "PlannerText"); return block;
        }
        private Border Card(UIElement child)
        {
            Border card = new Border { Child = child, CornerRadius = new CornerRadius(11), Padding = new Thickness(15, 12, 15, 12), Margin = new Thickness(4, 0, 4, 10), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "PlannerCard"); card.SetResourceReference(Border.BorderBrushProperty, "PlannerBorder"); return card;
        }
        private Border Metric(string label, string value)
        {
            StackPanel panel = new StackPanel(); panel.Children.Add(Text(label, 11, false, true)); TextBlock number = Text(value, 20, true, false); number.Margin = new Thickness(0, 4, 0, 0); number.SetResourceReference(TextBlock.ForegroundProperty, "PlannerAccent"); panel.Children.Add(number);
            Border card = Card(panel); card.Margin = new Thickness(4, 0, 4, 8); return card;
        }
        private Button ActionButton(string label, Action action)
        {
            Button button = new Button { Content = label, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(6, 0, 0, 0) };
            button.Click += delegate { action(); }; return button;
        }
        private static double Numeric(TextBox box, string label)
        {
            double value;
            if (!Double.TryParse((box.Text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return Double.NaN;
            return value;
        }
        private static double NumericTag(ComboBox box)
        {
            ComboBoxItem item = box.SelectedItem as ComboBoxItem;
            double value;
            if (item == null || !Double.TryParse(Convert.ToString(item.Tag, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return Double.NaN;
            return value;
        }
        private static void Select(ComboBox box, string tag)
        {
            for (int i = 0; i < box.Items.Count; i++) {
                ComboBoxItem item = (ComboBoxItem)box.Items[i];
                if (Convert.ToString(item.Tag, CultureInfo.InvariantCulture) == tag) { box.SelectedIndex = i; return; }
            }
            box.SelectedIndex = 0;
        }
        private static string Number(double value) { return value.ToString("#,##0", CultureInfo.InvariantCulture); }
        private static int Level(TextBox box)
        {
            double value = Numeric(box, "等级");
            return Double.IsNaN(value) || Double.IsInfinity(value) || value < 1 || value > 200 || value != Math.Truncate(value) ? -1 : (int)value;
        }
        internal static void MarkError(Control control, string message)
        { control.BorderBrush = new SolidColorBrush(Color.FromRgb(230, 78, 91)); control.BorderThickness = new Thickness(2); control.ToolTip = message; }
        internal static void ClearError(Control control)
        { control.SetResourceReference(Control.BorderBrushProperty, "PlannerBorder"); control.ClearValue(Control.BorderThicknessProperty); control.ToolTip = null; }
    }
}
