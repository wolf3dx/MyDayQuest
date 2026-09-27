using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyDayQuest.Data;
using MyDayQuest.Models;

namespace MyDayQuest.Avalonia.ViewModels;

public partial class TaskDetailViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly int _taskId;

    public TaskDetailViewModel(AppDatabase db, int taskId)
    {
        _db = db;
        _taskId = taskId;
    }

    [ObservableProperty] private string _taskName = string.Empty;
    [ObservableProperty] private DateTimeOffset _startDate = DateTimeOffset.Now;
    [ObservableProperty] private DateTimeOffset _deadline = DateTimeOffset.Now.AddDays(1);
    [ObservableProperty] private int _progress;
    [ObservableProperty] private SubtaskRowVM? _selectedSubtask;

    public ObservableCollection<SubtaskRowVM> Subtasks { get; } = new();

    public string ProgressPercentText => $"{Progress} %";
    partial void OnProgressChanged(int v) => OnPropertyChanged(nameof(ProgressPercentText));

    public async Task LoadAsync()
    {
        var task = await _db.GetTaskAsync(_taskId);
        if (task is null) return;
        TaskName = task.Title;
        StartDate = new DateTimeOffset(task.StartDate ?? DateTime.Today);
        Deadline = new DateTimeOffset(task.Deadline ?? DateTime.Today.AddDays(1));
        Subtasks.Clear();
        foreach (var s in await _db.GetSubtasksAsync(_taskId))
            Subtasks.Add(Track(new SubtaskRowVM { Id = s.Id, Title = s.Title, IsDone = s.IsDone }));
        Recalc();
    }

    private SubtaskRowVM Track(SubtaskRowVM vm)
    {
        vm.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName != nameof(SubtaskRowVM.IsDone)) return;
            Recalc();
            await _db.SaveSubtaskAsync(new Subtask { Id = vm.Id, TaskId = _taskId, Title = vm.Title, IsDone = vm.IsDone, SortOrder = Subtasks.IndexOf(vm) });
            await _db.RefreshTaskProgressAsync(_taskId);
        };
        return vm;
    }

    private void Recalc()
        => Progress = Subtasks.Count == 0 ? 0 : (int)Math.Round(Subtasks.Count(s => s.IsDone) * 100.0 / Subtasks.Count);

    [RelayCommand]
    private async Task AddSubtaskAsync()
    {
        var sub = new Subtask { TaskId = _taskId, Title = string.Empty, SortOrder = Subtasks.Count };
        await _db.SaveSubtaskAsync(sub);
        Subtasks.Add(Track(new SubtaskRowVM { Id = sub.Id, Title = string.Empty }));
        Recalc();
    }

    [RelayCommand]
    private async Task RemoveSubtaskAsync()
    {
        if (SelectedSubtask is null) return;
        await _db.DeleteSubtaskAsync(SelectedSubtask.Id);
        Subtasks.Remove(SelectedSubtask);
        SelectedSubtask = null;
        Recalc();
        await _db.RefreshTaskProgressAsync(_taskId);
    }

    public async Task SaveAsync()
    {
        var task = await _db.GetTaskAsync(_taskId);
        if (task is not null)
        {
            task.Title = TaskName.Trim();
            task.StartDate = StartDate.DateTime;
            task.Deadline = Deadline.DateTime;
            await _db.SaveTaskAsync(task);
        }
        var order = 0;
        foreach (var s in Subtasks)
            await _db.SaveSubtaskAsync(new Subtask { Id = s.Id, TaskId = _taskId, Title = s.Title.Trim(), IsDone = s.IsDone, SortOrder = order++ });
        await _db.RefreshTaskProgressAsync(_taskId);
    }
}
