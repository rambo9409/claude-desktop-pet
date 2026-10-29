using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace ClaudePet
{
    // Plain data. Public setters are required by JavaScriptSerializer.
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

        internal bool IsFilled { get { return !String.IsNullOrWhiteSpace(Name); } }

        internal CombatCharacterInput Clone() { return (CombatCharacterInput)MemberwiseClone(); }
    }

    internal sealed class CombatPlanInput
    {
        public int Version { get; set; }
        public string PresetId { get; set; }
        public int CharacterLevel { get; set; }
        public int EnemyLevel { get; set; }
        public double EnemyResistance { get; set; }
        public double DefenseReduction { get; set; }
        public double ReactionMultiplier { get; set; }
        public double ReactionCoverage { get; set; }
        public List<CombatCharacterInput> Characters { get; set; }

        internal const int CurrentVersion = 2;

        internal static CombatPlanInput CreateExample()
        {
            return new CombatPlanInput {
                Version = CurrentVersion,
                PresetId = "",
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

        // Stat template used when a preset fills a slot: numbers come from the
        // example character with the same role, so the user only edits deltas.
        internal static CombatCharacterInput TemplateForRole(string role)
        {
            foreach (CombatCharacterInput character in CreateExample().Characters)
                if (character.Role == role) return character;
            return CreateExample().Characters[0];
        }

        internal static CombatCharacterInput EmptySlot(int index)
        {
            CombatCharacterInput value = CreateExample().Characters[Math.Max(0, Math.Min(3, index))];
            value.Name = "";
            return value;
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

    // Slot 0 is the enemy section; slots 1-4 are character tabs. Field is the
    // CombatPlanInput / CombatCharacterInput property name, so the UI can
    // highlight the exact input box.
    internal sealed class CombatFieldError
    {
        internal readonly int Slot;
        internal readonly string Field;
        internal readonly string Message;
        internal CombatFieldError(int slot, string field, string message) { Slot = slot; Field = field; Message = message; }
    }

    internal sealed class CombatValidationException : ArgumentException
    {
        internal readonly List<CombatFieldError> Errors;
        internal CombatValidationException(List<CombatFieldError> errors) : base(Summarize(errors)) { Errors = errors; }
        private static string Summarize(List<CombatFieldError> errors)
        {
            List<string> lines = new List<string>();
            foreach (CombatFieldError error in errors) lines.Add(error.Message);
            return String.Join("\n", lines.ToArray());
        }
    }

    internal sealed class CombatCharacterResult
    {
        internal string Name;
        internal string Role;
        internal string Scaling;
        internal int SkillCasts = 1;
        internal double BuffPercent;
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
        private const double SkillSeconds = 1.0, BurstSeconds = 1.6, SwapSeconds = 0.6;

        internal static CombatPlanResult Calculate(CombatPlanInput input)
        {
            List<CombatFieldError> errors = Validate(input);
            if (errors.Count > 0) throw new CombatValidationException(errors);
            List<CombatCharacterInput> characters = new List<CombatCharacterInput>();
            foreach (CombatCharacterInput character in input.Characters)
                if (character != null && character.IsFilled) characters.Add(character);

            List<CombatCharacterInput> order = new List<CombatCharacterInput>(characters);
            order.Sort(CompareRotationOrder);
            CombatPlanResult result = new CombatPlanResult();
            int mainCount = 0;
            double mainField = 0;
            foreach (CombatCharacterInput character in characters)
                if (character.Role == "main") { mainCount++; mainField = Math.Max(mainField, character.FieldSeconds); }
            if (mainCount == 0) result.Warnings.Add("没有主C。循环仍可生成，但缺少明确驻场角色。");
            if (mainCount > 1) result.Warnings.Add("有多名主C。请减少驻场时间，避免互相抢时间。");

            // First pass: one cast of every skill, plus swaps and field time.
            double actionSeconds = 0, longestCooldown = 0;
            Dictionary<CombatCharacterInput, double> firstSkill = new Dictionary<CombatCharacterInput, double>();
            for (int i = 0; i < order.Count; i++) {
                CombatCharacterInput character = order[i];
                if (HasSkill(character)) { firstSkill[character] = actionSeconds; actionSeconds += SkillSeconds; longestCooldown = Math.Max(longestCooldown, character.SkillCooldown); }
                if (HasBurst(character)) { actionSeconds += BurstSeconds; longestCooldown = Math.Max(longestCooldown, character.BurstCooldown); }
                if (HasField(character)) actionSeconds += character.FieldSeconds;
                if (i + 1 < order.Count) actionSeconds += SwapSeconds;
            }
            result.RotationSeconds = Math.Max(actionSeconds, longestCooldown);

            // Second pass: short-cooldown skills are recast inside idle time.
            // On-field carries recast in place; others pay a swap in and out.
            Dictionary<CombatCharacterInput, int> casts = new Dictionary<CombatCharacterInput, int>();
            foreach (CombatCharacterInput character in characters) casts[character] = 1;
            double cursor = actionSeconds;
            Dictionary<CombatCharacterInput, double> lastSkill = new Dictionary<CombatCharacterInput, double>(firstSkill);
            List<string> recasts = new List<string>();
            // Place extra skills on the remaining timeline, after the opening
            // sequence, rather than counting casts that overlap field time.
            while (true) {
                CombatCharacterInput selected = null;
                double selectedStart = 0, selectedEnd = Double.MaxValue;
                foreach (CombatCharacterInput character in order) {
                    if (!HasSkill(character) || character.SkillCooldown <= 0 || character.SkillMultiplier <= 0) continue;
                    double swap = character == order[order.Count - 1] ? 0 : SwapSeconds;
                    double start = Math.Max(cursor + swap, lastSkill[character] + character.SkillCooldown);
                    double end = start + SkillSeconds + swap;
                    if (end <= result.RotationSeconds + 0.001 && end < selectedEnd) {
                        selected = character; selectedStart = start; selectedEnd = end;
                    }
                }
                if (selected == null) break;
                casts[selected]++; lastSkill[selected] = selectedStart; cursor = selectedEnd;
                recasts.Add("约 " + Seconds(selectedStart) + "秒：切回" + selected.Name.Trim() + "补E，再回驻场角色。");
            }
            double idle = result.RotationSeconds - cursor;

            for (int i = 0; i < order.Count; i++) {
                CombatCharacterInput character = order[i];
                List<string> actions = new List<string>();
                if (HasSkill(character)) actions.Add("E");
                if (HasBurst(character)) actions.Add("Q");
                if (HasField(character)) actions.Add("驻场连段 " + Seconds(character.FieldSeconds) + "秒");
                if (actions.Count == 0) actions.Add("切入触发效果");
                result.Steps.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + character.Name.Trim() + "：" + String.Join(" → ", actions.ToArray()));
            }
            result.Steps.AddRange(recasts);
            if (idle >= 0.5)
                result.Steps.Add("补位：普攻或走位 " + Seconds(idle) + "秒，等待技能。");

            foreach (CombatCharacterInput character in characters) {
                if (character.TeamBuffPercent > 0 && character.EffectDuration > 0 && mainField > character.EffectDuration)
                    result.Warnings.Add(character.Name.Trim() + "的增益短于主C驻场，只按覆盖比例计算。请在主C前释放，或缩短连段。");
                CombatCharacterResult evaluation = Evaluate(input, characters, character, casts[character], result.RotationSeconds);
                result.Characters.Add(evaluation);
                result.CurrentDamage += evaluation.CurrentDamage;
                result.TargetDamage += evaluation.TargetDamage;
            }
            result.CurrentDps = result.CurrentDamage / Math.Max(1, result.RotationSeconds);
            result.TargetDps = result.TargetDamage / Math.Max(1, result.RotationSeconds);
            result.Warnings.Add("伤害是通用粗算：主要属性 × 倍率 × 增伤 × 期望暴击 × 防御 × 抗性 × 增幅反应。");
            result.Warnings.Add("属性与充能是按职责设置的通用参考线，不是具体角色毕业标准；重复E仅安排在循环余量内，增益覆盖按时长比例估算，未模拟逐击附着与快照。");
            result.Warnings.Add("剧变、月曜、星辉等反应，元素精通、命座和独立乘区不在公式内，需另行修正。");
            return result;
        }

        private static bool HasSkill(CombatCharacterInput c) { return c.SkillMultiplier > 0 || c.SkillCooldown > 0; }
        private static bool HasBurst(CombatCharacterInput c) { return c.BurstMultiplier > 0 && c.BurstCost > 0; }
        private static bool HasField(CombatCharacterInput c) { return c.FieldSeconds > 0 && (c.Role == "main" || c.ComboMultiplier > 0); }

        // Seconds during which a character's damage lands. Used to scale how
        // much of an ally's timed buff actually covers that damage.
        internal static double DamageWindow(CombatCharacterInput character, double rotationSeconds)
        {
            double actions = (HasSkill(character) ? SkillSeconds : 0) + (HasBurst(character) ? BurstSeconds : 0);
            if (HasField(character)) return Math.Max(1, character.FieldSeconds + actions);
            return Math.Max(1, Math.Max(actions, Math.Min(rotationSeconds, character.EffectDuration)));
        }

        // Team buffs include the buffer's own damage ("全队"). A buff with no
        // duration is treated as permanent; otherwise it covers at most its
        // duration out of the target's damage window.
        internal static double TeamBuffFor(List<CombatCharacterInput> team, CombatCharacterInput target, double rotationSeconds)
        {
            double window = DamageWindow(target, rotationSeconds), total = 0;
            foreach (CombatCharacterInput ally in team) {
                if (ally.TeamBuffPercent <= 0) continue;
                double coverage = ally.EffectDuration <= 0 ? 1 : Math.Min(1, ally.EffectDuration / window);
                total += ally.TeamBuffPercent * coverage;
            }
            return Math.Min(200, total);
        }

        private static CombatCharacterResult Evaluate(CombatPlanInput input, List<CombatCharacterInput> team,
            CombatCharacterInput character, int skillCasts, double rotationSeconds)
        {
            double rawEnergy = EstimatedRawEnergy(character, team) * (1 + 0.6 * (skillCasts - 1));
            double requiredEnergy = HasBurst(character)
                ? RoundUp(Math.Max(100, Math.Min(400, character.BurstCost / Math.Max(1, rawEnergy) * 100)), 5) : 100;
            double burstCoverage = HasBurst(character)
                ? Math.Max(0, Math.Min(1, rawEnergy * character.EnergyRecharge / 100 / character.BurstCost)) : 1;
            double targetCore = MinimumCore(character.Scaling, character.Role);
            double targetRate = character.Role == "sustain" ? 0 : character.Role == "support" ? 50 : character.Role == "offfield" ? 60 : 65;
            double targetCritDamage = character.Role == "sustain" ? 0 : character.Role == "support" ? 100 : character.Role == "offfield" ? 120 : 130;
            double targetBonus = character.Role == "main" || character.Role == "offfield" ? 46.6 : 0;
            double teamBuff = TeamBuffFor(team, character, rotationSeconds);

            CombatCharacterResult result = new CombatCharacterResult {
                Name = character.Name.Trim(), Role = character.Role, Scaling = character.Scaling,
                SkillCasts = skillCasts, BuffPercent = teamBuff,
                RequiredEnergyRecharge = requiredEnergy, TargetCoreStat = targetCore, TargetCritRate = targetRate,
                TargetCritDamage = targetCritDamage, TargetDamageBonus = targetBonus, BurstCoverage = burstCoverage
            };
            result.CurrentDamage = ExpectedDamage(input, character, teamBuff, character.CoreStat, character.CritRate,
                character.CritDamage, character.DamageBonus, burstCoverage, skillCasts);
            result.TargetDamage = ExpectedDamage(input, character, teamBuff, Math.Max(character.CoreStat, targetCore),
                Math.Max(character.CritRate, targetRate), Math.Max(character.CritDamage, targetCritDamage),
                Math.Max(character.DamageBonus, targetBonus), 1, skillCasts);

            if (HasBurst(character) && character.EnergyRecharge + 0.1 < requiredEnergy)
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
                result.Advice.Add("已达到通用参考线。继续提升双暴或主要伤害属性。");
            return result;
        }

        internal static double ExpectedDamage(CombatPlanInput input, CombatCharacterInput character, double teamBuff,
            double coreStat, double critRate, double critDamage, double damageBonus, double burstCoverage)
        {
            return ExpectedDamage(input, character, teamBuff, coreStat, critRate, critDamage, damageBonus, burstCoverage, 1);
        }

        internal static double ExpectedDamage(CombatPlanInput input, CombatCharacterInput character, double teamBuff,
            double coreStat, double critRate, double critDamage, double damageBonus, double burstCoverage, int skillCasts)
        {
            double talent = character.SkillMultiplier * Math.Max(1, skillCasts) + character.ComboMultiplier +
                (HasBurst(character) ? character.BurstMultiplier * burstCoverage : 0);
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
            string element = (character.Element ?? "").Trim();
            if (element.Length > 0 && element != "无")
                foreach (CombatCharacterInput ally in team) if ((ally.Element ?? "").Trim() == element) sameElement++;
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

        private static int RoleOrder(string role) { return role == "sustain" ? 0 : role == "offfield" ? 1 : role == "support" ? 2 : 3; }

        internal static string RoleLabel(string role)
        {
            return role == "main" ? "主C" : role == "offfield" ? "后台副C" : role == "support" ? "增伤辅助" : "生存位";
        }

        internal static string ScalingLabel(string scaling) { return scaling == "health" ? "生命值" : scaling == "defense" ? "防御力" : "攻击力"; }
        internal static bool IsRole(string value) { return OneOf(value, Roles); }
        internal static bool IsScaling(string value) { return OneOf(value, Scalings); }

        private static string Seconds(double value) { return value.ToString("0.#", CultureInfo.InvariantCulture); }
        private static double RoundUp(double value, double step) { return Math.Ceiling(value / step) * step; }
        private static bool OneOf(string value, string[] choices)
        {
            foreach (string choice in choices) if (value == choice) return true;
            return false;
        }

        private static void Range(List<CombatFieldError> errors, int slot, string field, double value, double minimum, double maximum, string label)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value) || value < minimum || value > maximum)
                errors.Add(new CombatFieldError(slot, field, label + "应在 " + minimum.ToString("0.##", CultureInfo.InvariantCulture)
                    + " 到 " + maximum.ToString("0.##", CultureInfo.InvariantCulture) + " 之间。"));
        }

        // Returns every problem at once, without modifying the input.
        internal static List<CombatFieldError> Validate(CombatPlanInput input)
        {
            List<CombatFieldError> errors = new List<CombatFieldError>();
            if (input == null || input.Characters == null) { errors.Add(new CombatFieldError(0, "", "配队数据为空。")); return errors; }
            if (input.CharacterLevel < 1 || input.CharacterLevel > 100) errors.Add(new CombatFieldError(0, "CharacterLevel", "角色等级应为 1 到 100 之间的整数。"));
            if (input.EnemyLevel < 1 || input.EnemyLevel > 200) errors.Add(new CombatFieldError(0, "EnemyLevel", "敌人等级应为 1 到 200 之间的整数。"));
            Range(errors, 0, "EnemyResistance", input.EnemyResistance, -100, 1000, "敌人抗性");
            Range(errors, 0, "DefenseReduction", input.DefenseReduction, 0, 90, "防御降低");
            Range(errors, 0, "ReactionMultiplier", input.ReactionMultiplier, 1, 2, "反应倍率");
            Range(errors, 0, "ReactionCoverage", input.ReactionCoverage, 0, 100, "反应覆盖");
            int filled = 0;
            for (int i = 0; i < input.Characters.Count; i++) {
                CombatCharacterInput character = input.Characters[i];
                if (character == null || !character.IsFilled) continue;
                filled++;
                int slot = i + 1;
                string name = character.Name.Trim();
                string prefix = "角色" + slot.ToString(CultureInfo.InvariantCulture) + "「" + name + "」";
                if (name.Length > 20) errors.Add(new CombatFieldError(slot, "Name", prefix + "名字不能超过20个字。"));
                if (!IsRole(character.Role)) errors.Add(new CombatFieldError(slot, "Role", prefix + "的队内职责无效。"));
                if (!IsScaling(character.Scaling)) errors.Add(new CombatFieldError(slot, "Scaling", prefix + "的伤害基值无效。"));
                Range(errors, slot, "CoreStat", character.CoreStat, 1, 100000, prefix + "的主要属性");
                Range(errors, slot, "CritRate", character.CritRate, 0, 100, prefix + "的暴击率");
                Range(errors, slot, "CritDamage", character.CritDamage, 0, 500, prefix + "的暴击伤害");
                Range(errors, slot, "DamageBonus", character.DamageBonus, -90, 500, prefix + "的伤害加成");
                Range(errors, slot, "EnergyRecharge", character.EnergyRecharge, 100, 400, prefix + "的充能");
                Range(errors, slot, "SkillMultiplier", character.SkillMultiplier, 0, 10000, prefix + "的E倍率");
                Range(errors, slot, "BurstMultiplier", character.BurstMultiplier, 0, 20000, prefix + "的Q倍率");
                Range(errors, slot, "ComboMultiplier", character.ComboMultiplier, 0, 30000, prefix + "的连段倍率");
                Range(errors, slot, "BurstCost", character.BurstCost, 0, 100, prefix + "的大招能量");
                Range(errors, slot, "SkillCooldown", character.SkillCooldown, 0, 60, prefix + "的E冷却");
                Range(errors, slot, "BurstCooldown", character.BurstCooldown, 0, 60, prefix + "的Q冷却");
                Range(errors, slot, "FieldSeconds", character.FieldSeconds, 0, 30, prefix + "的驻场时间");
                Range(errors, slot, "TeamBuffPercent", character.TeamBuffPercent, 0, 200, prefix + "的全队增伤");
                Range(errors, slot, "EffectDuration", character.EffectDuration, 0, 60, prefix + "的效果时间");
            }
            if (filled == 0) errors.Add(new CombatFieldError(1, "Name", "请至少填写一名角色。"));
            if (filled > 4) errors.Add(new CombatFieldError(0, "", "一支队伍最多填写四名角色。"));
            return errors;
        }
    }

    internal sealed class CombatPlanLoad
    {
        internal CombatPlanInput Input;
        internal string Notice;
    }

    internal static class CombatPlannerStorage
    {
        private const int MaximumBytes = 256 * 1024;
        internal static string SettingsPath { get { return Path.Combine(PetStorage.RootDirectory, "combat-plan.json"); } }

        internal static CombatPlanLoad Load() { return Load(SettingsPath); }

        // A missing file gives the example. An unreadable file is preserved
        // as *.broken-<time>.json before the example is shown, so the next
        // save can never destroy the user's only copy.
        internal static CombatPlanLoad Load(string path)
        {
            CombatPlanLoad load = new CombatPlanLoad();
            if (!File.Exists(path)) { load.Input = CombatPlanInput.CreateExample(); return load; }
            try {
                if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("存档超过 256 KB。");
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = MaximumBytes, RecursionLimit = 20 };
                CombatPlanInput value = serializer.Deserialize<CombatPlanInput>(File.ReadAllText(path));
                if (value == null || value.Characters == null) throw new InvalidDataException("存档缺少配队。");
                if (value.Version > CombatPlanInput.CurrentVersion || value.Characters.Count > 4)
                    throw new InvalidDataException("存档版本或角色数量不受当前版本支持。");
                for (int i = 0; i < value.Characters.Count; i++)
                    if (value.Characters[i] == null) value.Characters[i] = CombatPlanInput.EmptySlot(i);
                while (value.Characters.Count < 4) value.Characters.Add(CombatPlanInput.EmptySlot(value.Characters.Count));
                if (value.PresetId == null) value.PresetId = "";
                List<CombatFieldError> errors = CombatPlanner.Validate(value);
                if (errors.Count > 0) throw new InvalidDataException(errors[0].Message);
                value.Version = CombatPlanInput.CurrentVersion;
                load.Input = value;
                return load;
            } catch (Exception error) {
                string backup = PetStorage.Quarantine(path, "combat-plan");
                load.Input = CombatPlanInput.CreateExample();
                load.Notice = "配队存档无法读取（" + error.Message + "）。" + (backup == null
                    ? "备份未成功，当前显示示例；保存前必须成功备份原文件。"
                    : "原文件已备份为 " + Path.GetFileName(backup) + "，当前显示示例。");
                return load;
            }
        }

        internal static void Save(CombatPlanInput value) { Save(SettingsPath, value); }
        internal static void Save(string path, CombatPlanInput value)
        {
            List<CombatFieldError> errors = CombatPlanner.Validate(value);
            if (errors.Count > 0) throw new CombatValidationException(errors);
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = MaximumBytes, RecursionLimit = 20 };
            // Retry preservation at save time too: a failed load-time backup
            // must never allow a later button press to destroy the only copy.
            if (File.Exists(path)) {
                bool unreadable = false;
                try {
                    if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException();
                    CombatPlanInput previous = serializer.Deserialize<CombatPlanInput>(File.ReadAllText(path));
                    unreadable = previous == null || previous.Version > CombatPlanInput.CurrentVersion ||
                        previous.Characters == null || previous.Characters.Count > 4 || CombatPlanner.Validate(previous).Count > 0;
                } catch { unreadable = true; }
                if (unreadable && PetStorage.Quarantine(path, "combat-plan-before-save") == null)
                    throw new IOException("原存档无法读取且备份失败，已停止保存以保护原文件。");
            }
            CombatPlanInput saved = serializer.Deserialize<CombatPlanInput>(serializer.Serialize(value));
            saved.Version = CombatPlanInput.CurrentVersion;
            PetStorage.WriteAllTextAtomic(path, serializer.Serialize(saved));
        }
    }
}
