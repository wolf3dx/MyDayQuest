using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.Avalonia.ViewModels;

/// <summary>Вкладка листа в нижней ленте.</summary>
public partial class QuestTabVM : ObservableObject
{
    public int QuestId { get; init; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isDropTarget;
    public string ColorHex { get; init; } = "#EDEDED";
}

/// <summary>Плашка задания открытого листа.</summary>
public partial class TaskCardVM : ObservableObject
{
    public int TaskId { get; init; }
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _inDailyPlan;
    [ObservableProperty] private string _dateRange = string.Empty;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _currentSubtaskText = string.Empty;
    [ObservableProperty] private bool _hasCurrentSubtask;
    public int? CurrentSubtaskId { get; set; }

    public string NameLine => $"Name: {Title}";
    public string ProgressText => $"POGRES: {Progress} %";
    partial void OnProgressChanged(int v) => OnPropertyChanged(nameof(ProgressText));
    partial void OnTitleChanged(string v) => OnPropertyChanged(nameof(NameLine));
}

/// <summary>Плашка Главного окна (дневной план).</summary>
public partial class DailyCardVM : ObservableObject
{
    public int TaskId { get; init; }
    public string QuestName { get; init; } = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _dateRange = string.Empty;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _currentSubtaskText = string.Empty;
    [ObservableProperty] private bool _hasCurrentSubtask;
    public int? CurrentSubtaskId { get; set; }

    public string ListLine => $"NAME LIST: {QuestName}";
    public string NameLine => $"Name: {Title}";
    public string ProgressText => $"POGRES: {Progress} %";
    partial void OnProgressChanged(int v) => OnPropertyChanged(nameof(ProgressText));
    partial void OnTitleChanged(string v) => OnPropertyChanged(nameof(NameLine));
}

/// <summary>Строка подзадания на экране деталей.</summary>
public partial class SubtaskRowVM : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isDropTarget;

    /// <summary>Чем обвести строку: 2 — сюда метят переносом, 1 — выделена, 0 — обычная.</summary>
    public int Accent => IsDropTarget ? 2 : IsSelected ? 1 : 0;

    partial void OnIsDropTargetChanged(bool v) => OnPropertyChanged(nameof(Accent));
    partial void OnIsSelectedChanged(bool v) => OnPropertyChanged(nameof(Accent));
}
