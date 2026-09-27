using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyDayQuest.Data;
using MyDayQuest.Models;

namespace MyDayQuest.ViewModels;

[QueryProperty(nameof(TaskId), "taskId")]
public partial class TaskDetailViewModel : ObservableObject
{
    private readonly AppDatabase _db;

    public TaskDetailViewModel(AppDatabase db)
    {
        _db = db;
    }

    private int _taskId;
    public int TaskId
    {
        get => _taskId;
        set
        {
            _taskId = value;
            _ = LoadAsync();
        }
    }

    [ObservableProperty]
    private string _taskName = string.Empty;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _deadline = DateTime.Today.AddDays(1);

    public ObservableCollection<SubtaskVM> Subtasks { get; } = new();

    [ObservableProperty]
    private SubtaskVM? _selectedSubtask;

    [ObservableProperty]
    private int _progress;

    public string ProgressText => $"POGRES: {Progress} %";
    public string ProgressPercentText => $"{Progress} %";

    partial void OnProgressChanged(int value)
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressPercentText));
    }

    private async Task LoadAsync()
    {
        var task = await _db.GetTaskAsync(TaskId);
        if (task is null) return;

        TaskName = task.Title;
        StartDate = task.StartDate ?? DateTime.Today;
        Deadline = task.Deadline ?? DateTime.Today.AddDays(1);

        Subtasks.Clear();
        foreach (var s in await _db.GetSubtasksAsync(TaskId))
            Subtasks.Add(Track(new SubtaskVM { Id = s.Id, Title = s.Title, IsDone = s.IsDone }));

        RecalcProgress();
    }

    /// <summary>Подписка на переключение чек-бокса подзадания → пересчёт и сохранение прогресса.</summary>
    private SubtaskVM Track(SubtaskVM vm)
    {
        vm.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName != nameof(SubtaskVM.IsDone)) return;
            RecalcProgress();
            await _db.SaveSubtaskAsync(new Subtask
            {
                Id = vm.Id, TaskId = TaskId, Title = vm.Title, IsDone = vm.IsDone,
                SortOrder = Subtasks.IndexOf(vm),
            });
            await _db.RefreshTaskProgressAsync(TaskId);
        };
        return vm;
    }

    private void RecalcProgress()
    {
        Progress = Subtasks.Count == 0
            ? 0
            : (int)Math.Round(Subtasks.Count(s => s.IsDone) * 100.0 / Subtasks.Count);
    }

    [RelayCommand]
    private async Task AddSubtaskAsync()
    {
        var sub = new Subtask { TaskId = TaskId, Title = string.Empty, SortOrder = Subtasks.Count };
        await _db.SaveSubtaskAsync(sub);
        Subtasks.Add(Track(new SubtaskVM { Id = sub.Id, Title = string.Empty }));
        RecalcProgress();
    }

    [RelayCommand]
    private async Task RemoveSubtaskAsync()
    {
        if (SelectedSubtask is null)
        {
            await Shell.Current.DisplayAlertAsync("Удаление",
                "Сначала выберите подзадание.", "OK");
            return;
        }
        await _db.DeleteSubtaskAsync(SelectedSubtask.Id);
        Subtasks.Remove(SelectedSubtask);
        SelectedSubtask = null;
        RecalcProgress();
        await _db.RefreshTaskProgressAsync(TaskId);
    }

    [RelayCommand]
    private void SelectSubtask(SubtaskVM sub)
    {
        foreach (var s in Subtasks)
            s.IsSelected = ReferenceEquals(s, sub) && !s.IsSelected;
        SelectedSubtask = Subtasks.FirstOrDefault(s => s.IsSelected);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // название и даты задания
        var task = await _db.GetTaskAsync(TaskId);
        if (task is not null)
        {
            task.Title = string.IsNullOrWhiteSpace(TaskName) ? task.Title : TaskName.Trim();
            task.StartDate = StartDate;
            task.Deadline = Deadline;
            await _db.SaveTaskAsync(task);
        }

        // тексты подзаданий
        var order = 0;
        foreach (var s in Subtasks)
        {
            await _db.SaveSubtaskAsync(new Subtask
            {
                Id = s.Id, TaskId = TaskId, Title = s.Title.Trim(), IsDone = s.IsDone,
                SortOrder = order++,
            });
        }

        await _db.RefreshTaskProgressAsync(TaskId);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
