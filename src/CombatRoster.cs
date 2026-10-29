using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace ClaudePet
{
    internal sealed class CombatRosterProfile
    {
        public CombatRosterProfile() { Version = 1; Owned = new List<string>(); }
        public int Version { get; set; }
        public bool Configured { get; set; }
        public List<string> Owned { get; set; }

        internal static CombatRosterProfile CreateEmpty()
        { return new CombatRosterProfile { Version = 1, Configured = false, Owned = new List<string>() }; }
    }

    internal sealed class CombatRosterLoad
    {
        internal CombatRosterProfile Profile;
        internal string Notice;
    }

    internal static class CombatRosterStorage
    {
        private const int MaximumBytes = 64 * 1024;
        internal static string SettingsPath { get { return Path.Combine(PetStorage.RootDirectory, "combat-roster-owned.json"); } }

        internal static CombatRosterLoad Load() { return Load(SettingsPath); }
        internal static CombatRosterLoad Load(string path)
        {
            CombatRosterLoad result = new CombatRosterLoad { Profile = CombatRosterProfile.CreateEmpty() };
            if (!File.Exists(path)) return result;
            try { result.Profile = Read(path); }
            catch (Exception error)
            {
                string backup = PetStorage.Quarantine(path, "combat-roster-load");
                result.Notice = "角色拥有表无法读取（" + error.Message + "）。" + (backup == null
                    ? "备份未成功；保存前必须先成功备份原文件。暂按未设置处理。"
                    : "原表已备份为 " + Path.GetFileName(backup) + "，暂按未设置处理。");
            }
            return result;
        }

        internal static void Save(CombatRosterProfile value) { Save(SettingsPath, value); }
        internal static void Save(string path, CombatRosterProfile value)
        {
            CombatRosterProfile copy = ValidateCopy(value);
            string json = Serializer().Serialize(copy);
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new InvalidDataException("角色拥有表超过64 KB，已保留原表。");
            if (File.Exists(path))
            {
                bool readable = true;
                try { Read(path); } catch { readable = false; }
                if (!readable && PetStorage.Quarantine(path, "combat-roster-before-save") == null)
                    throw new IOException("角色拥有表无法读取且备份失败，已停止保存以保护原文件。");
            }
            PetStorage.WriteAllTextAtomic(path, json);
        }

        private static JavaScriptSerializer Serializer()
        { return new JavaScriptSerializer { MaxJsonLength = MaximumBytes, RecursionLimit = 10 }; }

        private static CombatRosterProfile Read(string path)
        {
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("角色拥有表超过64 KB。");
            return ValidateCopy(Serializer().Deserialize<CombatRosterProfile>(File.ReadAllText(path, Encoding.UTF8)));
        }

        private static CombatRosterProfile ValidateCopy(CombatRosterProfile value)
        {
            if (value == null || value.Version != 1 || value.Owned == null)
                throw new InvalidDataException("角色拥有表格式版本或角色列表无效。");
            if (value.Owned.Count > 512) throw new InvalidDataException("角色拥有表最多保存512个角色条目。");
            CombatRosterProfile copy = new CombatRosterProfile { Version = 1, Configured = value.Configured, Owned = new List<string>() };
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in value.Owned)
            {
                if (String.IsNullOrWhiteSpace(name) || name.Trim().Length > 64)
                    throw new InvalidDataException("角色名字不能为空或超过64个字符。");
                foreach (char c in name) if (Char.IsControl(c)) throw new InvalidDataException("角色名字不能包含换行或控制字符。");
                if (seen.Add(CombatRosterMatcher.Normalize(name))) copy.Owned.Add(name.Trim());
            }
            return copy;
        }
    }

    internal sealed class CombatRosterCharacter
    {
        public string Name { get; set; }
        public List<string> Aliases { get; set; }
    }

    internal sealed class CombatRosterCatalog
    {
        public int Version { get; set; }
        public string Updated { get; set; }
        public string Notice { get; set; }
        public List<CombatTeamSource> Sources { get; set; }
        public List<CombatRosterCharacter> Characters { get; set; }
    }

    internal static class CombatRosterMatcher
    {
        private static readonly Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> displayNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<string> baseNames = new List<string>();
        internal static readonly string BaseNotice;

        static CombatRosterMatcher()
        {
            // This alias is also supplied directly by the user's character name.
            aliases[Key("Vesna")] = "vesna";
            aliases[Key("薇斯娜")] = "vesna";
            displayNames["vesna"] = "薇斯娜";
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("combat-roster.json"))
                {
                    if (stream == null) throw new FileNotFoundException("基础角色名单资源缺失，仍可使用配队目录和自定义角色。");
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 256 * 1024, RecursionLimit = 15 };
                        CombatRosterCatalog catalog = serializer.Deserialize<CombatRosterCatalog>(reader.ReadToEnd());
                        if (catalog == null || catalog.Version != 1 || catalog.Characters == null) throw new InvalidDataException("基础角色名单格式无效。");
                        BaseNotice = catalog.Notice ?? "";
                        foreach (CombatRosterCharacter character in catalog.Characters)
                        {
                            if (character == null || String.IsNullOrWhiteSpace(character.Name) || character.Name.Length > 64) continue;
                            string key = Key(character.Name);
                            if (key == "薇斯娜") key = "vesna";
                            aliases[Key(character.Name)] = key;
                            if (character.Aliases != null)
                                foreach (string alias in character.Aliases)
                                    if (!String.IsNullOrWhiteSpace(alias)) aliases[Key(alias)] = key;
                            displayNames[key] = key == "vesna" ? "薇斯娜" : character.Name.Trim();
                            baseNames.Add(displayNames[key]);
                        }
                    }
                }
            }
            catch (Exception error) { PetStorage.Log("combat-roster-catalog", error); BaseNotice = "基础名单不可用；以下列出配队目录与已保存的角色。"; }
            // Preserve the explicit alias even if an imported base list changes its spelling.
            aliases[Key("Vesna")] = "vesna"; aliases[Key("薇斯娜")] = "vesna"; displayNames["vesna"] = "薇斯娜";
        }

        private static string Key(string name)
        {
            StringBuilder value = new StringBuilder();
            foreach (char c in (name ?? "").Normalize(NormalizationForm.FormKC))
                if (!Char.IsWhiteSpace(c)) value.Append(Char.ToLowerInvariant(c));
            return value.ToString();
        }

        internal static string Normalize(string name)
        {
            string key = Key(name), canonical;
            return aliases.TryGetValue(key, out canonical) ? canonical : key;
        }

        internal static string DisplayName(string name)
        {
            string display;
            return displayNames.TryGetValue(Normalize(name), out display) ? display : (name ?? "").Trim();
        }

        internal static bool IsOwned(CombatRosterProfile profile, string name)
        {
            if (profile == null || !profile.Configured || profile.Owned == null || String.IsNullOrWhiteSpace(name)) return false;
            string key = Normalize(name);
            foreach (string owned in profile.Owned) if (Normalize(owned) == key) return true;
            return false;
        }

        internal static List<string> KnownCharacters(CombatTeamCatalog catalog, CombatRosterProfile profile)
        {
            List<string> names = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            Action<string> add = delegate(string name)
            {
                if (!String.IsNullOrWhiteSpace(name) && seen.Add(Normalize(name))) names.Add(DisplayName(name));
            };
            foreach (string name in baseNames) add(name);
            if (catalog != null && catalog.Teams != null)
                foreach (CombatTeamPreset team in catalog.Teams)
                    if (team != null && team.Members != null)
                        foreach (CombatTeamMember member in team.Members) if (member != null) add(member.Name);
            if (profile != null && profile.Owned != null) foreach (string name in profile.Owned) add(name);
            names.Sort(StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true));
            return names;
        }

        internal static List<string> Missing(CombatTeamPreset team, CombatRosterProfile profile)
        {
            List<string> missing = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (team != null && team.Members != null)
                foreach (CombatTeamMember member in team.Members)
                    if (member != null && !IsOwned(profile, member.Name) && seen.Add(Normalize(member.Name))) missing.Add(DisplayName(member.Name));
            return missing;
        }

        internal static List<CombatTeamPreset> Rank(CombatTeamCatalog catalog, CombatRosterProfile profile, bool onlyOwned)
        {
            List<CombatTeamPreset> result = new List<CombatTeamPreset>();
            if (catalog == null || catalog.Teams == null) return result;
            if (profile == null || !profile.Configured)
            { result.AddRange(catalog.Teams); return result; }
            // Bucket insertion preserves catalog order for ties. This is an
            // ownership match score, never a fabricated combat-strength score.
            for (int count = 0; count <= 4; count++)
            {
                if (onlyOwned && count > 0) break;
                foreach (CombatTeamPreset team in catalog.Teams)
                    if (team != null && Missing(team, profile).Count == count) result.Add(team);
            }
            return result;
        }
    }
}
