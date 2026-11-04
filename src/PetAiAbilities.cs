using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    // UTC deadlines keep focus time independent of animation speed, frame
    // drops, dragging and suspension. A completed session fires exactly once.
    internal sealed class DragonFocusSession
    {
        internal bool Active { get; private set; }
        internal DateTime EndsUtc { get; private set; }
        internal int Minutes { get; private set; }
        internal bool Start(DateTime utc, int minutes)
        {
            if (minutes != 15 && minutes != 25 && minutes != 45) throw new ArgumentOutOfRangeException("minutes");
            if (Active) return false;
            Minutes = minutes; EndsUtc = utc.ToUniversalTime().AddMinutes(minutes); Active = true; return true;
        }
        internal double Remaining(DateTime utc)
        { return Active ? Math.Max(0, Math.Min(Minutes * 60, (EndsUtc - utc.ToUniversalTime()).TotalSeconds)) : 0; }
        internal double Progress(DateTime utc)
        { return Active ? 1 - Remaining(utc) / (Minutes * 60) : 0; }
        internal bool Advance(DateTime utc)
        {
            if (!Active || utc.ToUniversalTime() < EndsUtc) return false;
            Active = false; return true;
        }
        internal void Cancel() { Active = false; }
        internal string RemainingText(DateTime utc)
        {
            int seconds = (int)Math.Ceiling(Remaining(utc));
            return (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class BubbleNote
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string UpdatedUtc { get; set; }
        [ScriptIgnore] public string Preview { get { return (Text ?? "").Replace("\r", " ").Replace("\n", " "); } }
        [ScriptIgnore] public string UpdatedLabel {
            get {
                DateTime date;
                return DateTime.TryParse(UpdatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date) ?
                    date.ToLocalTime().ToString("MM-dd HH:mm") : "已保存";
            }
        }
        internal BubbleNote Copy() { return new BubbleNote { Id = Id, Text = Text, UpdatedUtc = UpdatedUtc }; }
        public override string ToString()
        {
            string first = (Text ?? "").Replace("\r", " ").Replace("\n", " ");
            if (first.Length > 32) first = first.Substring(0, 32) + "…";
            DateTime date;
            return (DateTime.TryParse(UpdatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date) ? date.ToLocalTime().ToString("MM-dd HH:mm") + "  " : "") + first;
        }
    }

    internal sealed class BubbleNoteBook
    {
        public int Version { get; set; }
        public List<BubbleNote> Notes { get; set; }
        internal static BubbleNoteBook Empty() { return new BubbleNoteBook { Version = 1, Notes = new List<BubbleNote>() }; }
        internal BubbleNoteBook Copy()
        {
            BubbleNoteBook result = Empty(); foreach (BubbleNote note in Notes) result.Notes.Add(note.Copy()); return result;
        }
    }

    internal static class BubbleNoteStorage
    {
        internal const int MaximumNotes = 100, MaximumText = 2000, MaximumBytes = 1024 * 1024;
        internal static string DefaultPath { get { return Path.Combine(PetStorage.RootDirectory, "deepseek-bubbles.json"); } }
        private static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = MaximumBytes, RecursionLimit = 12 }; }
        private static BubbleNoteBook Read(string path)
        {
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("灵感本超过1 MB。");
            BubbleNoteBook result = Serializer().Deserialize<BubbleNoteBook>(File.ReadAllText(path, Encoding.UTF8));
            Validate(result); return result;
        }
        private static void Validate(BubbleNoteBook book)
        {
            if (book == null || book.Version != 1 || book.Notes == null || book.Notes.Count > MaximumNotes)
                throw new InvalidDataException("灵感本格式无效，最多保存100条。");
            HashSet<string> ids = new HashSet<string>();
            foreach (BubbleNote note in book.Notes) {
                Guid id; DateTime updated;
                if (note == null || !Guid.TryParse(note.Id, out id) || !ids.Add(note.Id) || String.IsNullOrWhiteSpace(note.Text) || note.Text.Length > MaximumText ||
                    !DateTime.TryParse(note.UpdatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out updated))
                    throw new InvalidDataException("灵感条目损坏或超过2000字。");
            }
        }
        internal static BubbleNoteBook Load(string path, out string notice)
        {
            notice = null;
            if (!File.Exists(path)) return BubbleNoteBook.Empty();
            try { return Read(path); }
            catch (Exception error) {
                string backup = PetStorage.Quarantine(path, "deepseek-notes-load");
                notice = "灵感本暂时无法读取：" + error.Message + (backup == null ? " 原文件未能备份，暂不允许覆盖。" : " 原文件已留存备份；当前显示空白灵感本。");
                return BubbleNoteBook.Empty();
            }
        }
        internal static void Save(string path, BubbleNoteBook book)
        {
            Validate(book);
            string json = Serializer().Serialize(book);
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("灵感本超过1 MB。");
            if (File.Exists(path)) {
                bool valid = true; try { Read(path); } catch { valid = false; }
                if (!valid && PetStorage.Quarantine(path, "deepseek-notes-before-save") == null)
                    throw new IOException("原灵感本无法读取且备份失败，已停止覆盖。");
            }
            PetStorage.WriteAllTextAtomic(path, json);
        }
    }

    internal sealed class BubbleNotesWindow : Window
    {
        private readonly string path;
        private readonly Action saved;
        private BubbleNoteBook book;
        private readonly ListBox history = new ListBox();
        internal readonly TextBox Editor = new TextBox();
        internal readonly TextBlock Status = new TextBlock();
        private string selectedId;
        private bool refreshing;
        private string savedEditorText = "";
        private readonly TextBlock countLabel = new TextBlock(), editorCount = new TextBlock(), emptyHistory = new TextBlock();
        private readonly Button saveButton, deleteButton;
        internal Func<bool> DiscardConfirmation;
        internal int NoteCount { get { return book.Notes.Count; } }

        internal BubbleNotesWindow(string storagePath, Action onSaved, BitmapSource portrait = null)
        {
            path = storagePath; saved = onSaved;
            Title = "原神启动 · DeepSeek 灵感泡泡"; Width = 620; Height = 640; MinWidth = 430; MinHeight = 450;
            MaxHeight = Math.Max(450, SystemParameters.WorkArea.Height - 40);
            FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 14;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResizeWithGrip; ShowInTaskbar = true;
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            ConfigureTheme(); ConfigureControls();
            SetResourceReference(ForegroundProperty, "NotesText");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            DiscardConfirmation = delegate { return MessageBox.Show(this, "当前修改尚未保存。放弃这段修改吗？", "保留灵感", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK; };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; };
            string notice; book = BubbleNoteStorage.Load(path, out notice);
            Border outer = Surface("NotesSurface", new Thickness(0)); outer.Tag = "bubble-shell";
            outer.CornerRadius = new CornerRadius(24); Content = outer;
            Grid layout = new Grid(); outer.Child = layout;
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(96) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Border header = new Border { Tag = "bubble-header", CornerRadius = new CornerRadius(23, 23, 0, 0), Padding = new Thickness(20, 12, 20, 12) };
            header.SetResourceReference(Border.BackgroundProperty, "NotesHeader"); layout.Children.Add(header);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed) DragMove();
            };
            Grid heading = new Grid(); header.Child = heading;
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border avatar = Surface("NotesSelected", new Thickness(0)); avatar.Width = 56; avatar.Height = 56; avatar.CornerRadius = new CornerRadius(18);
            if (portrait != null) {
                Image art = new Image { Source = portrait, Width = 62, Height = 68, Stretch = Stretch.Uniform, IsHitTestVisible = false };
                RenderOptions.SetBitmapScalingMode(art, BitmapScalingMode.HighQuality); avatar.Child = art;
            } else {
                TextBlock mark = Label("◌", 34, "NotesAccent"); mark.FontFamily = new FontFamily("Segoe UI Symbol");
                mark.HorizontalAlignment = HorizontalAlignment.Center; mark.VerticalAlignment = VerticalAlignment.Center; avatar.Child = mark;
            }
            heading.Children.Add(avatar);
            StackPanel title = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
            Grid.SetColumn(title, 1); heading.Children.Add(title);
            TextBlock name = Label("灵感泡泡", 25, "NotesText"); name.FontWeight = FontWeights.Bold; title.Children.Add(name);
            TextBlock subtitle = Label("DeepSeek · 收好每一个小想法", 11.5, "NotesMuted");
            subtitle.TextTrimming = TextTrimming.CharacterEllipsis; subtitle.Margin = new Thickness(0, 5, 0, 0); title.Children.Add(subtitle);
            StackPanel controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(controls, 2); heading.Children.Add(controls);
            Button minimize = Button("—", delegate { WindowState = WindowState.Minimized; });
            minimize.Tag = "bubble-minimize"; minimize.ToolTip = "最小化"; minimize.Width = minimize.Height = 32; minimize.Padding = new Thickness(0); controls.Children.Add(minimize);
            System.Windows.Automation.AutomationProperties.SetName(minimize, "最小化灵感泡泡");
            Button close = Button("×", delegate { Close(); }); close.Tag = "bubble-close"; close.ToolTip = "关闭";
            close.Width = close.Height = 32; close.FontSize = 21; close.Padding = new Thickness(0); close.Margin = new Thickness(7, 0, 0, 0); controls.Children.Add(close);
            System.Windows.Automation.AutomationProperties.SetName(close, "关闭灵感泡泡");

            Grid main = new Grid { Margin = new Thickness(20, 14, 20, 0) }; Grid.SetRow(main, 1); layout.Children.Add(main);
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.55, GridUnitType.Star), MinHeight = 85, MaxHeight = 160 });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 90 });
            Border historyCard = Surface("NotesCard", new Thickness(12, 8, 12, 8)); main.Children.Add(historyCard);
            DockPanel historyBody = new DockPanel(); historyCard.Child = historyBody;
            DockPanel historyHeading = new DockPanel { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(historyHeading, Dock.Top); historyBody.Children.Add(historyHeading);
            Button create = Button("＋ 新建", delegate { NewNote(); }); create.Tag = "新建"; create.Padding = new Thickness(10, 5, 10, 5); create.FontSize = 12;
            DockPanel.SetDock(create, Dock.Right); historyHeading.Children.Add(create);
            countLabel.FontSize = 12; countLabel.FontWeight = FontWeights.SemiBold; countLabel.VerticalAlignment = VerticalAlignment.Center;
            countLabel.SetResourceReference(TextBlock.ForegroundProperty, "NotesMuted"); historyHeading.Children.Add(countLabel);
            Grid historyArea = new Grid(); historyBody.Children.Add(historyArea);
            history.Tag = "bubble-history"; history.Background = Brushes.Transparent; history.BorderThickness = new Thickness(0);
            history.ItemTemplate = (DataTemplate)FindResource("NotesHistoryTemplate");
            history.Padding = new Thickness(0); history.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            ScrollViewer.SetHorizontalScrollBarVisibility(history, ScrollBarVisibility.Disabled); historyArea.Children.Add(history);
            System.Windows.Automation.AutomationProperties.SetName(history, "已保存的灵感");
            emptyHistory.Text = "还没有泡泡，写下第一个想法吧。"; emptyHistory.FontSize = 12;
            emptyHistory.SetResourceReference(TextBlock.ForegroundProperty, "NotesMuted"); emptyHistory.IsHitTestVisible = false;
            emptyHistory.HorizontalAlignment = HorizontalAlignment.Center; emptyHistory.VerticalAlignment = VerticalAlignment.Center; historyArea.Children.Add(emptyHistory);

            Border editorCard = Surface("NotesCard", new Thickness(12, 10, 12, 12)); Grid.SetRow(editorCard, 2); main.Children.Add(editorCard);
            DockPanel editorBody = new DockPanel(); editorCard.Child = editorBody;
            DockPanel editorHeading = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(editorHeading, Dock.Top); editorBody.Children.Add(editorHeading);
            editorCount.FontSize = 11; editorCount.SetResourceReference(TextBlock.ForegroundProperty, "NotesMuted");
            DockPanel.SetDock(editorCount, Dock.Right); editorHeading.Children.Add(editorCount);
            TextBlock editorTitle = Label("此刻的灵感", 12, "NotesMuted"); editorTitle.FontWeight = FontWeights.SemiBold; editorHeading.Children.Add(editorTitle);
            Editor.Tag = "bubble-editor"; Editor.AcceptsReturn = true; Editor.TextWrapping = TextWrapping.Wrap;
            Editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; Editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Editor.MaxLength = BubbleNoteStorage.MaximumText; Editor.Padding = new Thickness(12); Editor.FontSize = 15;
            System.Windows.Automation.AutomationProperties.SetName(Editor, "灵感正文，最多2000字");
            editorBody.Children.Add(Editor);

            Grid footer = new Grid { Margin = new Thickness(20, 12, 20, 17) }; Grid.SetRow(footer, 2); layout.Children.Add(footer);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            deleteButton = Button("删除所选", delegate {
                if (String.IsNullOrEmpty(selectedId)) { Status.Text = "先选择一条已保存的泡泡。"; return; }
                if (MessageBox.Show(this, "删除这条已保存的灵感？", "灵感泡泡", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) DeleteSelected();
            });
            deleteButton.HorizontalAlignment = HorizontalAlignment.Left; deleteButton.SetResourceReference(Control.ForegroundProperty, "NotesAttention"); footer.Children.Add(deleteButton);
            saveButton = Button("保存泡泡", delegate { SaveCurrent(); }); saveButton.Style = (Style)FindResource("NotesPrimaryButton");
            Grid.SetColumn(saveButton, 1); footer.Children.Add(saveButton);
            Status.TextWrapping = TextWrapping.Wrap; Status.FontSize = 11.5; Status.Margin = new Thickness(0, 10, 0, 0);
            Status.MaxHeight = 32; Status.TextTrimming = TextTrimming.CharacterEllipsis;
            Status.SetBinding(ToolTipProperty, new System.Windows.Data.Binding("Text") { Source = Status });
            Status.SetResourceReference(TextBlock.ForegroundProperty, "NotesMuted"); Grid.SetRow(Status, 1); Grid.SetColumnSpan(Status, 2); footer.Children.Add(Status);
            history.SelectionChanged += delegate {
                if (refreshing) return;
                BubbleNote item = history.SelectedItem as BubbleNote;
                if (item != null) {
                    if (!ConfirmDiscard()) {
                        refreshing = true;
                        try { history.SelectedItem = book.Notes.Find(delegate(BubbleNote note) { return note.Id == selectedId; }); }
                        finally { refreshing = false; }
                        return;
                    }
                    selectedId = item.Id; Editor.Text = item.Text; savedEditorText = Editor.Text;
                    UpdateEditorState();
                    Status.Text = "正在编辑已保存的灵感；修改后请再次保存。";
                }
            };
            Editor.TextChanged += delegate { UpdateEditorState(); };
            RefreshHistory(); UpdateEditorState(); Status.Text = notice ?? "只保存在本机 · 点击保存才会记录";
        }
        private static Brush ColorBrush(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
        private static Button Button(string label, Action action)
        {
            Button button = new Button { Content = label, Tag = label, Padding = new Thickness(15, 8, 15, 8), Cursor = Cursors.Hand };
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += delegate { action(); }; return button;
        }
        private static TextBlock Label(string value, double size, string color)
        { TextBlock label = new TextBlock { Text = value, FontSize = size }; label.SetResourceReference(TextBlock.ForegroundProperty, color); return label; }
        private static Border Surface(string color, Thickness padding)
        {
            Border card = new Border { CornerRadius = new CornerRadius(13), Padding = padding, BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, color); card.SetResourceReference(Border.BorderBrushProperty, "NotesBorder"); return card;
        }
        private void UpdateEditorState()
        {
            editorCount.Text = Editor.Text.Length + " / 2000" + (Editor.Text == savedEditorText ? "" : " · 未保存");
            saveButton.IsEnabled = !String.IsNullOrWhiteSpace(Editor.Text); deleteButton.IsEnabled = !String.IsNullOrEmpty(selectedId);
        }
        private void ConfigureTheme()
        {
            PetWindow.MenuPalette palette = PetWindow.MenuPalette.ForCharacter("deepseek");
            string[] keys = { "NotesSurface", "NotesText", "NotesMuted", "NotesAccent", "NotesBorder", "NotesHover", "NotesSelected", "NotesDisabled", "NotesAttention" };
            string[] colors = { palette.Surface, palette.Text, palette.Muted, palette.Accent, palette.Border, palette.Hover, palette.Checked, palette.Disabled, palette.Exit };
            for (int i = 0; i < keys.Length; i++) Resources[keys[i]] = ColorBrush(colors[i]);
            // Use the same lightly tinted card treatment as DailyHighlights.
            Color surface = (Color)ColorConverter.ConvertFromString(palette.Surface), text = (Color)ColorConverter.ConvertFromString(palette.Text);
            Resources["NotesCard"] = new SolidColorBrush(Color.FromRgb((byte)(surface.R + (text.R - surface.R) * .045),
                (byte)(surface.G + (text.G - surface.G) * .045), (byte)(surface.B + (text.B - surface.B) * .045)));
            Resources["NotesHeader"] = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(palette.Checked), surface, 14);
        }
        private void ConfigureControls()
        {
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type Button}'>
 <Setter Property='Foreground' Value='{DynamicResource NotesText}'/><Setter Property='Background' Value='{DynamicResource NotesSurface}'/>
 <Setter Property='BorderBrush' Value='{DynamicResource NotesBorder}'/><Setter Property='BorderThickness' Value='1'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
  <Border x:Name='Face' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='10' Padding='{TemplateBinding Padding}'>
   <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
  </Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource NotesHover}'/></Trigger>
   <Trigger Property='IsPressed' Value='True'><Setter TargetName='Face' Property='Opacity' Value='.75'/></Trigger>
   <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Face' Property='BorderBrush' Value='{DynamicResource NotesAccent}'/><Setter TargetName='Face' Property='BorderThickness' Value='2'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter Property='Foreground' Value='{DynamicResource NotesDisabled}'/><Setter TargetName='Face' Property='Opacity' Value='.65'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='NotesPrimaryButton' TargetType='{x:Type Button}' BasedOn='{StaticResource {x:Type Button}}'>
 <Setter Property='Background' Value='{DynamicResource NotesAccent}'/><Setter Property='Foreground' Value='White'/><Setter Property='FontWeight' Value='SemiBold'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
  <Border x:Name='Face' Background='{TemplateBinding Background}' BorderBrush='{DynamicResource NotesAccent}' BorderThickness='1' CornerRadius='10' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource NotesText}'/></Trigger>
   <Trigger Property='IsPressed' Value='True'><Setter TargetName='Face' Property='Opacity' Value='.75'/></Trigger>
   <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Face' Property='BorderBrush' Value='{DynamicResource NotesText}'/><Setter TargetName='Face' Property='BorderThickness' Value='2'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Face' Property='Background' Value='{DynamicResource NotesSelected}'/><Setter Property='Foreground' Value='{DynamicResource NotesMuted}'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='{x:Type TextBox}'>
 <Setter Property='Foreground' Value='{DynamicResource NotesText}'/><Setter Property='Background' Value='{DynamicResource NotesSurface}'/>
 <Setter Property='BorderBrush' Value='{DynamicResource NotesBorder}'/><Setter Property='BorderThickness' Value='1'/>
 <Setter Property='CaretBrush' Value='{DynamicResource NotesText}'/><Setter Property='SelectionBrush' Value='{DynamicResource NotesHover}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TextBox}'>
  <Border x:Name='Face' CornerRadius='9' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost'/></Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Face' Property='BorderBrush' Value='{DynamicResource NotesAccent}'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter Property='Foreground' Value='{DynamicResource NotesDisabled}'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='{x:Type ListBoxItem}'>
 <Setter Property='Foreground' Value='{DynamicResource NotesText}'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/>
 <Setter Property='Padding' Value='9,6'/><Setter Property='Margin' Value='0,1,3,1'/><Setter Property='BorderThickness' Value='1'/>
 <Setter Property='Background' Value='Transparent'/><Setter Property='BorderBrush' Value='Transparent'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'>
  <Border x:Name='Face' CornerRadius='8' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'/></Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource NotesHover}'/></Trigger>
   <Trigger Property='IsSelected' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource NotesSelected}'/><Setter TargetName='Face' Property='BorderBrush' Value='{DynamicResource NotesBorder}'/></Trigger>
   <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Face' Property='BorderBrush' Value='{DynamicResource NotesAccent}'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<DataTemplate x:Key='NotesHistoryTemplate'><Grid>
 <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
 <TextBlock Text='{Binding Preview}' FontSize='13' TextTrimming='CharacterEllipsis' Margin='0,0,12,0'/>
 <TextBlock Grid.Column='1' Text='{Binding UpdatedLabel}' Foreground='{DynamicResource NotesMuted}' FontSize='11' VerticalAlignment='Center'/>
</Grid></DataTemplate>
<Style x:Key='NotesScrollPage' TargetType='{x:Type RepeatButton}'>
 <Setter Property='Focusable' Value='False'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='{x:Type ScrollBar}'>
 <Setter Property='Width' Value='10'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
  <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'>
   <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Style='{StaticResource NotesScrollPage}'/></Track.DecreaseRepeatButton>
   <Track.Thumb><Thumb MinHeight='26'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border x:Name='Face' Background='{DynamicResource NotesBorder}' CornerRadius='4' Margin='2,1'/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource NotesAccent}'/></Trigger><Trigger Property='IsDragging' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource NotesAccent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
   <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Style='{StaticResource NotesScrollPage}'/></Track.IncreaseRepeatButton>
  </Track></Grid>
 </ControlTemplate></Setter.Value></Setter>
</Style>
</ResourceDictionary>";
            Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(xaml));
        }
        private void RefreshHistory()
        {
            refreshing = true;
            try {
                history.Items.Clear();
                foreach (BubbleNote note in book.Notes) {
                    history.Items.Add(note);
                    if (note.Id == selectedId) history.SelectedItem = note;
                }
            }
            finally { refreshing = false; }
            countLabel.Text = "已保存的灵感 · " + book.Notes.Count + " / 100";
            emptyHistory.Visibility = book.Notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        internal bool ConfirmDiscard()
        {
            return Editor.Text == savedEditorText || (DiscardConfirmation != null && DiscardConfirmation());
        }
        internal bool NewNote()
        {
            if (!ConfirmDiscard()) return false;
            selectedId = null; history.SelectedIndex = -1; Editor.Clear(); savedEditorText = "";
            UpdateEditorState();
            Status.Text = "新泡泡还没有保存。"; Editor.Focus(); return true;
        }
        internal bool SaveCurrent()
        {
            string text = Editor.Text.Trim();
            if (String.IsNullOrWhiteSpace(text)) { Status.Text = "先写下一点灵感吧。"; return false; }
            BubbleNoteBook candidate = book.Copy(); BubbleNote target = candidate.Notes.Find(delegate(BubbleNote note) { return note.Id == selectedId; });
            if (target == null) {
                if (candidate.Notes.Count >= BubbleNoteStorage.MaximumNotes) { Status.Text = "已保存100条，先整理不需要的泡泡再添加。"; return false; }
                target = new BubbleNote { Id = Guid.NewGuid().ToString("N") }; candidate.Notes.Insert(0, target);
            }
            target.Text = text; target.UpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            try {
                BubbleNoteStorage.Save(path, candidate); book = candidate; selectedId = target.Id; RefreshHistory();
                Editor.Text = text; savedEditorText = text;
                UpdateEditorState();
                Status.Text = "已保存在本机 · " + book.Notes.Count + " / 100 条。";
                if (saved != null) saved(); return true;
            } catch (Exception error) { PetStorage.Log("deepseek-notes-save", error); Status.Text = "保存失败，文字仍在：" + error.Message; return false; }
        }
        internal bool DeleteSelected()
        {
            BubbleNoteBook candidate = book.Copy();
            if (candidate.Notes.RemoveAll(delegate(BubbleNote item) { return item.Id == selectedId; }) == 0) return false;
            try {
                BubbleNoteStorage.Save(path, candidate); book = candidate; savedEditorText = Editor.Text; NewNote(); RefreshHistory(); Status.Text = "已删除选中的泡泡。"; return true;
            } catch (Exception error) { PetStorage.Log("deepseek-notes-delete", error); Status.Text = "删除失败，原记录仍保留：" + error.Message; return false; }
        }
    }

    internal sealed class AiCompanionVisual : FrameworkElement
    {
        private string character;
        private double phase, progress, bubbleLife;
        private bool focusing;
        internal void Update(string key, double seconds, bool active, double fraction, double bubbles)
        { character = key; phase = seconds; focusing = active; progress = Math.Max(0, Math.Min(1, fraction)); bubbleLife = Math.Max(0, bubbles); InvalidateVisual(); }
        protected override void OnRender(DrawingContext draw)
        {
            base.OnRender(draw);
            if (character == "gpt6a" && focusing) {
                Point center = new Point(165, 283); double radius = 126;
                Brush lavender = new SolidColorBrush(Color.FromArgb(140, 148, 117, 191));
                Brush lilac = new SolidColorBrush(Color.FromRgb(158, 126, 204));
                Brush moonlight = new SolidColorBrush(Color.FromRgb(244, 237, 255));
                draw.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(45, 204, 184, 235)), 10), center, radius, radius);
                draw.DrawEllipse(null, new Pen(lavender, 1.5), center, radius, radius);
                int lit = (int)Math.Floor(progress * 60);
                for (int i = 0; i < 60; i++) {
                    double angle = -Math.PI / 2 + i * Math.PI / 30;
                    Point a = new Point(center.X + Math.Cos(angle) * (radius - 4), center.Y + Math.Sin(angle) * (radius - 4));
                    Point b = new Point(center.X + Math.Cos(angle) * (radius + 3), center.Y + Math.Sin(angle) * (radius + 3));
                    draw.DrawLine(new Pen(i <= lit ? lilac : lavender, i <= lit ? 2.6 : 1), a, b);
                }
                for (int i = 0; i < 3; i++) {
                    double angle = phase * .35 + i * Math.PI * 2 / 3;
                    Point p = new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
                    draw.DrawEllipse(moonlight, new Pen(lavender, 1), p, 4, 4);
                }
            }
            if (character == "deepseek" && bubbleLife > 0) {
                double fade = Math.Min(1, bubbleLife / 1.2);
                for (int i = 0; i < 10; i++) {
                    double t = (phase * .24 + i / 10.0) % 1;
                    double x = 40 + i * 27 + Math.Sin(phase * 2 + i) * 13;
                    double y = 412 - t * 300;
                    double size = 6 + i % 4 * 2.5;
                    Brush edge = new SolidColorBrush(Color.FromArgb((byte)(180 * fade * (1 - t * .5)), 97, 193, 242));
                    draw.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(30 * fade), 153, 223, 255)), new Pen(edge, 1.7), new Point(x, y), size, size);
                    draw.DrawEllipse(Brushes.White, null, new Point(x - size * .3, y - size * .35), 1.8, 1.8);
                }
            }
        }
    }

    public sealed partial class PetWindow
    {
        private readonly DragonFocusSession dragonFocus = new DragonFocusSession();
        private AiCompanionVisual aiCompanionVisual;
        private Border dragonFocusHud;
        private TextBlock dragonFocusLabel;
        private BubbleNotesWindow bubbleNotesWindow;
        private double aiEffectSeconds, bubbleEffectRemaining, aiCastRemaining;
        private DateTime aiCastDeadlineUtc;

        private void InitializeAiAbilities(Grid root)
        {
            aiCompanionVisual = new AiCompanionVisual { IsHitTestVisible = false }; root.Children.Add(aiCompanionVisual);
            dragonFocusLabel = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(247, 244, 255)), FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 20, TextAlignment = TextAlignment.Center };
            dragonFocusHud = new Border { Width = 240, Height = 40, CornerRadius = new CornerRadius(12), Padding = new Thickness(8, 5, 8, 5),
                Background = new SolidColorBrush(Color.FromArgb(235, 55, 41, 76)), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 185, 232)), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 94, 0, 0),
                IsHitTestVisible = false, Visibility = Visibility.Collapsed, Child = dragonFocusLabel };
            root.Children.Add(dragonFocusHud);
        }
        private void StartDragonFocus(int minutes)
        {
            if (currentCharacter != "gpt6a") return;
            DateTime utc = DateTime.UtcNow;
            if (!dragonFocus.Start(utc, minutes)) { ShowSpeech("龙息专注还剩 " + dragonFocus.RemainingText(utc) + "，慢慢来。右键可以取消。" ); return; }
            BeginAiCast();
            ShowSpeech("星环亮起，陪你专注 " + minutes + " 分钟。切换形象或退出会取消计时。" );
        }
        private bool HandleAiDoubleClick()
        {
            if (currentCharacter == "gpt6a") { StartDragonFocus(25); return true; }
            if (currentCharacter == "deepseek") { OpenBubbleNotes(); return true; }
            return false;
        }
        private void OpenBubbleNotes()
        {
            if (currentCharacter != "deepseek") return;
            if (bubbleNotesWindow == null) {
                bubbleNotesWindow = new BubbleNotesWindow(BubbleNoteStorage.DefaultPath, delegate {
                    if (currentCharacter == "deepseek") { bubbleEffectRemaining = 7; BeginAiCast(); ShowSpeech("叽咕！这颗灵感泡泡收好啦。" ); }
                }, frames["idle"][0]);
                bubbleNotesWindow.Closed += delegate { bubbleNotesWindow = null; };
            }
            bubbleEffectRemaining = 4; BeginAiCast();
            bubbleNotesWindow.Show(); if (bubbleNotesWindow.WindowState == WindowState.Minimized) bubbleNotesWindow.WindowState = WindowState.Normal;
            bubbleNotesWindow.Activate();
        }
        private void AddAiAbilityMenu(ContextMenu menu)
        {
            if (currentCharacter == "gpt6a") {
                MenuItem focus = StyledItem("✧", "龙息专注", null);
                foreach (int choice in new[] { 15, 25, 45 }) {
                    int minutes = choice; focus.Items.Add(StyledItem("◷", "专注 " + minutes + " 分钟", delegate { StartDragonFocus(minutes); }));
                }
                focus.Items.Add(StyledItem("◉", "查看剩余时间", delegate { ShowSpeech(dragonFocus.Active ? "星环还剩 " + dragonFocus.RemainingText(DateTime.UtcNow) + "。" : "还没有开始专注。双击我可开始25分钟。" ); }));
                focus.Items.Add(StyledItem("×", "取消本次专注", delegate { dragonFocus.Cancel(); ShowSpeech("星环收起，休息一下吧。" ); }));
                menu.Items.Add(focus);
            } else if (currentCharacter == "deepseek") menu.Items.Add(StyledItem("○", "灵感泡泡 · 本地随手记", OpenBubbleNotes));
        }
        private void UpdateAiAbilities(DateTime now, double deltaSeconds)
        {
            aiEffectSeconds += Math.Max(0, deltaSeconds);
            bubbleEffectRemaining = Math.Max(0, bubbleEffectRemaining - Math.Max(0, deltaSeconds));
            if (now.ToUniversalTime() >= aiCastDeadlineUtc) aiCastRemaining = 0;
            else if (state == "idle" && movement == "rest" && !pointerHeld)
                aiCastRemaining = Math.Max(0, aiCastRemaining - Math.Max(0, deltaSeconds));
            if (dragonFocus.Advance(now.ToUniversalTime()) && currentCharacter == "gpt6a") {
                BeginAiCast(); ShowSpeech("龙息专注完成！做得好，活动肩颈、喝口水吧。" );
            }
            bool active = currentCharacter == "gpt6a" && dragonFocus.Active && IsVisible;
            if (dragonFocusHud != null) {
                dragonFocusHud.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
                if (active) dragonFocusLabel.Text = "龙息专注  " + dragonFocus.RemainingText(now.ToUniversalTime());
            }
            if (aiCompanionVisual != null) aiCompanionVisual.Update(IsVisible ? currentCharacter : "", aiEffectSeconds, active, dragonFocus.Progress(now.ToUniversalTime()), bubbleEffectRemaining);
        }
        private void CancelAiAbilities()
        {
            dragonFocus.Cancel(); bubbleEffectRemaining = 0; aiCastRemaining = 0;
            if (dragonFocusHud != null) dragonFocusHud.Visibility = Visibility.Collapsed;
            if (aiCompanionVisual != null) aiCompanionVisual.Update("", 0, false, 0, 0);
            // Keep the draft in the hidden window when changing characters.
            // It remains available when DeepSeek is selected again.
            if (bubbleNotesWindow != null) bubbleNotesWindow.Hide();
        }
        private bool CanCloseAiAbilities()
        { return bubbleNotesWindow == null || bubbleNotesWindow.ConfirmDiscard(); }
        private void CloseAiAbilities()
        {
            CancelAiAbilities();
            if (bubbleNotesWindow != null) {
                // Main-window OnClosing already checked the draft. Avoid a
                // second question while Application is shutting down.
                bubbleNotesWindow.DiscardConfirmation = delegate { return true; };
                bubbleNotesWindow.Close();
            }
        }
        private BitmapSource PrepareAiAbilityFrame(BitmapSource requested)
        {
            BitmapSource[] poses;
            if (aiCastRemaining <= 0 || state != "idle" || movement != "rest" || pointerHeld ||
                (currentCharacter != "gpt6a" && currentCharacter != "deepseek") || !frames.TryGetValue("ability", out poses) || poses.Length == 0) return requested;
            int index = Math.Min(poses.Length - 1, (int)((2 - aiCastRemaining) / 2 * poses.Length));
            return poses[Math.Max(0, index)];
        }
        private void BeginAiCast()
        {
            aiCastRemaining = 2;
            // The first tap may still be playing its happy pose. Count casting
            // only while idle, but expire an interrupted sequence promptly.
            aiCastDeadlineUtc = DateTime.UtcNow.AddSeconds(6);
        }
    }
}
