using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.ViewModels;

/// <summary>Плашка задания в Главном Листе (дневном плане).</summary>
public partial class DailyTaskVM : ObservableObject
{
    public int TaskId { get; init; }

    /// <summary>Имя листа-источника (верхняя область плашки).</summary>
    public string QuestName { get; init; } = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _currentSubtaskText = string.Empty;

    [ObservableProperty]
    private bool _hasCurrentSubtask;

    [ObservableProperty]
    private string _dateRange = string.Empty;

    [ObservableProperty]
    private int _progress;

    public int? CurrentSubtaskId { get; set; }

    public string ListLine => $"NAME LIST: {QuestName}";
    public string NameLine => $"Name: {Title}";
    public string ProgressText => $"POGRES: {Progress} %";

    partial void OnProgressChanged(int value) => OnPropertyChanged(nameof(ProgressText));
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(NameLine));
}
