using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.ViewModels;

/// <summary>Задача открытого листа в среднем поле.</summary>
public partial class TaskItemVM : ObservableObject
{
    public int TaskId { get; init; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isDone;

    /// <summary>Выделена для удаления кнопкой «–».</summary>
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _dateRange = string.Empty;

    /// <summary>Прогресс задания 0..100 % (по подзаданиям).</summary>
    [ObservableProperty]
    private int _progress;

    /// <summary>Первое невыполненное подзадание — показывается в середине плашки.</summary>
    public int? CurrentSubtaskId { get; set; }

    [ObservableProperty]
    private string _currentSubtaskText = string.Empty;

    /// <summary>Есть ли невыполненное подзадание (показывать ли строку с чек-боксом).</summary>
    [ObservableProperty]
    private bool _hasCurrentSubtask;

    /// <summary>Задание добавлено в Главный Лист (чек-бокс у Name в пилюле).</summary>
    [ObservableProperty]
    private bool _inDailyPlan;

    public string NameLine => $"Name: {Title}";
    public string ProgressText => $"POGRES: {Progress} %";

    partial void OnProgressChanged(int value) => OnPropertyChanged(nameof(ProgressText));
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(NameLine));
}
