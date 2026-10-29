using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace ClaudePet
{
    public sealed partial class PetWindow : Window
    {
        private readonly List<MenuItem> movementItems = new List<MenuItem>();
        private MenuPalette menuPalette;

        internal sealed class MenuPalette
        {
            internal string Surface, Text, Muted, Accent, Border, Hover, Checked, Disabled, Exit;
            internal MenuPalette(string surface, string text, string muted, string accent, string border,
                string hover, string selected, string disabled, string exit)
            {
                Surface = surface; Text = text; Muted = muted; Accent = accent; Border = border;
                Hover = hover; Checked = selected; Disabled = disabled; Exit = exit;
            }
            internal static MenuPalette ForCharacter(string character)
            {
                switch (character)
                {
                    case "hutao": return new MenuPalette("#221A20", "#FFF0ED", "#CDB3B9", "#FF9B9F", "#984D5D", "#713341", "#492831", "#A58D94", "#FFADB0");
                    case "ganyu": return new MenuPalette("#F4FAFF", "#193F5D", "#496983", "#286694", "#A7CBE3", "#D1E9F9", "#E0EFFB", "#718BA0", "#9E3C56");
                    case "varesa": return new MenuPalette("#30203F", "#FFF1FC", "#D0B4DD", "#FFB3DE", "#9C6EB4", "#674075", "#4D2E60", "#A78AB8", "#FFB4D1");
                    default: return new MenuPalette("#32251E", "#FFF2DF", "#D9BDA3", "#FFC176", "#B87846", "#745033", "#533B29", "#AD9279", "#FFAD99");
                }
            }
        }

        private static SolidColorBrush MenuBrush(string color)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        private ContextMenu BuildMenu()
        {
            characterItems.Clear();
            movementItems.Clear();
            menuPalette = MenuPalette.ForCharacter(currentCharacter);
            ContextMenu menu = new ContextMenu();
            menu.Width = 280;
            menu.MaxHeight = Math.Max(250, SystemParameters.WorkArea.Height - 24);
            menu.Opened += delegate {
                menu.MaxHeight = Math.Max(250, GetWorkAreaForPoint(new Point(CenterX, Top + Height / 2)).Height - 24);
            };
            menu.FontFamily = new FontFamily("Microsoft YaHei UI");
            menu.FontSize = 13;
            menu.Foreground = MenuBrush(menuPalette.Text);
            menu.Background = MenuBrush(menuPalette.Surface);
            menu.BorderBrush = MenuBrush(menuPalette.Border);
            menu.BorderThickness = new Thickness(1);
            menu.Padding = new Thickness(6);
            menu.HasDropShadow = true;
            menu.SnapsToDevicePixels = true;
            ConfigureMenuStyles(menu, menuPalette);

            menu.Items.Add(MenuHeader());
            menu.Items.Add(SectionLabel("形象"));
            MenuItem characters = StyledItem("✦", "选择形象", null);
            for (int i = 0; i < CharacterOrder.Length; i++)
            {
                string key = CharacterOrder[i];
                string icon = key == "hutao" ? "✿" : key == "ganyu" ? "❄" : key == "varesa" ? "⚡" : "●";
                MenuItem characterItem = StyledItem(icon, CharacterNames[key], delegate { SwitchCharacter(key); });
                characterItem.IsCheckable = true;
                characterItem.IsChecked = key == currentCharacter;
                characterItem.Tag = key;
                characterItems.Add(characterItem);
                characters.Items.Add(characterItem);
            }
            menu.Items.Add(characters);

            menu.Items.Add(SectionLabel("工具"));
            menu.Items.Add(StyledItem("◈", "每日看点", OpenHighlights));
            menu.Items.Add(StyledItem("⚔", "战斗循环助手", OpenCombatPlanner));
            menu.Items.Add(StyledItem("◷", "GPT 重置预测", OpenGptResetPrediction));

            menu.Items.Add(SectionLabel("动作与大小"));
            menu.Items.Add(StyledItem("♧", "慢慢走一会儿", delegate { if (state == "idle") StartStrolling(); }));
            menu.Items.Add(StyledItem("➜", currentCharacter == "hutao" ? "化蝶飞行一会儿" : "立即跑步", delegate { if (state == "idle") StartWalking(); }));
            if (currentCharacter == "ganyu")
                menu.Items.Add(StyledItem("↗", "蹬墙跳上附近窗口", delegate { if (state == "idle") StartGanyuJump(); }));
            MenuItem gait = StyledItem("⇄", "移动模式", null);
            string[] modes = { "stroll", "run", "mixed" };
            string[] labels = { "慢速行走", currentCharacter == "hutao" ? "金蝶飞行" : "活力奔跑",
                currentCharacter == "hutao" ? "走飞交替" : "走跑交替" };
            for (int i = 0; i < modes.Length; i++)
            {
                string mode = modes[i];
                MenuItem option = StyledItem(i == 0 ? "♧" : i == 1 ? "➜" : "⇄", labels[i], delegate { ApplyMovementMode(mode); });
                option.Tag = mode;
                option.IsCheckable = true;
                movementItems.Add(option);
                gait.Items.Add(option);
            }
            RefreshMovementChecks();
            menu.Items.Add(gait);

            MenuItem quietItem = StyledItem("☾", "安静模式（少跑动）", null);
            quietItem.IsCheckable = true;
            quietItem.IsChecked = quietMode;
            quietItem.Click += delegate {
                quietMode = quietItem.IsChecked;
                PetSettings.SaveQuietMode(quietMode);
                if (quietMode) CancelAutomaticTraversal();
                else ScheduleNextWalk(DateTime.Now);
                ShowSpeech(quietMode ? "安静模式已开启，我会少跑动。" : "安静模式已关闭。活力回来啦！");
            };
            menu.Items.Add(quietItem);

            MenuItem autoItem = StyledItem("↻", "自动移动", null);
            autoItem.IsCheckable = true;
            autoItem.IsChecked = autoWalk;
            autoItem.Click += delegate {
                autoWalk = autoItem.IsChecked;
                if (!autoWalk) CancelAutomaticTraversal();
                if (autoWalk) ScheduleNextWalk(DateTime.Now);
            };
            menu.Items.Add(autoItem);

            MenuItem size = StyledItem("⌘", "调整大小", null);
            size.Items.Add(StyledItem("＋", "放大", delegate { ResizePet(1.08, false); }));
            size.Items.Add(StyledItem("－", "缩小", delegate { ResizePet(0.92, false); }));
            size.Items.Add(StyledItem("○", "恢复默认", delegate { ResizePet(1, true); }));
            menu.Items.Add(size);

            menu.Items.Add(new Separator());
            MenuItem exit = StyledItem("×", "退出桌宠", delegate { Close(); });
            exit.Foreground = MenuBrush(menuPalette.Exit);
            menu.Items.Add(exit);
            return menu;
        }

        private void RefreshMovementChecks()
        {
            foreach (MenuItem item in movementItems)
                item.IsChecked = Convert.ToString(item.Tag) == preferredGait;
        }

        private static void ConfigureMenuStyles(ContextMenu menu, MenuPalette palette)
        {
            // Own complete templates: Windows native submenu/highlight resources otherwise
            // override individual setters, especially in the light Ganyu palette.
            const string xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <SolidColorBrush x:Key='PetMenuSurface' Color='{SURFACE}'/>
  <SolidColorBrush x:Key='PetMenuText' Color='{TEXT}'/>
  <SolidColorBrush x:Key='PetMenuBorder' Color='{BORDER}'/>
  <SolidColorBrush x:Key='PetMenuHover' Color='{HOVER}'/>
  <SolidColorBrush x:Key='PetMenuChecked' Color='{CHECKED}'/>
  <SolidColorBrush x:Key='PetMenuDisabled' Color='{DISABLED}'/>
  <ControlTemplate x:Key='PetMenuTemplate' TargetType='{x:Type ContextMenu}'>
    <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='11' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'>
      <ScrollViewer CanContentScroll='True' HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto'>
        <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/>
      </ScrollViewer>
    </Border>
  </ControlTemplate>
  <ControlTemplate x:Key='PetMenuLabelTemplate' TargetType='{x:Type MenuItem}'>
    <ContentPresenter ContentSource='Header' Margin='{TemplateBinding Padding}'/>
  </ControlTemplate>
  <Style TargetType='{x:Type MenuItem}'>
    <Setter Property='Foreground' Value='{StaticResource PetMenuText}'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Padding' Value='8,5,8,5'/>
    <Setter Property='Margin' Value='1,1,1,1'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type MenuItem}'>
          <Grid>
            <Border x:Name='Row' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' CornerRadius='6'>
              <Grid>
                <Grid.ColumnDefinitions><ColumnDefinition Width='18'/><ColumnDefinition Width='24'/><ColumnDefinition Width='*'/><ColumnDefinition Width='15'/></Grid.ColumnDefinitions>
                <TextBlock x:Name='Check' Text='✓' Visibility='Hidden' VerticalAlignment='Center' FontSize='13' FontWeight='Bold'/>
                <ContentPresenter Grid.Column='1' ContentSource='Icon' VerticalAlignment='Center' HorizontalAlignment='Center'/>
                <ContentPresenter Grid.Column='2' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center' Margin='2,0,5,0'/>
                <TextBlock x:Name='Arrow' Grid.Column='3' Text='›' Visibility='Hidden' VerticalAlignment='Center' HorizontalAlignment='Right' FontSize='18'/>
              </Grid>
            </Border>
            <Popup x:Name='PART_Popup' Placement='Right' HorizontalOffset='2' AllowsTransparency='True' Focusable='False' IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}' PopupAnimation='Fade'>
              <Border x:Name='SubmenuSurface' MinWidth='228' MaxHeight='560' Background='{StaticResource PetMenuSurface}' BorderBrush='{StaticResource PetMenuBorder}' BorderThickness='1' CornerRadius='9' Padding='5'>
                <ScrollViewer CanContentScroll='True' HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto'>
                  <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle' KeyboardNavigation.TabNavigation='Cycle'/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'><Setter TargetName='Check' Property='Visibility' Value='Visible'/><Setter TargetName='Row' Property='Background' Value='{StaticResource PetMenuChecked}'/></Trigger>
            <Trigger Property='HasItems' Value='True'><Setter TargetName='Arrow' Property='Visibility' Value='Visible'/></Trigger>
            <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Row' Property='Background' Value='{StaticResource PetMenuHover}'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Row' Property='Background' Value='{StaticResource PetMenuHover}'/></Trigger>
            <Trigger Property='IsSubmenuOpen' Value='True'><Setter TargetName='Row' Property='Background' Value='{StaticResource PetMenuHover}'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Foreground' Value='{StaticResource PetMenuDisabled}'/><Setter TargetName='Row' Property='Background' Value='Transparent'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='{x:Type Separator}'>
    <Setter Property='Margin' Value='12,6,12,6'/><Setter Property='Height' Value='1'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Separator}'><Border Background='{StaticResource PetMenuBorder}'/></ControlTemplate></Setter.Value></Setter>
  </Style>
</ResourceDictionary>";
            ResourceDictionary resources = (ResourceDictionary)XamlReader.Parse(xaml.Replace("{SURFACE}", palette.Surface)
                .Replace("{TEXT}", palette.Text).Replace("{BORDER}", palette.Border).Replace("{HOVER}", palette.Hover)
                .Replace("{CHECKED}", palette.Checked).Replace("{DISABLED}", palette.Disabled));
            menu.Resources = resources;
            menu.Template = (ControlTemplate)resources["PetMenuTemplate"];
        }

        private MenuItem MenuHeader()
        {
            MenuItem item = new MenuItem { IsHitTestVisible = false, Focusable = false, Padding = new Thickness(10, 7, 10, 8) };
            item.SetResourceReference(Control.TemplateProperty, "PetMenuLabelTemplate");
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            string markText = currentCharacter == "hutao" ? "✿" : currentCharacter == "ganyu" ? "❄" : currentCharacter == "varesa" ? "⚡" : "✦";
            Border mark = new Border {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = MenuBrush(menuPalette.Checked),
                BorderBrush = MenuBrush(menuPalette.Border), BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = markText, Foreground = MenuBrush(menuPalette.Accent), FontSize = 17,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            grid.Children.Add(mark);
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            text.Children.Add(new TextBlock { Text = "原神启动", Foreground = MenuBrush(menuPalette.Text), FontSize = 15, FontWeight = FontWeights.Bold });
            menuCharacterText = new TextBlock {
                Text = "当前：" + CharacterNames[currentCharacter] + "  ·  V" + typeof(PetWindow).Assembly.GetName().Version.ToString(2),
                Foreground = MenuBrush(menuPalette.Muted), FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0)
            };
            text.Children.Add(menuCharacterText);
            grid.Children.Add(text);
            item.Header = grid;
            return item;
        }

        private MenuItem SectionLabel(string text)
        {
            MenuItem item = new MenuItem {
                Header = text, IsHitTestVisible = false, Focusable = false, Foreground = MenuBrush(menuPalette.Accent),
                FontSize = 10.5, FontWeight = FontWeights.Bold, Padding = new Thickness(12, 6, 8, 2)
            };
            item.SetResourceReference(Control.TemplateProperty, "PetMenuLabelTemplate");
            return item;
        }

        private static MenuItem StyledItem(string icon, string text, Action action)
        {
            MenuItem item = new MenuItem { Header = text };
            TextBlock glyph = new TextBlock { Text = icon, Width = 21, FontSize = 14, TextAlignment = TextAlignment.Center };
            // Follow the actual item's disabled/exit colors even in nested popups.
            glyph.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = item });
            item.Icon = glyph;
            if (action != null) item.Click += delegate { action(); };
            return item;
        }
    }
}
