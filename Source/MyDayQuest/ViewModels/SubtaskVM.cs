using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.ViewModels;

/// <summary>Подзадание (строка) на экране деталей задания.</summary>
public partial class SubtaskVM : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isDone;

    /// <summary>Выделено для удаления кнопкой «–».</summary>
    [ObservableProperty]
    private bool _isSelected;
}
