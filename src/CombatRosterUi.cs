using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClaudePet
{
    // Inventory and reference-data updates are independent of the user's
    // current four editors. Filtering never reloads or rewrites panel values.
    internal sealed partial class CombatRotationWindow
    {
        private CombatRosterProfile rosterProfile;
        private TextBlock rosterSummary;
        private CheckBox onlyOwned;
        private string rosterNotice;
        private bool refreshingRecommendations;

        private void InitializeRosterControls(Panel parent)
        {
            CombatRosterLoad initial = persist ? CombatRosterStorage.Load() :
                new CombatRosterLoad { Profile = CombatRosterProfile.CreateEmpty() };
            rosterProfile = initial.Profile ?? CombatRosterProfile.CreateEmpty();
            rosterNotice = initial.Notice;
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 7) };
            actions.Children.Add(ActionButton("我的角色", delegate { CreateRosterDialog().ShowDialog(); }));
            actions.Children.Add(ActionButton("导入配队资料", ImportCatalog));
            actions.Children.Add(ActionButton("重新加载资料", ReloadCatalog));
            parent.Children.Add(actions);
            onlyOwned = new CheckBox { Content = Text("只看已拥有角色能组出的队伍", 12, false, false),
                IsChecked = true, Margin = new Thickness(6, 0, 4, 5), Tag = "roster-only-owned" };
            onlyOwned.Checked += delegate { RefreshRecommendations(); };
            onlyOwned.Unchecked += delegate { RefreshRecommendations(); };
            parent.Children.Add(onlyOwned);
            rosterSummary = Text("", 11, false, true); rosterSummary.Margin = new Thickness(4, 0, 4, 8);
            parent.Children.Add(rosterSummary);
        }

        internal void ApplyRoster(CombatRosterProfile value)
        {
            if (value == null) throw new ArgumentNullException("value");
            rosterProfile = new CombatRosterProfile { Version = value.Version, Configured = value.Configured,
                Owned = value.Owned == null ? new List<string>() : new List<string>(value.Owned) };
            rosterNotice = null;
            RefreshRecommendations();
        }

        internal void RefreshRecommendations()
        {
            if (presets == null || rosterProfile == null || onlyOwned == null) return;
            CombatTeamPreset selected = SelectedPreset();
            string selectedId = selected == null ? "" : selected.Id;
            List<CombatTeamPreset> matches = CombatRosterMatcher.Rank(catalog, rosterProfile, onlyOwned.IsChecked == true);
            refreshingRecommendations = true;
            try {
                presets.Items.Clear();
                presets.Items.Add(new ComboBoxItem { Content = "自定义配队", Tag = "" });
                foreach (CombatTeamPreset team in matches) {
                    List<string> missing = CombatRosterMatcher.Missing(team, rosterProfile);
                    string suffix = !rosterProfile.Configured ? " · 尚未筛选" : missing.Count == 0 ? " · 可直接组队" :
                        " · 缺" + missing.Count.ToString(CultureInfo.InvariantCulture) + "人";
                    ComboBoxItem item = new ComboBoxItem { Content = CombatTeamPresets.Label(team) + suffix, Tag = team.Id };
                    item.ToolTip = !rosterProfile.Configured ? "先在「我的角色」确认拥有情况。" : missing.Count == 0 ?
                        "四名角色均已拥有，仍需核对命座与武器条件。" : "缺少：" + String.Join("、", missing.ToArray());
                    presets.Items.Add(item);
                }
                Select(presets, selectedId);
                if (rosterProfile.Configured && presets.SelectedIndex == 0 && matches.Count > 0) presets.SelectedIndex = 1;
            } finally { refreshingRecommendations = false; }
            if (rosterSummary != null) rosterSummary.Text = RosterSummaryText();
            ShowRecommendation();
        }

        private string RosterSummaryText()
        {
            if (rosterProfile == null || !rosterProfile.Configured)
                return "尚未设置拥有表：先浏览全部参考队伍。点击「我的角色」勾选后，再按实际持有情况推荐。" +
                    (String.IsNullOrWhiteSpace(rosterNotice) ? "" : "\n" + rosterNotice);
            List<CombatTeamPreset> ready = CombatRosterMatcher.Rank(catalog, rosterProfile, true);
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in rosterProfile.Owned ?? new List<string>()) unique.Add(CombatRosterMatcher.Normalize(name));
            int owned = unique.Count;
            string summary = "已记录 " + owned + " 个角色/形态 · 资料库中可直接组队 " + ready.Count + " 套。";
            if (ready.Count == 0) summary += " 暂无符合拥有表的参考队伍；可取消筛选查看缺少谁，或手动填写自定义配队。";
            else if (onlyOwned != null && onlyOwned.IsChecked != true) summary += " 全部参考按缺少人数排列，不代表强度排名。";
            return summary + (String.IsNullOrWhiteSpace(rosterNotice) ? "" : "\n" + rosterNotice);
        }

        private CombatTeamPreset CurrentAppliedReference()
        {
            if (String.IsNullOrEmpty(presetId)) return null;
            return appliedReference != null && appliedReference.Id == presetId ? appliedReference : catalog.Find(presetId);
        }

        internal void UseCustomReference()
        {
            presetId = ""; appliedReference = null;
            resultBody.Children.Clear();
            resultBody.Children.Add(Card(Text("已转为自定义，四名角色及当前面板保持不变。\n请检查成员和倍率后重新生成；通用估算不涵盖特殊资源、反应或角色专属机制。", 13, false, false)));
            status.Text = "已转为自定义，尚未保存；重新生成后保存当前输入。";
        }

        internal void ApplyCatalog(CombatTeamCatalog value)
        {
            if (value == null || value.Teams == null) throw new ArgumentException("配队资料为空。", "value");
            // Preserve the applied reference's mechanism boundary even if a
            // subsequent catalogue drops that team or changes its members.
            appliedReference = CurrentAppliedReference();
            catalog = value;
            RefreshRecommendations();
            resultBody.Children.Clear();
            resultBody.Children.Add(Card(Text("配队资料已刷新，当前四名角色及面板输入保持不变。\n浏览新版手法后，可选择载入成员；重新生成会沿用当前输入及原已应用队伍的计算适用范围。", 13, false, false)));
        }

        private void ReloadCatalog()
        {
            if (importing) return;
            try {
                ApplyCatalog(CombatTeamPresets.Reload());
                status.Text = "已重新读取配队资料；拥有表和当前输入已保留。";
            } catch (Exception error) {
                PetStorage.Log("combat-catalog-reload", error); status.Text = "资料加载失败：" + error.Message + " 当前资料与输入仍保留。";
            }
        }

        private void ImportCatalog()
        {
            if (importing) return;
            OpenFileDialog picker = new OpenFileDialog { Title = "选择经过核验的配队资料 JSON", Filter = "配队资料|*.json",
                CheckFileExists = true, Multiselect = false };
            if (picker.ShowDialog(this) != true) return;
            try {
                CombatTeamCatalog updated;
                if (persist) updated = CombatTeamPresets.Import(picker.FileName);
                else {
                    if (new FileInfo(picker.FileName).Length > 512 * 1024) throw new InvalidDataException("配队资料超过512 KB。");
                    updated = CombatTeamPresets.Parse(File.ReadAllText(picker.FileName));
                }
                ApplyCatalog(updated);
                status.Text = "已导入 " + updated.Teams.Count + " 套资料（" + updated.Updated + "），拥有表和当前输入保持不变。";
            } catch (Exception error) {
                PetStorage.Log("combat-catalog-import", error); status.Text = "资料未导入：" + error.Message + " 原资料仍保留。";
            }
        }

        // Construction is separate from ShowDialog so real WPF search,
        // selection and save/cancel paths can be exercised without input tools.
        internal Window CreateRosterDialog()
        {
            Window dialog = new Window { Title = "我的角色 · 按拥有情况推荐", Width = 680, Height = 740,
                MinWidth = 520, MinHeight = 480, MaxHeight = Math.Max(480, SystemParameters.WorkArea.Height - 48),
                WindowStartupLocation = WindowStartupLocation.CenterOwner, FontFamily = FontFamily, FontSize = 13 };
            if (IsVisible) dialog.Owner = this;
            dialog.Resources.MergedDictionaries.Add(Resources);
            dialog.SetResourceReference(Window.BackgroundProperty, "PlannerSurface");
            dialog.SetResourceReference(Window.ForegroundProperty, "PlannerText");
            Border background = new Border { Padding = new Thickness(20) };
            background.SetResourceReference(Border.BackgroundProperty, "PlannerSurface");
            DockPanel shell = new DockPanel(); background.Child = shell; dialog.Content = background;
            StackPanel top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); shell.Children.Add(top);
            top.Children.Add(Text("先勾选你已拥有的角色", 21, true, false));
            top.Children.Add(Text("名单来自基础角色表、当前配队资料和你保存的自定义角色。旅行者按已解锁元素分别勾选；未来新角色可先添加，再导入对应攻略。", 12, false, true));
            if (!String.IsNullOrWhiteSpace(CombatRosterMatcher.BaseNotice)) {
                TextBlock scope = Text("基础名单不是实时名册；未收录角色可手动补充。悬停查看名单说明。", 11, false, true);
                scope.ToolTip = CombatRosterMatcher.BaseNotice; top.Children.Add(scope);
            }
            TextBox search = Input(); search.Tag = "roster-search"; search.ToolTip = "搜索角色；支持已收录的中英文别名。";
            top.Children.Add(Field("搜索角色", search));
            WrapPanel selectors = new WrapPanel { Margin = new Thickness(0, 1, 0, 8) }; top.Children.Add(selectors);
            TextBlock count = Text("", 12, true, false); count.Tag = "roster-count"; top.Children.Add(count);

            StackPanel bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); shell.Children.Add(bottom);
            TextBlock message = Text("这里只记录拥有情况，不会读取游戏账号；未保存的勾选可以取消。", 11, false, true);
            message.Tag = "roster-message"; bottom.Children.Add(message);
            Grid customRow = new Grid { Margin = new Thickness(0, 8, 0, 9) };
            customRow.ColumnDefinitions.Add(new ColumnDefinition()); customRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBox custom = Input(); custom.Tag = "roster-custom-name"; custom.ToolTip = "输入目录外角色名称（最多20个字符）"; custom.MaxLength = 20;
            customRow.Children.Add(custom); bottom.Children.Add(Text("没有找到？添加其他已拥有角色", 12, false, false)); bottom.Children.Add(customRow);
            WrapPanel footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) }; bottom.Children.Add(footer);
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 8, 0, 12) };
            shell.Children.Add(scroll);
            WrapPanel choices = new WrapPanel(); scroll.Content = choices;
            List<string> names = CombatRosterMatcher.KnownCharacters(catalog, rosterProfile);
            HashSet<string> draft = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string owned in rosterProfile.Owned ?? new List<string>()) draft.Add(CombatRosterMatcher.Normalize(owned));
            Dictionary<string, string> draftLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names) draftLabels[CombatRosterMatcher.Normalize(name)] = name;
            List<string> visible = new List<string>();
            Action updateCount = delegate { count.Text = "已勾选 " + draft.Count + " 个角色/形态 · 当前显示 " + visible.Count + " 项"; };
            Action render = delegate {
                choices.Children.Clear(); visible.Clear();
                string query = (search.Text ?? "").Trim(), normalized = CombatRosterMatcher.Normalize(query);
                foreach (string name in names) {
                    string key = CombatRosterMatcher.Normalize(name);
                    if (query.Length > 0 && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                        key.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    visible.Add(name);
                    CheckBox check = new CheckBox { Content = Text(name, 13, false, false), IsChecked = draft.Contains(key), Tag = name,
                        Width = 197, Margin = new Thickness(0, 5, 4, 5), Padding = new Thickness(2) };
                    check.SetResourceReference(Control.ForegroundProperty, "PlannerText");
                    check.Checked += delegate { draft.Add(key); updateCount(); };
                    check.Unchecked += delegate { draft.Remove(key); updateCount(); };
                    choices.Children.Add(check);
                }
                if (visible.Count == 0) choices.Children.Add(Text("没有匹配的角色。可在下方添加；添加姓名本身不会生成未经核实的攻略。", 12, false, true));
                updateCount();
            };
            Button all = ActionButton("勾选搜索结果", delegate { foreach (string name in visible) draft.Add(CombatRosterMatcher.Normalize(name)); render(); });
            all.Tag = "roster-select-visible"; selectors.Children.Add(all);
            Button none = ActionButton("取消搜索结果", delegate { foreach (string name in visible) draft.Remove(CombatRosterMatcher.Normalize(name)); render(); });
            none.Tag = "roster-clear-visible"; selectors.Children.Add(none);
            Button clear = ActionButton("清空全部勾选", delegate { draft.Clear(); render(); });
            clear.Tag = "roster-clear-all"; selectors.Children.Add(clear);
            Button add = ActionButton("添加并勾选", delegate {
                string text = (custom.Text ?? "").Trim();
                if (text.Length == 0 || text.Length > 20 || text.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0) {
                    message.Text = "请输入1到20个字符的角色名称。"; return;
                }
                string key = CombatRosterMatcher.Normalize(text);
                bool exists = false; foreach (string name in names) if (CombatRosterMatcher.Normalize(name) == key) { exists = true; break; }
                if (!exists) { names.Add(CombatRosterMatcher.DisplayName(text)); names.Sort(StringComparer.CurrentCultureIgnoreCase); }
                draftLabels[key] = CombatRosterMatcher.DisplayName(text);
                draft.Add(key); custom.Text = ""; search.Text = ""; render();
                message.Text = "已勾选「" + CombatRosterMatcher.DisplayName(text) + "」。尚未保存；没有攻略的角色会等待后续资料更新。";
            });
            add.Tag = "roster-add"; Grid.SetColumn(add, 1); customRow.Children.Add(add);
            Button cancel = ActionButton("取消", delegate { dialog.Close(); }); cancel.Tag = "roster-cancel"; footer.Children.Add(cancel);
            Button save = ActionButton("保存拥有表并推荐", delegate {
                CombatRosterProfile pending = new CombatRosterProfile { Version = 1, Configured = true, Owned = new List<string>() };
                foreach (string key in draft) { string label; pending.Owned.Add(draftLabels.TryGetValue(key, out label) ? label : CombatRosterMatcher.DisplayName(key)); }
                try {
                    if (persist) CombatRosterStorage.Save(pending);
                    ApplyRoster(pending);
                    status.Text = "拥有表已更新，推荐已按持有情况筛选。当前面板输入保持不变。";
                    dialog.Close();
                } catch (Exception error) { PetStorage.Log("combat-roster-save", error); message.Text = "保存失败：" + error.Message + " 当前勾选仍保留，可重试或取消。"; }
            });
            save.Tag = "roster-save"; footer.Children.Add(save);
            search.TextChanged += delegate { render(); };
            dialog.KeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e) {
                if (e.Key == System.Windows.Input.Key.Escape) dialog.Close();
            };
            render(); return dialog;
        }
    }
}
