using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MyDayQuest.Avalonia;

/// <summary>Простые модальные диалоги (ввод текста, подтверждение, сообщение).</summary>
public static class Dialogs
{
    public static async Task<string?> PromptAsync(Window owner, string title, string placeholder)
    {
        var box = new TextBox { Watermark = placeholder, Margin = new(0, 10) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90 };
        var cancel = new Button { Content = "Отмена", IsCancel = true, MinWidth = 90 };
        string? result = null;
        var dlg = BuildDialog(title, box, ok, cancel, out var close);
        ok.Click += (_, _) => { result = box.Text; close(); };
        cancel.Click += (_, _) => { result = null; close(); };
        await dlg.ShowDialog(owner);
        return result;
    }

    public static async Task<bool> ConfirmAsync(Window owner, string title, string message)
    {
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new(0, 10) };
        var yes = new Button { Content = "Да", IsDefault = true, MinWidth = 90 };
        var no = new Button { Content = "Отмена", IsCancel = true, MinWidth = 90 };
        var res = false;
        var dlg = BuildDialog(title, text, yes, no, out var close);
        yes.Click += (_, _) => { res = true; close(); };
        no.Click += (_, _) => { res = false; close(); };
        await dlg.ShowDialog(owner);
        return res;
    }

    public static async Task AlertAsync(Window owner, string title, string message)
    {
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new(0, 10) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right };
        var dlg = BuildDialog(title, text, ok, null, out var close);
        ok.Click += (_, _) => close();
        await dlg.ShowDialog(owner);
    }

    /// <summary>Выбор одного пункта из списка (заменяет ActionSheet из MAUI).</summary>
    public static async Task<string?> ChooseAsync(Window owner, string title, params string[] options)
    {
        var list = new ListBox { Margin = new(0, 10), MaxHeight = 260 };
        foreach (var option in options) list.Items.Add(option);

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90 };
        var cancel = new Button { Content = "Отмена", IsCancel = true, MinWidth = 90 };
        string? result = null;
        var dlg = BuildDialog(title, list, ok, cancel, out var close);
        list.DoubleTapped += (_, _) => { result = list.SelectedItem as string; close(); };
        ok.Click += (_, _) => { result = list.SelectedItem as string; close(); };
        cancel.Click += (_, _) => { result = null; close(); };
        await dlg.ShowDialog(owner);
        return result;
    }

    private static Window BuildDialog(string title, Control content, Button primary, Button? secondary, out System.Action close)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (secondary is not null) buttons.Children.Add(secondary);
        buttons.Children.Add(primary);

        var root = new StackPanel { Margin = new(16), Spacing = 6 };
        root.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 16 });
        root.Children.Add(content);
        root.Children.Add(buttons);

        var window = new Window
        {
            Title = title,
            Width = 340,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = root,
        };
        close = () => window.Close();
        return window;
    }
}
