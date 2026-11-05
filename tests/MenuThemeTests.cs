using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudePet;

internal static class MenuThemeTests
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Private).GetValue(owner); }
    private static void Set(object owner, string name, object value) { owner.GetType().GetField(name, Private).SetValue(owner, value); }
    private static object Call(object owner, string name, params object[] args) { return owner.GetType().GetMethod(name, Private).Invoke(owner, args); }
    private static void Require(bool good, string why) { if (!good) throw new Exception(why); }
    private static Color ColorOf(Brush brush) { return ((SolidColorBrush)brush).Color; }
    private static double Channel(byte v) { double x = v / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
    private static double Light(Color c) { return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B); }
    private static double Contrast(Brush a, Brush b) { double x = Light(ColorOf(a)), y = Light(ColorOf(b)); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
    private static void Layout(FrameworkElement element, double width)
    {
        WpfTestRender.Drain();
        element.Measure(new Size(width, 2000));
        element.Arrange(new Rect(new Point(), element.DesiredSize));
        element.UpdateLayout();
    }
    private static RenderTargetBitmap Render(FrameworkElement element)
    {
        Require(element.ActualWidth > 0 && element.ActualHeight > 0, "Empty layout: " + element.GetType().Name);
        RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        WpfTestRender.Render(bitmap, element); return bitmap;
    }
    private static MenuItem Find(ContextMenu menu, string label)
    {
        foreach (object value in menu.Items) { MenuItem item = value as MenuItem; if (item != null && Convert.ToString(item.Header) == label) return item; }
        throw new Exception("Missing menu item: " + label);
    }
    private static Border Row(MenuItem item) { item.ApplyTemplate(); return (Border)item.Template.FindName("Row", item); }
    private static FrameworkElement Submenu(MenuItem item)
    {
        item.ApplyTemplate();
        Popup popup = (Popup)item.Template.FindName("PART_Popup", item);
        Require(popup != null, "Missing production Popup");
        FrameworkElement child = (FrameworkElement)popup.Child;
        Layout(child, 228);
        return child;
    }
    private static void Save(Visual visual, int width, int height, string path)
    {
        RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); WpfTestRender.Render(bitmap, visual);
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(path)) encoder.Save(stream);
    }

    [STAThread]
    private static void Main()
    {
        PetWindow pet = null;
        try
        {
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            pet = new PetWindow();
            Field<DispatcherTimer>(pet, "timer").Stop();
            string[] characters = { "kolo", "hutao", "ganyu", "varesa", "gpt6a", "deepseek" };
            string[] names = { "珂洛", "胡桃", "甘雨", "瓦雷莎", "GPT6A·龙女", "DeepSeek·小肥鱼" };
            HashSet<Color> backgrounds = new HashSet<Color>();
            for (int index = 0; index < characters.Length; index++)
            {
                string character = characters[index];
                Set(pet, "currentCharacter", character);
                Set(pet, "autoWalk", false);
                Set(pet, "preferredGait", "stroll");
                ContextMenu menu = (ContextMenu)Call(pet, "BuildMenu");
                pet.ContextMenu = menu;
                menu.ApplyTemplate(); Layout(menu, menu.Width);
                Require(menu.ActualWidth == 280 && menu.ActualHeight < 760, "Menu is oversized: " + menu.ActualHeight);
                Require(backgrounds.Add(ColorOf(menu.Background)), "Character reused another palette");
                Require(Contrast(menu.Foreground, menu.Background) >= 4.5, "Root text contrast");
                Require(Field<TextBlock>(pet, "menuCharacterText").Text.Contains(names[index] + "  ·  V" + typeof(PetWindow).Assembly.GetName().Version.ToString(2)), "Wrong header character/version");
                Require(!Find(menu, "自动移动").IsChecked, "Rebuild reset autoWalk");
                Find(menu, "每日看点"); Find(menu, "战斗循环助手");
                Require(Field<List<MenuItem>>(pet, "characterItems").Count == 6 && Field<List<MenuItem>>(pet, "movementItems").Count == (character == "kolo" ? 1 : 3), "Rebuild duplicated check lists");
                Find(menu, "慢慢走一会儿"); Find(menu, "移动模式");
                if (character != "kolo") Find(menu, character == "hutao" ? "化蝶飞行一会儿" : "立即跑步");
                foreach (object entry in menu.Items)
                {
                    MenuItem item = entry as MenuItem;
                    if (item == null) continue;
                    string label = Convert.ToString(item.Header);
                    Require(label != "互动" && label != "摸摸她" && label != "让她开心" && label != "送她玫瑰" && label != "陪我工作",
                        "Removed interaction section is still present for " + character);
                }
                MenuItem movementMenu = Find(menu, "移动模式");
                if (character != "kolo")
                    Require(Convert.ToString(((MenuItem)movementMenu.Items[1]).Header) == (character == "hutao" ? "金蝶飞行" : "活力奔跑"), "Character-specific fast mode label");

                List<RenderTargetBitmap> submenus = new List<RenderTargetBitmap>();
                foreach (string label in new[] { "选择形象", "移动模式", "调整大小" })
                {
                    MenuItem parent = Find(menu, label);
                    Border surface = (Border)Submenu(parent);
                    Require(ColorOf(surface.Background) == ColorOf(menu.Background), "Submenu native surface leak");
                    Require(surface.ActualWidth >= 228 && surface.ActualWidth <= 280, "Submenu width");
                    for (int i = 0; i < parent.Items.Count; i++)
                    {
                        MenuItem child = (MenuItem)parent.Items[i];
                        Require(Row(child) != null, "Submenu item used native template");
                        Require(Contrast(child.Foreground, surface.Background) >= 4.5, "Submenu text contrast");
                        Require(ColorOf(((TextBlock)child.Icon).Foreground) == ColorOf(child.Foreground), "Icon did not inherit palette");
                        if (child.IsChecked)
                        {
                            TextBlock check = (TextBlock)child.Template.FindName("Check", child);
                            Require(check.Visibility == Visibility.Visible, "Checked glyph missing");
                            Require(Contrast(child.Foreground, Row(child).Background) >= 4.5, "Checked text contrast");
                        }
                    }
                    // Exercise the actual production trigger rather than a separate preview style.
                    MenuItem hovered = (MenuItem)parent.Items[parent.Items.Count - 1];
                    typeof(MenuItem).GetProperty("IsHighlighted").GetSetMethod(true).Invoke(hovered, new object[] { true });
                    surface.UpdateLayout();
                    Require(ColorOf(Row(hovered).Background) == ColorOf((Brush)menu.Resources["PetMenuHover"]), "Hover trigger color");
                    Require(Contrast(hovered.Foreground, Row(hovered).Background) >= 4.5, "Hover text contrast");
                    hovered.IsEnabled = false;
                    surface.UpdateLayout();
                    Require(ColorOf(hovered.Foreground) == ColorOf((Brush)menu.Resources["PetMenuDisabled"]), "Disabled color");
                    Require(Contrast(hovered.Foreground, surface.Background) >= 3, "Disabled text contrast");
                    Require(ColorOf(((TextBlock)hovered.Icon).Foreground) == ColorOf(hovered.Foreground), "Disabled icon color");
                    hovered.IsEnabled = true;
                    surface.UpdateLayout();
                    submenus.Add(Render(surface));
                }
                foreach (string mode in new[] { "stroll", "run", "mixed" })
                {
                    Set(pet, "preferredGait", mode); Call(pet, "RefreshMovementChecks");
                    int checkedCount = 0;
                    foreach (MenuItem option in Field<List<MenuItem>>(pet, "movementItems"))
                    {
                        if (option.IsChecked) checkedCount++;
                        Require(option.IsChecked == (Convert.ToString(option.Tag) == (character == "kolo" ? "stroll" : mode)), "Wrong movement check");
                    }
                    Require(checkedCount == 1, "Movement menu must have one checked option");
                }
                if (character == "kolo") {
                    Require(Field<List<MenuItem>>(pet, "movementItems").Count == 1, "Kolo still exposes running/mixed modes.");
                    foreach (object value in menu.Items) {
                        MenuItem option = value as MenuItem;
                        Require(option == null || Convert.ToString(option.Header) != "立即跑步", "Kolo still exposes the run command.");
                    }
                }
                Set(pet, "preferredGait", "stroll"); Call(pet, "RefreshMovementChecks");
                RenderTargetBitmap root = Render(menu);
                DrawingVisual montage = new DrawingVisual();
                using (DrawingContext drawing = montage.RenderOpen())
                {
                    drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(228, 231, 236)), null, new Rect(0, 0, 540, root.PixelHeight + 24));
                    drawing.DrawImage(root, new Rect(12, 12, root.PixelWidth, root.PixelHeight));
                    int y = 12;
                    foreach (RenderTargetBitmap submenu in submenus)
                    {
                        drawing.DrawImage(submenu, new Rect(300, y, submenu.PixelWidth, submenu.PixelHeight)); y += submenu.PixelHeight + 18;
                    }
                }
                Save(montage, 540, root.PixelHeight + 24, "artifacts/tests/menu-theme-" + character + ".png");
                Console.WriteLine("PASS " + character + ": root + 3 actual submenu renders, checked/highlight/disabled colors, contrast, compact geometry, rebuilt state.");
            }
            Console.WriteLine("PASS all six menu themes; no settings modified.");
        }
        catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
        finally { if (pet != null) pet.Close(); }
    }
}
