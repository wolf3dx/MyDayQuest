using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyDayQuest.Data;
using MyDayQuest.Models;

namespace MyDayQuest.ViewModels;

/// <summary>Создание нового листа (Quest) с набором задач — кнопка «+».</summary>
public partial class QuestEditViewModel : ObservableObject
{
    private readonly AppDatabase _db;

    public QuestEditViewModel(AppDatabase db)
    {
        _db = db;
        Tasks.Add(new TaskInputRow());
    }

    [ObservableProperty]
    private string _questName = string.Empty;

    public ObservableCollection<TaskInputRow> Tasks { get; } = new();

    [RelayCommand]
    private void AddTaskRow() => Tasks.Add(new TaskInputRow());

    [RelayCommand]
    private void RemoveTaskRow(TaskInputRow row)
    {
        if (Tasks.Count > 1)
            Tasks.Remove(row);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(QuestName))
        {
            await Shell.Current.DisplayAlertAsync("Название", "Укажите название листа.", "OK");
            return;
        }

        var filled = Tasks.Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList();
        if (filled.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Задачи", "Добавьте хотя бы одну задачу.", "OK");
            return;
        }

        var order = await _db.CountQuestsAsync();
        var quest = new Quest { Name = QuestName.Trim(), SortOrder = order };
        var questId = await _db.SaveQuestAsync(quest);

        var tasks = filled.Select(t => new QuestTask
        {
            Title = t.Title.Trim(),
            StartDate = t.StartDate,
            Deadline = t.Deadline,
        });
        await _db.ReplaceTasksAsync(questId, tasks);

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
