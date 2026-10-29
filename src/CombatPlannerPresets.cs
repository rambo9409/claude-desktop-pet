using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace ClaudePet
{
    // Recommended teams ship as the embedded resource "combat-teams.json"
    // (source: assets/combat-teams.json). A newer copy at
    // %LocalAppData%\KoloPet\combat-teams.json overrides it without a rebuild,
    // as long as it parses, validates and has an "updated" date that is not
    // older than the embedded one.
    internal sealed class CombatTeamSource
    {
        public string Title { get; set; }
        public string Url { get; set; }
    }

    internal sealed class CombatTeamMember
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Element { get; set; }
        public string Scaling { get; set; }
        public string Note { get; set; }
    }

    internal sealed class CombatTeamPreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Archetype { get; set; }
        public string Tier { get; set; }
        public string Budget { get; set; }
        public List<CombatTeamMember> Members { get; set; }
        public List<string> Rotation { get; set; }
        public string Energy { get; set; }
        public string Tips { get; set; }
        public string ModelNote { get; set; }
        public bool SupportsEstimate { get; set; }
        public string VersionScope { get; set; }
        public string VerifiedAt { get; set; }
        public string RotationBasis { get; set; }
        public List<CombatTeamSource> Sources { get; set; }
    }

    internal sealed class CombatTeamCatalog
    {
        public int Version { get; set; }
        public string GameVersion { get; set; }
        public string Updated { get; set; }
        public string Notice { get; set; }
        public List<CombatTeamPreset> Teams { get; set; }

        internal string Origin = "内置";

        internal CombatTeamPreset Find(string id)
        {
            if (String.IsNullOrEmpty(id) || Teams == null) return null;
            foreach (CombatTeamPreset team in Teams) if (team.Id == id) return team;
            return null;
        }
    }

    internal static class CombatTeamPresets
    {
        internal const string ResourceName = "combat-teams.json";
        private const int MaximumBytes = 512 * 1024;
        private static CombatTeamCatalog cached;

        internal static string OverridePath { get { return Path.Combine(PetStorage.RootDirectory, "combat-teams.json"); } }

        internal static CombatTeamCatalog Current
        {
            get { if (cached == null) cached = Load(OverridePath); return cached; }
        }

        internal static CombatTeamCatalog Reload()
        {
            cached = Load(OverridePath);
            return cached;
        }

        internal static CombatTeamCatalog Import(string sourcePath)
        {
            CombatTeamCatalog imported = Import(sourcePath, OverridePath);
            cached = imported;
            return imported;
        }

        // Destination overload keeps tests and maintenance tools isolated from
        // the user's saved catalog. Only the real override refreshes the cache.
        internal static CombatTeamCatalog Import(string sourcePath, string destinationPath)
        {
            if (String.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) throw new FileNotFoundException("找不到配队资料文件。", sourcePath);
            if (String.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("配队资料保存路径为空。", "destinationPath");
            if (new FileInfo(sourcePath).Length > MaximumBytes) throw new InvalidDataException("配队资料超过512 KB。");
            string json = File.ReadAllText(sourcePath);
            CombatTeamCatalog candidate = Parse(json);
            CombatTeamCatalog current = Load(destinationPath);
            if (String.CompareOrdinal(candidate.Updated, current.Updated ?? "") < 0)
                throw new InvalidDataException("导入资料的日期早于当前资料（" + current.Updated + "），已保留当前资料。");
            if (File.Exists(destinationPath))
            {
                string previous = null;
                bool damaged = false;
                try
                {
                    if (new FileInfo(destinationPath).Length > MaximumBytes) throw new InvalidDataException();
                    previous = File.ReadAllText(destinationPath);
                    Parse(previous);
                }
                catch { damaged = true; }
                if (damaged)
                {
                    if (PetStorage.Quarantine(destinationPath, "combat-teams-before-import") == null)
                        throw new IOException("当前配队资料无法读取且备份失败，已停止导入以保护原文件。");
                }
                else
                    PetStorage.WriteAllTextAtomic(destinationPath + ".previous.json", previous);
            }
            // Only a fully parsed and date-checked document can replace the
            // active file. A write failure leaves the original and cache intact.
            PetStorage.WriteAllTextAtomic(destinationPath, json);
            candidate.Origin = "本地更新";
            if (String.Equals(Path.GetFullPath(destinationPath), Path.GetFullPath(OverridePath), StringComparison.OrdinalIgnoreCase)) cached = candidate;
            return candidate;
        }

        internal static CombatTeamCatalog Load(string overridePath)
        {
            CombatTeamCatalog embedded = null;
            try { embedded = Parse(ReadEmbedded()); }
            catch (Exception error) { PetStorage.Log("combat-teams-embedded", error); }
            string text = PetStorage.TryReadAllText(overridePath, MaximumBytes, "combat-teams-override");
            if (text != null) {
                try {
                    CombatTeamCatalog local = Parse(text);
                    if (embedded == null || String.CompareOrdinal(local.Updated ?? "", embedded.Updated ?? "") >= 0) {
                        local.Origin = "本地更新";
                        return local;
                    }
                } catch (Exception error) { PetStorage.Log("combat-teams-override", error); }
            }
            if (embedded != null) return embedded;
            return new CombatTeamCatalog { Version = 1, GameVersion = "", Updated = "", Notice = "推荐配队数据缺失。", Teams = new List<CombatTeamPreset>() };
        }

        internal static string ReadEmbedded()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)) {
                if (stream == null) throw new FileNotFoundException("Embedded resource missing: " + ResourceName);
                using (StreamReader reader = new StreamReader(stream, true)) return reader.ReadToEnd();
            }
        }

        // Throws InvalidDataException with the first problem found.
        internal static CombatTeamCatalog Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new InvalidDataException("配队资料为空或超过512 KB。");
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = MaximumBytes, RecursionLimit = 20 };
            CombatTeamCatalog catalog = serializer.Deserialize<CombatTeamCatalog>(json);
            if (catalog == null || catalog.Teams == null || catalog.Teams.Count == 0) throw new InvalidDataException("没有配队。");
            DateTime updated;
            if (catalog.Version != 2 || !DateTime.TryParseExact(catalog.Updated, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out updated)) throw new InvalidDataException("配队数据格式版本或核验日期无效。");
            if (String.IsNullOrWhiteSpace(catalog.GameVersion) || catalog.GameVersion.Length > 32 || String.IsNullOrWhiteSpace(catalog.Notice))
                throw new InvalidDataException("配队资料缺少游戏版本或适用说明。");
            if (catalog.Teams.Count > 500) throw new InvalidDataException("单份资料最多包含500套配队。");
            HashSet<string> ids = new HashSet<string>();
            foreach (CombatTeamPreset team in catalog.Teams) {
                if (team == null || String.IsNullOrWhiteSpace(team.Id) || !ids.Add(team.Id)) throw new InvalidDataException("配队 id 缺失或重复。");
                if (String.IsNullOrWhiteSpace(team.Name)) throw new InvalidDataException(team.Id + " 缺少名称。");
                if (team.Members == null || team.Members.Count != 4) throw new InvalidDataException(team.Id + " 成员数应为 4。");
                DateTime verified;
                if (!DateTime.TryParseExact(team.VerifiedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out verified) ||
                    String.IsNullOrWhiteSpace(team.VersionScope) || String.IsNullOrWhiteSpace(team.RotationBasis))
                    throw new InvalidDataException(team.Id + " 缺少核验日期、适用范围或手法依据。");
                HashSet<string> memberNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (CombatTeamMember member in team.Members) {
                    if (member == null || String.IsNullOrWhiteSpace(member.Name) || member.Name.Trim().Length > 20) throw new InvalidDataException(team.Id + " 成员名无效。");
                    if (!memberNames.Add(CombatRosterMatcher.Normalize(member.Name))) throw new InvalidDataException(team.Id + " 不能重复安排同一名角色。");
                    if (!CombatPlanner.IsRole(member.Role) || !CombatPlanner.IsScaling(member.Scaling)) throw new InvalidDataException(team.Id + " 的 " + member.Name + " 职责或基值无效。");
                }
                if (team.Rotation == null || team.Rotation.Count == 0) throw new InvalidDataException(team.Id + " 缺少手法。");
                foreach (string step in team.Rotation)
                    if (String.IsNullOrWhiteSpace(step) || step.Length > 2000) throw new InvalidDataException(team.Id + " 的手法步骤为空或过长。");
                if (team.Sources == null || team.Sources.Count == 0) throw new InvalidDataException(team.Id + " 缺少来源。");
                foreach (CombatTeamSource source in team.Sources)
                    if (source == null || String.IsNullOrWhiteSpace(source.Title) || !IsSafeLink(source.Url)) throw new InvalidDataException(team.Id + " 的来源需要名称和 https 链接。");
            }
            return catalog;
        }

        internal static bool IsSafeLink(string url)
        {
            Uri uri;
            return !String.IsNullOrWhiteSpace(url) && url.Length <= 2048 && Uri.TryCreate(url, UriKind.Absolute, out uri) &&
                uri.Scheme == Uri.UriSchemeHttps && String.IsNullOrEmpty(uri.UserInfo);
        }

        // Fills slots with the preset's names, roles, elements and scaling.
        // All panel values remain explicitly marked examples in the UI.
        // A preset is a rotation reference, not a character damage database.
        internal static CombatPlanInput Apply(CombatPlanInput current, CombatTeamPreset preset)
        {
            if (current == null || preset == null) throw new ArgumentNullException(current == null ? "current" : "preset");
            CombatPlanInput value = new CombatPlanInput {
                Version = CombatPlanInput.CurrentVersion, PresetId = preset.Id,
                CharacterLevel = current.CharacterLevel, EnemyLevel = current.EnemyLevel,
                EnemyResistance = current.EnemyResistance, DefenseReduction = current.DefenseReduction,
                ReactionMultiplier = current.ReactionMultiplier, ReactionCoverage = current.ReactionCoverage,
                Characters = new List<CombatCharacterInput>()
            };
            for (int i = 0; i < 4; i++) {
                if (i >= preset.Members.Count) { value.Characters.Add(CombatPlanInput.EmptySlot(i)); continue; }
                CombatTeamMember member = preset.Members[i];
                CombatCharacterInput slot = CombatPlanInput.TemplateForRole(member.Role).Clone();
                slot.Name = member.Name.Trim(); slot.Role = member.Role; slot.Element = member.Element ?? ""; slot.Scaling = member.Scaling;
                slot.CoreStat = member.Scaling == "health" ? 30000 : member.Scaling == "defense" ? 2000 : slot.CoreStat;
                value.Characters.Add(slot);
            }
            return value;
        }

        internal static string Label(CombatTeamPreset team)
        {
            return "[" + (team.Tier ?? "参考") + "] " + team.Name;
        }

        internal static string Header(CombatTeamCatalog catalog)
        {
            return "推荐配队 · V" + (catalog.GameVersion ?? "?") + " · 截至 " + (catalog.Updated ?? "?") + " · " + catalog.Origin
                + " · " + (catalog.Teams == null ? 0 : catalog.Teams.Count).ToString(CultureInfo.InvariantCulture) + " 套";
        }
    }
}
