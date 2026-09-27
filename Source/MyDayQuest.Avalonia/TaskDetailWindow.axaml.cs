using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MyDayQuest.Avalonia.ViewModels;

namespace MyDayQuest.Avalonia;

public partial class TaskDetailWindow : Window
{
    private readonly TaskDetailViewModel _vm;

    public TaskDetailWindow(TaskDetailViewModel vm)
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = _vm = vm;
        Opened += async (_, _) => await _vm.LoadAsync();
    }

    private void OnSubtaskTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { Tag: SubtaskRowVM row })
        {
            foreach (var s in _vm.Subtasks) s.IsSelected = ReferenceEquals(s, row) && !s.IsSelected;
            _vm.SelectedSubtask = _vm.Subtasks.FirstOrDefault(s => s.IsSelected);
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        await _vm.SaveAsync();
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
