using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.ViewModels;

/// <summary>
/// Плашка на главном экране: название листа + «задание на сегодня»,
/// его даты и прогресс. Один визуальный блок дневного плана.
/// </summary>
public partial class QuestCard : ObservableObject
{
    public int QuestId { get; init; }

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Текст текущей задачи («задание на сегодня»).</summary>
    [ObservableProperty]
    private string _currentTaskTitle = string.Empty;

    /// <summary>Есть ли ещё невыполненная задача в листе.</summary>
    [ObservableProperty]
    private bool _hasTask;

    /// <summary>Отметка «сделано на сегодня» (чек-бокс плашки).</summary>
    [ObservableProperty]
    private bool _isDoneToday;

    [ObservableProperty]
    private string _dateRange = string.Empty;

    /// <summary>Прогресс листа в %, 0..100.</summary>
    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private bool _isSelected;

    public int? CurrentTaskId { get; set; }

    public string ProgressText => $"PROGRES: {Progress} %";

    partial void OnProgressChanged(int value) => OnPropertyChanged(nameof(ProgressText));
}
