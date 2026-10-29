using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace ClaudePet
{
    internal sealed class CombatCharacterInput
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Element { get; set; }
        public string Scaling { get; set; }
        public double CoreStat { get; set; }
        public double CritRate { get; set; }
        public double CritDamage { get; set; }
        public double DamageBonus { get; set; }
        public double EnergyRecharge { get; set; }
        public double SkillMultiplier { get; set; }
        public double BurstMultiplier { get; set; }
        public double ComboMultiplier { get; set; }
        public double BurstCost { get; set; }
        public double SkillCooldown { get; set; }
        public double BurstCooldown { get; set; }
        public double FieldSeconds { get; set; }
        public double TeamBuffPercent { get; set; }
        public double EffectDuration { get; set; }
    }

    internal sealed class CombatPlanInput
    {
        public int Version { get; set; }
        public int CharacterLevel { get; set; }
        public int EnemyLevel { get; set; }
        public double EnemyResistance { get; set; }
        public double DefenseReduction { get; set; }
        public double ReactionMultiplier { get; set; }
        public double ReactionCoverage { get; set; }
        public List<CombatCharacterInput> Characters { get; set; }

        internal static CombatPlanInput CreateExample()
        {
            return new CombatPlanInput {
                Version = 1,
                CharacterLevel = 90,
                EnemyLevel = 90,
                EnemyResistance = 10,
                DefenseReduction = 0,
                ReactionMultiplier = 1,
                ReactionCoverage = 0,
                Characters = new List<CombatCharacterInput> {
                    Character("主C", "main", "火", "attack", 2000, 70, 140, 46.6, 120, 300, 700, 1300, 60, 10, 18, 8, 0, 0),
                    Character("副C", "offfield", "水", "attack", 1800, 60, 120, 46.6, 160, 250, 900, 0, 80, 12, 20, 1, 0, 15),
                    Character("增伤辅助", "support", "风", "attack", 1500, 40, 80, 0, 210, 100, 300, 0, 80, 15, 20, 1, 25, 12),
                    Character("生存位", "sustain", "岩", "attack", 1500, 20, 50, 0, 160, 100, 200, 0, 70, 12, 20, 1, 0, 18)
                }
            };
        }

        private static CombatCharacterInput Character(string name, string role, string element, string scaling,
            double core, double rate, double damage, double bonus, double recharge, double skill, double burst,
            double combo, double cost, double skillCooldown, double burstCooldown, double fieldSeconds,
            double teamBuff, double effectDuration)
        {
            return new CombatCharacterInput {
                Name = name, Role = role, Element = element, Scaling = scaling, CoreStat = core,
                CritRate = rate, CritDamage = damage, DamageBonus = bonus, EnergyRecharge = recharge,
                SkillMultiplier = skill, BurstMultiplier = burst, ComboMultiplier = combo, BurstCost = cost,
                SkillCooldown = skillCooldown, BurstCooldown = burstCooldown, FieldSeconds = fieldSeconds,
                TeamBuffPercent = teamBuff, EffectDuration = effectDuration
            };
        }
    }

    internal sealed class CombatCharacterResult
    {
        internal string Name;
        internal string Role;
        internal string Scaling;
        internal double RequiredEnergyRecharge;
        internal double TargetCoreStat;
        internal double TargetCritRate;
        internal double TargetCritDamage;
        internal double TargetDamageBonus;
        internal double BurstCoverage;
        internal double CurrentDamage;
        internal double TargetDamage;
        internal readonly List<string> Advice = new List<string>();
    }

    internal sealed class CombatPlanResult
    {
        internal readonly List<string> Steps = new List<string>();
        internal readonly List<CombatCharacterResult> Characters = new List<CombatCharacterResult>();
        internal readonly List<string> Warnings = new List<string>();
        internal double RotationSeconds;
        internal double CurrentDamage;
        internal double TargetDamage;
        internal double CurrentDps;
        internal double TargetDps;
    }

    internal static class CombatPlanner
    {
        private static readonly string[] Roles = { "main", "offfield", "support", "sustain" };
        private static readonly string[] Scalings = { "attack", "health", "defense" };

        internal static CombatPlanResult Calculate(CombatPlanInput input)
        {
            Validate(input);
            List<CombatCharacterInput> characters = new List<CombatCharacterInput>();
            foreach (CombatCharacterInput character in input.Characters)
                if (!String.IsNullOrWhiteSpace(character.Name)) characters.Add(character);
            if (characters.Count == 0) throw new ArgumentException("请至少填写一名角色。");
            if (characters.Count > 4) throw new ArgumentException("一支队伍最多填写四名角色。");

            List<CombatCharacterInput> order = new List<CombatCharacterInput>(characters);
            order.Sort(CompareRotationOrder);
            CombatPlanResult result = new CombatPlanResult();
            int mainCount = 0;
            double mainField = 0;
            foreach (CombatCharacterInput character in characters) {
                if (character.Role == "main") { mainCount++; mainField = Math.Max(mainField, character.FieldSeconds); }
            }
            if (mainCount == 0) result.Warnings.Add("没有主C。循环仍可生成，但缺少明确驻场角色。");
            if (mainCount > 1) result.Warnings.Add("有多名主C。请减少驻场时间，避免互相抢时间。");

            double actionSeconds = 0;
            double longestCooldown = 0;
            for (int i = 0; i < order.Count; i++) {
                CombatCharacterInput character = order[i];
                List<string> actions = new List<string>();
                if (character.SkillMultiplier > 0 || character.SkillCooldown > 0) {
                    actions.Add("E"); actionSeconds += 1.0; longestCooldown = Math.Max(longestCooldown, character.SkillCooldown);
                }
                if (character.BurstMultiplier > 0 && character.BurstCost > 0) {
                    actions.Add("Q"); actionSeconds += 1.6; longestCooldown = Math.Max(longestCooldown, character.BurstCooldown);
                }
                if (character.FieldSeconds > 0 && (character.Role == "main" || character.ComboMultiplier > 0)) {
                    actions.Add("驻场连段 " + character.FieldSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "秒");
                    actionSeconds += character.FieldSeconds;
                }
                if (actions.Count == 0) actions.Add("切入触发效果");
                result.Steps.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + character.Name + "：" + String.Join(" → ", actions.ToArray()));
                if (i + 1 < order.Count) actionSeconds += 0.6;
            }
            result.RotationSeconds = Math.Max(actionSeconds, longestCooldown);
            if (result.RotationSeconds - actionSeconds >= 0.5)
                result.Steps.Add("补位：普攻或走位 " + (result.RotationSeconds - actionSeconds).ToString("0.#", CultureInfo.InvariantCulture) + "秒，等待技能。");

            foreach (CombatCharacterInput character in characters) {
                if (character.TeamBuffPercent > 0 && character.EffectDuration > 0 && mainField > character.EffectDuration)
                    result.Warnings.Add(character.Name + "的增益短于主C驻场。请在主C前释放，或缩短连段。");
                CombatCharacterResult evaluation = Evaluate(input, characters, character);
                result.Characters.Add(evaluation);
                result.CurrentDamage += evaluation.CurrentDamage;
                result.TargetDamage += evaluation.TargetDamage;
            }
            result.CurrentDps = result.CurrentDamage / Math.Max(1, result.RotationSeconds);
            result.TargetDps = result.TargetDamage / Math.Max(1, result.RotationSeconds);
            result.Warnings.Add("伤害是通用粗算，不含减抗之外的特殊机制。");
            result.Warnings.Add("元素精通、怪物阶段和命座效果需另行修正。");
            return result;
        }

        private static CombatCharacterResult Evaluate(CombatPlanInput input, List<CombatCharacterInput> team, CombatCharacterInput character)
        {
            double rawEnergy = EstimatedRawEnergy(character, team);
            double requiredEnergy = character.BurstCost > 0 && character.BurstMultiplier > 0
                ? RoundUp(Math.Max(100, Math.Min(300, character.BurstCost / Math.Max(1, rawEnergy) * 100)), 5) : 100;
            double burstCoverage = character.BurstCost > 0 && character.BurstMultiplier > 0
                ? Math.Max(0, Math.Min(1, rawEnergy * character.EnergyRecharge / 100 / character.BurstCost)) : 1;
            double targetCore = MinimumCore(character.Scaling, character.Role);
            double targetRate = character.Role == "sustain" ? 0 : character.Role == "support" ? 50 : character.Role == "offfield" ? 60 : 65;
            double targetCritDamage = character.Role == "sustain" ? 0 : character.Role == "support" ? 100 : character.Role == "offfield" ? 120 : 130;
            double targetBonus = character.Role == "main" || character.Role == "offfield" ? 46.6 : 0;
            double teamBuff = 0;
            foreach (CombatCharacterInput ally in team)
                if (!Object.ReferenceEquals(ally, character)) teamBuff += ally.TeamBuffPercent;
            teamBuff = Math.Min(200, teamBuff);

            CombatCharacterResult result = new CombatCharacterResult {
                Name = character.Name,
                Role = character.Role,
                Scaling = character.Scaling,
                RequiredEnergyRecharge = requiredEnergy,
                TargetCoreStat = targetCore,
                TargetCritRate = targetRate,
                TargetCritDamage = targetCritDamage,
                TargetDamageBonus = targetBonus,
                BurstCoverage = burstCoverage
            };
            result.CurrentDamage = ExpectedDamage(input, character, teamBuff, character.CoreStat, character.CritRate,
                character.CritDamage, character.DamageBonus, burstCoverage);
            result.TargetDamage = ExpectedDamage(input, character, teamBuff, Math.Max(character.CoreStat, targetCore),
                Math.Max(character.CritRate, targetRate), Math.Max(character.CritDamage, targetCritDamage),
                Math.Max(character.DamageBonus, targetBonus), 1);

            if (character.BurstMultiplier > 0 && character.BurstCost > 0 && character.EnergyRecharge + 0.1 < requiredEnergy)
                result.Advice.Add("充能先到 " + requiredEnergy.ToString("0", CultureInfo.InvariantCulture) + "%；目前大招约覆盖 " + (burstCoverage * 100).ToString("0", CultureInfo.InvariantCulture) + "% 循环。");
            if (character.CoreStat + 0.1 < targetCore)
                result.Advice.Add(ScalingLabel(character.Scaling) + "先到 " + targetCore.ToString("#,##0", CultureInfo.InvariantCulture) + "。");
            if (character.CritRate + 0.1 < targetRate)
                result.Advice.Add("暴击率先到 " + targetRate.ToString("0", CultureInfo.InvariantCulture) + "%。");
            if (character.CritDamage + 0.1 < targetCritDamage)
                result.Advice.Add("暴击伤害先到 " + targetCritDamage.ToString("0", CultureInfo.InvariantCulture) + "%。");
            if (character.DamageBonus + 0.1 < targetBonus)
                result.Advice.Add("对应元素伤害加成先到 " + targetBonus.ToString("0.#", CultureInfo.InvariantCulture) + "% 左右。");
            if (result.Advice.Count == 0)
                result.Advice.Add("已达到通用最低线。继续提升双暴或主要伤害属性。");
            return result;
        }

        internal static double ExpectedDamage(CombatPlanInput input, CombatCharacterInput character, double teamBuff,
            double coreStat, double critRate, double critDamage, double damageBonus, double burstCoverage)
        {
            double talent = character.SkillMultiplier + character.ComboMultiplier + character.BurstMultiplier * burstCoverage;
            double critical = 1 + Math.Max(0, Math.Min(100, critRate)) / 100 * Math.Max(0, critDamage) / 100;
            double bonus = 1 + Math.Max(-90, damageBonus + teamBuff) / 100;
            double defense = DefenseMultiplier(input.CharacterLevel, input.EnemyLevel, input.DefenseReduction);
            double resistance = ResistanceMultiplier(input.EnemyResistance);
            double reaction = 1 + Math.Max(0, Math.Min(1, input.ReactionCoverage / 100)) * (input.ReactionMultiplier - 1);
            return Math.Max(0, coreStat) * Math.Max(0, talent) / 100 * bonus * critical * defense * resistance * reaction;
        }

        internal static double DefenseMultiplier(int characterLevel, int enemyLevel, double reductionPercent)
        {
            double attacker = characterLevel + 100.0;
            double defender = (enemyLevel + 100.0) * (1 - Math.Max(0, Math.Min(90, reductionPercent)) / 100);
            return attacker / Math.Max(1, attacker + defender);
        }

        internal static double ResistanceMultiplier(double resistancePercent)
        {
            double resistance = resistancePercent / 100;
            if (resistance < 0) return 1 - resistance / 2;
            if (resistance < 0.75) return 1 - resistance;
            return 1 / (4 * resistance + 1);
        }

        private static double EstimatedRawEnergy(CombatCharacterInput character, List<CombatCharacterInput> team)
        {
            double energy = character.Role == "main" ? 44 : character.Role == "offfield" ? 36 : character.Role == "support" ? 32 : 30;
            if (character.SkillCooldown <= 6) energy += 10;
            else if (character.SkillCooldown <= 10) energy += 6;
            else if (character.SkillCooldown <= 15) energy += 3;
            else if (character.SkillCooldown >= 20) energy -= 4;
            int sameElement = 0;
            if (!String.IsNullOrWhiteSpace(character.Element) && character.Element != "无")
                foreach (CombatCharacterInput ally in team) if (ally.Element == character.Element) sameElement++;
            if (sameElement > 1) energy *= 1 + 0.12 * (sameElement - 1);
            return Math.Max(10, energy);
        }

        private static double MinimumCore(string scaling, string role)
        {
            if (scaling == "health") return role == "main" ? 32000 : role == "offfield" ? 30000 : role == "support" ? 28000 : 32000;
            if (scaling == "defense") return role == "main" ? 2000 : role == "offfield" ? 1800 : role == "support" ? 1600 : 2200;
            return role == "main" ? 1800 : role == "offfield" ? 1600 : role == "support" ? 1400 : 1200;
        }

        private static int CompareRotationOrder(CombatCharacterInput left, CombatCharacterInput right)
        {
            int difference = RoleOrder(left.Role).CompareTo(RoleOrder(right.Role));
            if (difference != 0) return difference;
            difference = right.EffectDuration.CompareTo(left.EffectDuration);
            return difference != 0 ? difference : String.Compare(left.Name, right.Name, StringComparison.CurrentCulture);
        }

        private static int RoleOrder(string role)
        {
            return role == "sustain" ? 0 : role == "offfield" ? 1 : role == "support" ? 2 : 3;
        }

        internal static string RoleLabel(string role)
        {
            return role == "main" ? "主C" : role == "offfield" ? "后台副C" : role == "support" ? "增伤辅助" : "生存位";
        }

        internal static string ScalingLabel(string scaling)
        {
            return scaling == "health" ? "生命值" : scaling == "defense" ? "防御力" : "攻击力";
        }

        private static double RoundUp(double value, double step) { return Math.Ceiling(value / step) * step; }
        private static bool OneOf(string value, string[] choices)
        {
            foreach (string choice in choices) if (value == choice) return true;
            return false;
        }
        private static void Range(double value, double minimum, double maximum, string label)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentException(label + "超出可用范围。");
        }

        private static void Validate(CombatPlanInput input)
        {
            if (input == null || input.Characters == null) throw new ArgumentException("配队数据为空。");
            if (input.CharacterLevel < 1 || input.CharacterLevel > 100 || input.EnemyLevel < 1 || input.EnemyLevel > 200)
                throw new ArgumentException("角色或敌人等级超出范围。");
            Range(input.EnemyResistance, -100, 1000, "敌人抗性");
            Range(input.DefenseReduction, 0, 90, "防御降低");
            Range(input.ReactionMultiplier, 1, 2, "反应倍率");
            Range(input.ReactionCoverage, 0, 100, "反应覆盖");
            foreach (CombatCharacterInput character in input.Characters) {
                if (character == null || String.IsNullOrWhiteSpace(character.Name)) continue;
                character.Name = character.Name.Trim();
                if (character.Name.Length > 20) throw new ArgumentException("角色名不能超过20个字。");
                if (!OneOf(character.Role, Roles) || !OneOf(character.Scaling, Scalings)) throw new ArgumentException(character.Name + "的角色类型无效。");
                Range(character.CoreStat, 1, 100000, character.Name + "的主要属性");
                Range(character.CritRate, 0, 100, character.Name + "的暴击率");
                Range(character.CritDamage, 0, 500, character.Name + "的暴击伤害");
                Range(character.DamageBonus, -90, 500, character.Name + "的伤害加成");
                Range(character.EnergyRecharge, 100, 400, character.Name + "的充能");
                Range(character.SkillMultiplier, 0, 10000, character.Name + "的E倍率");
                Range(character.BurstMultiplier, 0, 20000, character.Name + "的Q倍率");
                Range(character.ComboMultiplier, 0, 30000, character.Name + "的连段倍率");
                Range(character.BurstCost, 0, 100, character.Name + "的大招能量");
                Range(character.SkillCooldown, 0, 60, character.Name + "的E冷却");
                Range(character.BurstCooldown, 0, 60, character.Name + "的Q冷却");
                Range(character.FieldSeconds, 0, 30, character.Name + "的驻场时间");
                Range(character.TeamBuffPercent, 0, 200, character.Name + "的全队增伤");
                Range(character.EffectDuration, 0, 60, character.Name + "的效果时间");
            }
        }
    }

    internal static class CombatPlannerStorage
    {
        internal static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet", "combat-plan.json"); } }

        internal static CombatPlanInput Load() { return Load(SettingsPath); }
        internal static CombatPlanInput Load(string path)
        {
            try {
                if (!File.Exists(path) || new FileInfo(path).Length > 256 * 1024) return CombatPlanInput.CreateExample();
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 256 * 1024, RecursionLimit = 20 };
                CombatPlanInput value = serializer.Deserialize<CombatPlanInput>(File.ReadAllText(path));
                CombatPlanner.Calculate(value);
                while (value.Characters.Count < 4) value.Characters.Add(CombatPlanInput.CreateExample().Characters[value.Characters.Count]);
                return value;
            } catch { return CombatPlanInput.CreateExample(); }
        }

        internal static void Save(CombatPlanInput value) { Save(SettingsPath, value); }
        internal static void Save(string path, CombatPlanInput value)
        {
            CombatPlanner.Calculate(value);
            string directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 256 * 1024, RecursionLimit = 20 };
            File.WriteAllText(path, serializer.Serialize(value));
        }
    }

    internal sealed class CombatCharacterEditor
    {
        private readonly TextBox name, element, core, critRate, critDamage, damageBonus, energyRecharge;
        private readonly TextBox skillMultiplier, burstMultiplier, comboMultiplier, burstCost;
        private readonly TextBox skillCooldown, burstCooldown, fieldSeconds, teamBuff, effectDuration;
        private readonly ComboBox role, scaling;
        internal readonly TabItem Tab;

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
        }

        internal void Load(CombatCharacterInput value)
        {
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
            if (!Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException(label + "不是有效数字。");
            return value;
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
            TextBlock subtitle = Text("手填配队和面板  /  本地粗算  /  不读游戏数据", 12, false, true); subtitle.Margin = new Thickness(0, 5, 0, 0); title.Children.Add(subtitle);
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
            StackPanel form = new StackPanel(); formScroll.Content = form; content.Children.Add(formScroll);
            TextBlock safe = Text("安全边界：不读取内存，不改文件，不自动按键。", 12, true, false);
            form.Children.Add(Card(safe));
            form.Children.Add(SectionLabel("敌人与反应"));
            UniformGrid enemy = new UniformGrid { Columns = 2 };
            characterLevel = Input(); enemy.Children.Add(Field("角色等级", characterLevel));
            enemyLevel = Input(); enemy.Children.Add(Field("敌人等级", enemyLevel));
            enemyResistance = Input(); enemy.Children.Add(Field("敌人抗性 %", enemyResistance));
            defenseReduction = Input(); enemy.Children.Add(Field("防御降低 %", defenseReduction));
            reaction = Choice(new[] { "不算增幅反应", "1.5倍反应", "2.0倍反应" }, new[] { "1", "1.5", "2" }); enemy.Children.Add(Field("增幅反应", reaction));
            reactionCoverage = Input(); enemy.Children.Add(Field("反应覆盖 %", reactionCoverage));
            form.Children.Add(enemy);

            form.Children.Add(SectionLabel("当前配队"));
            TabControl tabs = new TabControl { Height = 445, Margin = new Thickness(4, 0, 4, 10) };
            for (int i = 1; i <= 4; i++) { CombatCharacterEditor editor = new CombatCharacterEditor(i, this); editors.Add(editor); tabs.Items.Add(editor.Tab); }
            form.Children.Add(tabs);
            Button calculate = ActionButton("生成循环与目标", Generate); calculate.HorizontalAlignment = HorizontalAlignment.Stretch; calculate.Margin = new Thickness(4, 2, 4, 8); calculate.Padding = new Thickness(15, 11, 15, 11); form.Children.Add(calculate);

            ScrollViewer resultScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(2, 0, 8, 0) };
            Grid.SetColumn(resultScroll, 2); content.Children.Add(resultScroll); resultBody = new StackPanel(); resultScroll.Content = resultBody;
            status = Text("计算只保存在本机 %LocalAppData%\\KoloPet。", 11, false, true); status.Margin = new Thickness(0, 12, 0, 0); Grid.SetRow(status, 2); root.Children.Add(status);

            CombatPlanInput initial = persist ? CombatPlannerStorage.Load() : CombatPlanInput.CreateExample();
            Load(initial); Generate(false);
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
<Style TargetType='{x:Type ComboBox}'><Setter Property='Foreground' Value='{x:Static SystemColors.WindowTextBrush}'/><Setter Property='Background' Value='{x:Static SystemColors.WindowBrush}'/><Setter Property='BorderBrush' Value='{DynamicResource PlannerBorder}'/><Setter Property='Padding' Value='6,5'/><Setter Property='Margin' Value='0,4,0,0'/></Style>
<Style TargetType='{x:Type TabControl}'><Setter Property='Foreground' Value='{DynamicResource PlannerText}'/><Setter Property='Background' Value='{DynamicResource PlannerCard}'/><Setter Property='BorderBrush' Value='{DynamicResource PlannerBorder}'/></Style>
<Style TargetType='{x:Type TabItem}'><Setter Property='Foreground' Value='{DynamicResource PlannerText}'/><Setter Property='Background' Value='{DynamicResource PlannerCard}'/><Setter Property='BorderBrush' Value='{DynamicResource PlannerBorder}'/><Setter Property='Padding' Value='12,7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabItem}'><Border x:Name='Tab' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1,1,1,0' CornerRadius='6,6,0,0' Padding='{TemplateBinding Padding}'><ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='Tab' Property='Background' Value='{DynamicResource PlannerHover}'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Tab' Property='Background' Value='{DynamicResource PlannerHover}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ScrollBar}'><Setter Property='Width' Value='11'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='{DynamicResource PlannerBorder}' CornerRadius='4' Margin='3,0'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
            Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(xaml));
        }

        private void Load(CombatPlanInput value)
        {
            characterLevel.Text = value.CharacterLevel.ToString(CultureInfo.InvariantCulture);
            enemyLevel.Text = value.EnemyLevel.ToString(CultureInfo.InvariantCulture);
            enemyResistance.Text = value.EnemyResistance.ToString("0.##", CultureInfo.InvariantCulture);
            defenseReduction.Text = value.DefenseReduction.ToString("0.##", CultureInfo.InvariantCulture);
            reactionCoverage.Text = value.ReactionCoverage.ToString("0.##", CultureInfo.InvariantCulture);
            Select(reaction, value.ReactionMultiplier.ToString("0.##", CultureInfo.InvariantCulture));
            CombatPlanInput fallback = CombatPlanInput.CreateExample();
            for (int i = 0; i < editors.Count; i++) editors[i].Load(i < value.Characters.Count ? value.Characters[i] : fallback.Characters[i]);
        }

        private CombatPlanInput ReadInput()
        {
            CombatPlanInput value = new CombatPlanInput {
                Version = 1,
                CharacterLevel = (int)Numeric(characterLevel, "角色等级"),
                EnemyLevel = (int)Numeric(enemyLevel, "敌人等级"),
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
            try {
                CombatPlanInput input = ReadInput();
                CombatPlanResult result = CombatPlanner.Calculate(input);
                Render(result);
                if (save && persist) CombatPlannerStorage.Save(input);
                status.Text = save && persist ? "已计算，并保存当前配队。" : "已按当前面板计算。";
            } catch (Exception error) {
                resultBody.Children.Clear();
                TextBlock message = Text("无法计算\n" + error.Message, 15, true, false); resultBody.Children.Add(Card(message));
                status.Text = "请修正红线外的数字。";
            }
        }

        internal void Render(CombatPlanResult value)
        {
            resultBody.Children.Clear();
            resultBody.Children.Add(SectionLabel("建议循环"));
            TextBlock steps = Text(String.Join("\n", value.Steps.ToArray()), 14, true, false); steps.LineHeight = 25; resultBody.Children.Add(Card(steps));
            resultBody.Children.Add(SectionLabel("伤害粗算"));
            UniformGrid summary = new UniformGrid { Columns = 2 };
            summary.Children.Add(Metric("完整循环", value.RotationSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " 秒"));
            summary.Children.Add(Metric("当前期望", Number(value.CurrentDamage)));
            summary.Children.Add(Metric("当前每秒", Number(value.CurrentDps)));
            summary.Children.Add(Metric("达标后期望", Number(value.TargetDamage)));
            resultBody.Children.Add(summary);
            resultBody.Children.Add(SectionLabel("最低循环目标"));
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
            TextBlock warnings = Text(String.Join("\n", value.Warnings.ToArray()) + "\n最低线是可用目标，不是硬要求。实际伤害以游戏内为准。", 11.5, false, true);
            resultBody.Children.Add(Card(warnings));
        }

        private void ResetExample()
        {
            if (MessageBox.Show("恢复示例会盖掉当前输入。", "战斗循环助手", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            Load(CombatPlanInput.CreateExample()); Generate();
        }

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
                throw new FormatException(label + "不是有效数字。");
            return value;
        }
        private static double NumericTag(ComboBox box)
        {
            ComboBoxItem item = box.SelectedItem as ComboBoxItem;
            double value;
            if (item == null || !Double.TryParse(Convert.ToString(item.Tag, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException("反应倍率无效。");
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
    }
}
