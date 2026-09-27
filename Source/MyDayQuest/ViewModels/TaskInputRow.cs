using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.ViewModels;

/// <summary>Строка ввода задачи на экране создания листа.</summary>
public partial class TaskInputRow : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _deadline = DateTime.Today.AddDays(1);
}
