using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudePet;

internal static class CombatRosterTests
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
    private static void Check(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
    private static void Reject(Action action, string message) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, message); }
    private static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Private).GetValue(owner); }
    private static object Call(object owner, string name, Type[] types, params object[] args)
    { return owner.GetType().GetMethod(name, Private, null, types, null).Invoke(owner, args); }
    private static CombatTeamCatalog Copy(CombatTeamCatalog value) { return Json.Deserialize<CombatTeamCatalog>(Json.Serialize(value)); }
    private static CombatRosterProfile Own(params string[] names)
    { return new CombatRosterProfile { Version = 1, Configured = true, Owned = new List<string>(names) }; }
    private static CombatRosterProfile OwnTeam(CombatTeamPreset team)
    {
        List<string> names = new List<string>(); foreach (CombatTeamMember member in team.Members) names.Add(member.Name);
        return Own(names.ToArray());
    }
    private static bool HasTeam(List<CombatTeamPreset> teams, string id)
    { foreach (CombatTeamPreset team in teams) if (team.Id == id) return true; return false; }

    private static void Matching(CombatTeamCatalog catalog)
    {
        CombatRosterProfile unset = CombatRosterProfile.CreateEmpty();
        Check(!unset.Configured, "Fresh profile must not invent ownership.");
        Check(CombatRosterMatcher.Rank(catalog, unset, true).Count == catalog.Teams.Count, "Unconfigured users must still be able to browse references.");
        Check(CombatRosterMatcher.Rank(catalog, Own(), true).Count == 0, "Explicitly owning no characters must not show playable teams.");
        CombatTeamPreset sample = catalog.Teams[0];
        CombatRosterProfile roster = OwnTeam(sample);
        Check(HasTeam(CombatRosterMatcher.Rank(catalog, roster, true), sample.Id), "A fully owned team was excluded.");
        Check(CombatRosterMatcher.Missing(sample, roster).Count == 0, "Owned team incorrectly reports missing members.");
        roster.Owned.RemoveAt(0);
        Check(!HasTeam(CombatRosterMatcher.Rank(catalog, roster, true), sample.Id), "Incomplete team marked playable.");
        Check(CombatRosterMatcher.Missing(sample, roster).Count == 1, "Missing-character count is not exact.");
        List<CombatTeamPreset> ranked = CombatRosterMatcher.Rank(catalog, roster, false);
        int previous = -1;
        foreach (CombatTeamPreset team in ranked) {
            int missing = CombatRosterMatcher.Missing(team, roster).Count;
            Check(missing >= previous, "All-team browsing is not ordered by missing members."); previous = missing;
        }
        Check(CombatRosterMatcher.Normalize(" Vesna ") == CombatRosterMatcher.Normalize("薇斯娜"), "English/Chinese alias breaks ownership.");
        Check(CombatRosterMatcher.IsOwned(Own("薇斯娜"), "Vesna"), "Alias cannot satisfy a preset member.");
        Check(CombatRosterMatcher.DisplayName("Vesna").Contains("薇斯娜"), "The user's character name is absent from the roster display.");
        Check(!CombatRosterMatcher.IsOwned(Own("旅行者（风）"), "旅行者（冰）"), "Traveler elements must not imply unlocked alternate elements.");
        roster.Owned.Add("未来测试角色");
        List<string> known = CombatRosterMatcher.KnownCharacters(catalog, roster);
        Check(known.Contains("未来测试角色"), "Unknown saved characters were discarded.");
        HashSet<string> unique = new HashSet<string>();
        foreach (string name in known) Check(unique.Add(CombatRosterMatcher.Normalize(name)), "Roster lists duplicate aliases.");
        CombatTeamCatalog future = Copy(catalog);
        future.Teams[0].Members[0].Name = "仅来自新资料的角色";
        Check(CombatRosterMatcher.KnownCharacters(future, roster).Contains("仅来自新资料的角色"), "New catalog characters require a code change.");
        Check(CombatRosterMatcher.Missing(future.Teams[0], roster).Contains("仅来自新资料的角色"), "New catalog characters are silently treated as owned.");
    }

    private static void Storage(string directory)
    {
        string path = Path.Combine(directory, "owned.json");
        CombatRosterProfile original = Own("薇斯娜", "Vesna", "自定义角色");
        CombatRosterStorage.Save(path, original);
        CombatRosterProfile loaded = CombatRosterStorage.Load(path).Profile;
        Check(loaded.Configured && CombatRosterMatcher.IsOwned(loaded, "Vesna") && CombatRosterMatcher.IsOwned(loaded, "自定义角色"), "Roster persistence loses selected characters.");
        Check(original.Owned.Count == 3, "Saving mutates the caller's selection.");
        CombatRosterStorage.Save(path, Own());
        Check(CombatRosterStorage.Load(path).Profile.Configured && CombatRosterStorage.Load(path).Profile.Owned.Count == 0, "Empty profile roundtrip becomes unconfigured.");
        File.WriteAllText(path, "broken roster");
        CombatRosterLoad damaged = CombatRosterStorage.Load(path);
        Check(!damaged.Profile.Configured && !String.IsNullOrEmpty(damaged.Notice), "Damaged roster did not provide recovery notice.");
        Check(File.ReadAllText(path) == "broken roster" && Directory.GetFiles(directory, "owned.json.broken-*").Length > 0, "Damaged roster was not retained and backed up.");
        CombatRosterStorage.Save(path, original);
        string before = File.ReadAllText(path);
        using (FileStream held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Reject(delegate { CombatRosterStorage.Save(path, Own("行秋")); }, "Locked roster was overwritten.");
        Check(File.ReadAllText(path) == before, "Failed save changed the owned roster.");
        CombatRosterProfile oversized = Own();
        for (int i = 0; i < 512; i++) oversized.Owned.Add(new String('角', 60) + i.ToString("D3"));
        Reject(delegate { CombatRosterStorage.Save(path, oversized); }, "Saving created a roster too large for the reader to reopen.");
        Check(File.ReadAllText(path) == before, "An oversized save changed the preceding valid roster.");
    }

    private static void Imports(CombatTeamCatalog catalog, string directory)
    {
        CombatTeamCatalog live = CombatTeamPresets.Current;
        string source = Path.Combine(directory, "teams-new.json"), target = Path.Combine(directory, "teams-installed.json");
        CombatTeamCatalog newer = Copy(catalog);
        newer.Updated = "2099-01-01"; // Synthetic future catalog; never shipped or installed in user settings.
        newer.Teams[0].Members[0].Name = "未来测试角色";
        newer.Teams[0].SupportsEstimate = false;
        File.WriteAllText(source, Json.Serialize(newer));
        CombatTeamCatalog imported = CombatTeamPresets.Import(source, target);
        Check(imported.Updated == newer.Updated && File.Exists(target), "Import did not install validated catalog.");
        Check(CombatTeamPresets.Load(target).Teams[0].Members[0].Name == "未来测试角色", "Imported future character was not used.");
        Check(!CombatTeamPresets.Load(target).Teams[0].SupportsEstimate, "Import loses unsupported-mechanic protection.");
        string good = File.ReadAllText(target);
        File.WriteAllText(source, "{ invalid");
        Reject(delegate { CombatTeamPresets.Import(source, target); }, "Invalid JSON imported.");
        Check(File.ReadAllText(target) == good, "Invalid JSON changed installed data.");
        newer.Updated = "2000-01-01"; File.WriteAllText(source, Json.Serialize(newer));
        Reject(delegate { CombatTeamPresets.Import(source, target); }, "Older data replaced newer installed catalog.");
        Check(File.ReadAllText(target) == good, "Rejected older import mutated installed data.");
        newer.Updated = "2099-01-02"; newer.Teams[0].Sources[0].Url = "javascript:alert(1)"; File.WriteAllText(source, Json.Serialize(newer));
        Reject(delegate { CombatTeamPresets.Import(source, target); }, "Unsafe source URL accepted.");
        newer = Copy(catalog); newer.Version = 99; newer.Updated = "2099-01-02"; File.WriteAllText(source, Json.Serialize(newer));
        Reject(delegate { CombatTeamPresets.Import(source, target); }, "Unknown catalog schema imported.");
        newer.Version = 2; newer.Teams[0].Members.RemoveAt(0); File.WriteAllText(source, Json.Serialize(newer));
        Reject(delegate { CombatTeamPresets.Import(source, target); }, "Incomplete team imported.");
        Check(File.ReadAllText(target) == good, "Rejected imports changed installed catalog.");
        newer = Copy(catalog); newer.Updated = "2099-01-03"; File.WriteAllText(source, Json.Serialize(newer));
        using (FileStream held = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
            Reject(delegate { CombatTeamPresets.Import(source, target); }, "Locked installed catalog was overwritten.");
        Check(File.ReadAllText(target) == good, "Failed replacement destroyed installed catalog.");
        File.WriteAllText(source, new String(' ', 600 * 1024));
        Reject(delegate { CombatTeamPresets.Import(source, target); }, "Oversized input was accepted.");
        newer = Copy(catalog); newer.Updated = "2099-01-04";
        newer.Teams[0].Members[0].Name = "Vesna"; newer.Teams[0].Members[1].Name = "薇斯娜";
        File.WriteAllText(source, Json.Serialize(newer));
        Reject(delegate { CombatTeamPresets.Import(source, target); }, "Aliases allowed the same character in two slots.");
        newer = Copy(catalog); newer.Updated = "2099-01-04"; File.WriteAllText(source, Json.Serialize(newer));
        CombatTeamPresets.Import(source, target);
        Check(File.ReadAllText(target + ".previous.json") == good, "Successful update lost the preceding valid catalog.");
        Check(CombatTeamPresets.Load(target).Updated == "2099-01-04", "Successful later update was not used.");
        Check(Object.ReferenceEquals(live, CombatTeamPresets.Current), "Importing to a test path polluted the user's live catalog cache.");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        T value = root as T; if (value != null) yield return value;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (T child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static string AllText(DependencyObject root)
    { string text = ""; foreach (TextBlock block in Descendants<TextBlock>(root)) text += block.Text + "\n"; return text; }
    private static T Tagged<T>(Window window, string tag) where T : FrameworkElement
    {
        FrameworkElement view = (FrameworkElement)window.Content;
        view.Measure(new Size(window.Width, window.Height)); view.Arrange(new Rect(0, 0, window.Width, window.Height)); view.UpdateLayout();
        foreach (T item in Descendants<T>(view)) if (Convert.ToString(item.Tag) == tag) return item;
        throw new Exception("Missing tagged control: " + tag);
    }
    private static void Click(Window window, string tag)
    { Tagged<Button>(window, tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    private static void Dialog(CombatRotationWindow window, string directory)
    {
        window.ApplyRoster(Own());
        Window dialog = window.CreateRosterDialog();
        try {
            Capture(dialog, Path.Combine(directory, "owned-character-table.png"));
            TextBox search = Tagged<TextBox>(dialog, "roster-search"); search.Text = "Vesna";
            Capture(dialog, Path.Combine(directory, "owned-character-search.png"));
            int matches = 0;
            foreach (CheckBox box in Descendants<CheckBox>((DependencyObject)dialog.Content)) {
                Check(CombatRosterMatcher.Normalize(Convert.ToString(box.Tag)) == CombatRosterMatcher.Normalize("Vesna"), "Alias search included unrelated characters.");
                matches++; box.IsChecked = true;
            }
            Check(matches == 1, "Alias search did not yield one selectable character.");
            search.Text = "not-a-real-character";
            Click(dialog, "roster-clear-visible");
            search.Text = "薇斯娜";
            Check(Tagged<CheckBox>(dialog, "薇斯娜").IsChecked == true, "Changing searches lost hidden selections.");
            Check(!CombatRosterMatcher.IsOwned(Field<CombatRosterProfile>(window, "rosterProfile"), "Vesna"), "Unsaved draft already changed the live roster.");
            Tagged<TextBox>(dialog, "roster-custom-name").Text = "未来手动角色";
            Click(dialog, "roster-add");
            Check(Tagged<CheckBox>(dialog, "未来手动角色").IsChecked == true, "Custom characters cannot be added and selected.");
            Click(dialog, "roster-save");
            Check(CombatRosterMatcher.IsOwned(Field<CombatRosterProfile>(window, "rosterProfile"), "Vesna") &&
                CombatRosterMatcher.IsOwned(Field<CombatRosterProfile>(window, "rosterProfile"), "未来手动角色"), "Saving the actual dialog failed to apply its draft.");
        } finally { dialog.Close(); }
        dialog = window.CreateRosterDialog();
        try {
            Click(dialog, "roster-clear-all");
            Click(dialog, "roster-cancel");
            Check(CombatRosterMatcher.IsOwned(Field<CombatRosterProfile>(window, "rosterProfile"), "未来手动角色"), "Cancel committed the cleared draft.");
        } finally { dialog.Close(); }
    }
    private static void Capture(Window window, string path)
    {
        FrameworkElement view = (FrameworkElement)window.Content;
        view.Measure(new Size(window.Width, window.Height)); view.Arrange(new Rect(0, 0, window.Width, window.Height)); view.UpdateLayout();
        RenderTargetBitmap image = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (FileStream output = File.Create(path)) encoder.Save(output);
    }

    private static void Visual(CombatTeamCatalog catalog, string directory)
    {
        CombatRotationWindow window = new CombatRotationWindow("ganyu", false) { Width = 1050, Height = 780 };
        try {
            window.ApplyCatalog(catalog);
            List<CombatCharacterEditor> editors = Field<List<CombatCharacterEditor>>(window, "editors");
            editors[0].ApplyPanelValues(new Dictionary<string, double> { { "CoreStat", 2468 } });
            CombatCharacterInput original = editors[0].Read();
            window.ApplyRoster(OwnTeam(catalog.Teams[0]));
            ComboBox choices = Field<ComboBox>(window, "presets");
            Check(choices.Items.Count >= 2, "Owned team is not offered in the actual selector.");
            foreach (ComboBoxItem item in choices.Items) {
                string id = Convert.ToString(item.Tag);
                if (String.IsNullOrEmpty(id)) continue;
                Check(CombatRosterMatcher.Missing(catalog.Find(id), OwnTeam(catalog.Teams[0])).Count == 0, "Selector offers an incomplete team while owned-only is on.");
            }
            Check(editors[0].Read().Name == original.Name && editors[0].Read().CoreStat == 2468, "Roster filtering overwrites current manual inputs.");
            foreach (string theme in new[] { "kolo", "hutao", "ganyu", "varesa" }) {
                window.ApplyCharacterTheme(theme);
                Capture(window, Path.Combine(directory, "roster-" + theme + ".png"));
                Check(AllText((DependencyObject)window.Content).Contains("我的角色"), "Roster entry is missing in " + theme);
            }
            window.ApplyRoster(Own());
            Check(choices.Items.Count == 1, "Explicitly empty roster still offers playable teams.");
            Capture(window, Path.Combine(directory, "roster-empty.png"));
            Field<CheckBox>(window, "onlyOwned").IsChecked = false;
            Check(choices.Items.Count == catalog.Teams.Count + 1, "Disabling the filter does not allow browsing missing-member references.");
            choices.SelectedIndex = 1;
            Check(AllText((DependencyObject)window.Content).Contains("缺少"), "Missing-member browsing does not explain who is absent.");
            Call(window, "ApplyPreset", Type.EmptyTypes);
            Check(editors[0].Read().Name == original.Name && editors[0].Read().CoreStat == 2468, "An incomplete reference replaced manual inputs.");
            Capture(window, Path.Combine(directory, "roster-missing.png"));
            Field<CheckBox>(window, "onlyOwned").IsChecked = true;
            // Applied unsupported presets keep their mechanism guard even if
            // the reference disappears from a newly loaded catalog.
            CombatTeamPreset unsupported = null; foreach (CombatTeamPreset team in catalog.Teams) if (!team.SupportsEstimate) { unsupported = team; break; }
            window.ApplyRoster(CombatRosterProfile.CreateEmpty());
            foreach (ComboBoxItem item in choices.Items) if ((string)item.Tag == unsupported.Id) { choices.SelectedItem = item; break; }
            Call(window, "ApplyPreset", Type.EmptyTypes);
            CombatTeamCatalog changed = Copy(catalog); changed.Teams.RemoveAll(delegate(CombatTeamPreset t) { return t.Id == unsupported.Id; });
            window.ApplyCatalog(changed);
            Check(Field<string>(window, "presetId") == unsupported.Id, "Refreshing references cleared applied identity.");
            Call(window, "Generate", new[] { typeof(bool) }, false);
            string result = AllText(Field<StackPanel>(window, "resultBody"));
            Check(result.Contains("仅提供手法") && !result.Contains("当前每秒"), "Catalog refresh removes an active unsupported-mechanic guard.");
            window.Width = 860; window.Height = 620;
            Capture(window, Path.Combine(directory, "roster-small.png"));
            // Reopening a saved preset absent from the new catalog has no
            // in-memory reference. It must not silently fall back to DPS.
            CombatPlanInput unavailable = CombatPlanInput.CreateExample();
            unavailable.PresetId = "removed-preset-from-old-catalog";
            unavailable.Characters[0].CoreStat = 2468;
            Call(window, "Load", new[] { typeof(CombatPlanInput) }, unavailable);
            Call(window, "Generate", new[] { typeof(bool) }, false);
            result = AllText(Field<StackPanel>(window, "resultBody"));
            Check(result.Contains("配队资料已失效") && !result.Contains("当前每秒"), "Reopening an unavailable preset silently enables generic DPS.");
            Check(editors[0].Read().CoreStat == 2468, "Unavailable reference destroyed saved panel values.");
            Capture(window, Path.Combine(directory, "roster-expired.png"));
            window.UseCustomReference();
            Check(Field<string>(window, "presetId") == "" && editors[0].Read().CoreStat == 2468, "Explicit custom transition did not preserve input.");
            Call(window, "Generate", new[] { typeof(bool) }, false);
            Check(AllText(Field<StackPanel>(window, "resultBody")).Contains("当前每秒"), "Explicit custom transition still cannot calculate the supported example.");
            Dialog(window, directory);
        } finally { window.Close(); }
    }

    [STAThread]
    private static int Main()
    {
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string directory = Path.Combine("artifacts", "v27", "checks-" + DateTime.Now.ToString("HHmmssfff")); Directory.CreateDirectory(directory);
        try {
            CombatTeamCatalog catalog = CombatTeamPresets.Parse(CombatTeamPresets.ReadEmbedded());
            Matching(catalog); Storage(directory); Imports(catalog, directory); Visual(catalog, directory);
            Console.WriteLine("PASS: " + checks + " ownership, aliases, safe persistence, future catalog imports and WPF filtering checks. Previews: " + directory);
            File.WriteAllText(Path.Combine("artifacts", "v27", "preview-directory.txt"), Path.GetFullPath(directory));
            return 0;
        } catch (Exception error) {
            Console.WriteLine("FAIL " + error.GetType().Name + ": " + error.Message);
            if (error.InnerException != null) Console.WriteLine(error.InnerException.Message);
            return 1;
        } finally { app.Shutdown(); }
    }
}
