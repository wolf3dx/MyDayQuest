using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyDayQuest.Cloud;
using MyDayQuest.Data;
using MyDayQuest.Models;

namespace MyDayQuest.Avalonia.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly SyncService _sync;
    private readonly UpdateService _update;
    private readonly CloudSyncManager _cloud;
    private UpdateInfo? _pendingUpdate;

    private static readonly string[] Palette =
    {
        "#EBDCB8", "#E0CDA0", "#D5BE8C", "#F0E3C4", "#DCC79C", "#E6D5AE", "#CBB183",
    };

    // Взаимодействия с UI (устанавливает View).
    public Func<string, string, Task<string?>>? PromptText;
    public Func<string, string, Task<bool>>? Confirm;
    public Func<string, Task>? Alert;
    public Func<int, Task>? OpenTaskDetail;

    public MainViewModel(AppDatabase db, SyncService sync, UpdateService update, CloudSyncManager cloud)
    {
        _db = db;
        _sync = sync;
        _update = update;
        _cloud = cloud;

        _cloudStatus = _cloud.StatusText;
        _cloud.StatusChanged += (_, _) =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => CloudStatus = _cloud.StatusText);
        _cloud.PulledFromCloud += (_, _) =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                Collapse();
                await LoadAsync();
            });
    }

    // ---- Облако ----

    /// <summary>Строка состояния под кнопками шапки.</summary>
    [ObservableProperty]
    private string _cloudStatus = string.Empty;

    public bool IsCloudEnabled => _cloud.IsEnabled;
    public string CloudStatusText => _cloud.StatusText;
    public string? CloudLocation => _cloud.Storage?.RemoteLocation;
    public bool IsCloudConfigured(CloudProvider provider) => _cloud.IsConfigured(provider);

    /// <summary>Поднять фоновую синхронизацию при открытии окна.</summary>
    public async Task StartCloudSyncAsync()
    {
        _cloud.Start();
        if (_cloud.IsEnabled)
            await _cloud.SyncNowAsync(force: true);
        CloudStatus = _cloud.StatusText;
    }

    public async Task<CloudSyncReport> ConnectCloudAsync(CloudProvider provider)
    {
        var report = await _cloud.ConnectAsync(provider);
        CloudStatus = _cloud.StatusText;
        if (report.Outcome == CloudSyncOutcome.Pulled)
        {
            Collapse();
            await LoadAsync();
        }
        return report;
    }

    public async Task<CloudSyncReport> SyncCloudNowAsync()
    {
        var report = await _cloud.SyncNowAsync(force: true);
        CloudStatus = _cloud.StatusText;
        return report;
    }

    public void DisconnectCloud()
    {
        _cloud.Disconnect();
        CloudStatus = _cloud.StatusText;
    }

    // Взаимодействия для обновления (устанавливает View).
    public Func<string, string, Task<bool>>? ConfirmUpdate;
    public Func<string, Task>? OpenUrl;

    /// <summary>Проверить GitLab Releases на новую версию.</summary>
    public async Task CheckUpdateAsync()
    {
        _pendingUpdate = await _update.CheckAsync(AppVersion.Current);
        IsUpdateAvailable = _pendingUpdate is not null;
    }

    /// <summary>Клик по иконке обновления — скачать и применить (Linux) либо открыть релиз.</summary>
    public async Task UpdateAppAsync()
    {
        if (_pendingUpdate is null) return;
        var up = _pendingUpdate;
        if (ConfirmUpdate is not null && !await ConfirmUpdate("Обновление", $"Доступна версия {up.Version}. Обновить сейчас?"))
            return;

        try
        {
            if (OperatingSystem.IsLinux())
                await ApplyLinuxAsync(up);
            else if (OpenUrl is not null)
                await OpenUrl(up.ReleaseUrl); // Windows/macOS Avalonia: открыть страницу релиза
        }
        catch (Exception ex)
        {
            if (Alert is not null) await Alert("Ошибка обновления: " + ex.Message);
        }
    }

    private async Task ApplyLinuxAsync(UpdateInfo up)
    {
        var url = UpdateService.RawAssetUrl(up.Tag, "MyDayQuest-linux-x64.tar.gz");
        var tmp = Path.Combine(Path.GetTempPath(), "mdq_update");
        Directory.CreateDirectory(tmp);
        var tar = Path.Combine(tmp, "update.tar.gz");
        await UpdateService.DownloadAsync(url, tar);

        var appDir = AppContext.BaseDirectory.TrimEnd('/');
        var exe = Path.Combine(appDir, "MyDayQuest.Avalonia");
        var sh = Path.Combine(tmp, "apply-update.sh");
        await File.WriteAllTextAsync(sh,
            "#!/bin/sh\n" +
            "sleep 2\n" +
            $"tar -xzf '{tar}' -C '{appDir}'\n" +
            $"chmod +x '{exe}'\n" +
            $"'{exe}' &\n");
        var psi = new System.Diagnostics.ProcessStartInfo { FileName = "/bin/sh", ArgumentList = { sh }, UseShellExecute = false };
        System.Diagnostics.Process.Start(psi);
        Environment.Exit(0);
    }

    public ObservableCollection<QuestTabVM> Tabs { get; } = new();
    public ObservableCollection<TaskCardVM> CurrentTasks { get; } = new();
    public ObservableCollection<DailyCardVM> DailyItems { get; } = new();

    [ObservableProperty] private bool _isListOpen;
    [ObservableProperty] private QuestTabVM? _selectedTab;
    [ObservableProperty] private TaskCardVM? _selectedTask;
    [ObservableProperty] private bool _isUpdateAvailable; // TODO: проверка GitLab Releases

    public bool IsHomeVisible => !IsListOpen;
    partial void OnIsListOpenChanged(bool value) => OnPropertyChanged(nameof(IsHomeVisible));

    public async Task LoadAsync()
    {
        Tabs.Clear();
        var quests = await _db.GetQuestsAsync();
        var i = 0;
        foreach (var q in quests)
            Tabs.Add(new QuestTabVM { QuestId = q.Id, Name = q.Name, ColorHex = Palette[i++ % Palette.Length], IsSelected = SelectedTab?.QuestId == q.Id });

        if (IsListOpen && SelectedTab is not null && Tabs.All(t => t.QuestId != SelectedTab.QuestId))
            Collapse();

        await LoadDailyPlanAsync();
    }

    public async Task LoadDailyPlanAsync()
    {
        DailyItems.Clear();
        var quests = await _db.GetQuestsAsync();
        var names = quests.ToDictionary(q => q.Id, q => q.Name);
        foreach (var task in await _db.GetDailyPlanTasksAsync())
        {
            var subs = await _db.GetSubtasksAsync(task.Id);
            var cur = subs.FirstOrDefault(s => !s.IsDone);
            DailyItems.Add(new DailyCardVM
            {
                TaskId = task.Id,
                QuestName = names.TryGetValue(task.QuestId, out var n) ? n : "—",
                Title = task.Title,
                DateRange = FormatDates(task),
                Progress = Percent(subs),
                CurrentSubtaskId = cur?.Id,
                CurrentSubtaskText = cur?.Title ?? string.Empty,
                HasCurrentSubtask = cur is not null,
            });
        }
    }

    private static int Percent(System.Collections.Generic.List<Subtask> subs)
        => subs.Count == 0 ? 0 : (int)Math.Round(subs.Count(s => s.IsDone) * 100.0 / subs.Count);

    private static string FormatDates(QuestTask t)
    {
        var s = t.StartDate?.ToString("dd.MM.yy") ?? "Star Line";
        var d = t.Deadline?.ToString("dd.MM.yy") ?? "Ded line";
        return $"DATE: {s} — {d}";
    }

    [RelayCommand]
    private async Task OpenTabAsync(QuestTabVM tab)
    {
        if (tab is null) return;
        if (IsListOpen && SelectedTab?.QuestId == tab.QuestId)
        {
            foreach (var t in Tabs) t.IsSelected = false;
            SelectedTab = null;
            Collapse();
            return;
        }
        foreach (var t in Tabs) t.IsSelected = ReferenceEquals(t, tab);
        SelectedTab = tab;
        await LoadTasksAsync(tab.QuestId);
        IsListOpen = true;
    }

    public void Collapse()
    {
        IsListOpen = false;
        CurrentTasks.Clear();
        SelectedTask = null;
    }

    [RelayCommand]
    private async Task GoHomeAsync()
    {
        foreach (var t in Tabs) t.IsSelected = false;
        SelectedTab = null;
        Collapse();
        await LoadDailyPlanAsync();
    }

    private async Task LoadTasksAsync(int questId)
    {
        CurrentTasks.Clear();
        foreach (var task in await _db.GetTasksAsync(questId))
        {
            var subs = await _db.GetSubtasksAsync(task.Id);
            var cur = subs.FirstOrDefault(s => !s.IsDone);
            CurrentTasks.Add(new TaskCardVM
            {
                TaskId = task.Id,
                Title = task.Title,
                DateRange = FormatDates(task),
                Progress = Percent(subs),
                InDailyPlan = task.InDailyPlan,
                CurrentSubtaskId = cur?.Id,
                CurrentSubtaskText = cur?.Title ?? string.Empty,
                HasCurrentSubtask = cur is not null,
            });
        }
    }

    public async Task RefreshOpenAsync()
    {
        if (IsListOpen && SelectedTab is not null)
            await LoadTasksAsync(SelectedTab.QuestId);
        await LoadDailyPlanAsync();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (IsListOpen && SelectedTab is not null)
        {
            var existing = await _db.GetTasksAsync(SelectedTab.QuestId);
            await _db.SaveTaskAsync(new QuestTask
            {
                QuestId = SelectedTab.QuestId, Title = string.Empty, SortOrder = existing.Count,
                StartDate = DateTime.Today, Deadline = DateTime.Today.AddDays(1),
            });
            await LoadTasksAsync(SelectedTab.QuestId);
        }
        else
        {
            var name = PromptText is null ? null : await PromptText("Новый лист", "Название листа");
            if (string.IsNullOrWhiteSpace(name)) return;
            var order = await _db.CountQuestsAsync();
            var id = await _db.SaveQuestAsync(new Quest { Name = name.Trim(), SortOrder = order });
            var tab = new QuestTabVM { QuestId = id, Name = name.Trim(), ColorHex = Palette[Tabs.Count % Palette.Length] };
            Tabs.Add(tab);
            await OpenTabAsync(tab);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (IsListOpen && SelectedTask is not null)
        {
            await _db.DeleteTaskAsync(SelectedTask.TaskId);
            if (SelectedTab is not null) await LoadTasksAsync(SelectedTab.QuestId);
            SelectedTask = null;
        }
        else if (SelectedTab is not null)
        {
            var ok = Confirm is null || await Confirm("Удалить лист?", $"«{SelectedTab.Name}» и все задания будут удалены.");
            if (!ok) return;
            await _db.DeleteQuestAsync(SelectedTab.QuestId);
            var t = Tabs.FirstOrDefault(x => x.QuestId == SelectedTab.QuestId);
            if (t is not null) Tabs.Remove(t);
            SelectedTab = null;
            Collapse();
        }
        else if (Alert is not null)
        {
            await Alert("Выберите лист или задание.");
        }
    }

    [RelayCommand]
    private void SelectTask(TaskCardVM card)
    {
        foreach (var t in CurrentTasks) t.IsSelected = ReferenceEquals(t, card) && !t.IsSelected;
        SelectedTask = CurrentTasks.FirstOrDefault(t => t.IsSelected);
    }

    [RelayCommand]
    private async Task OpenTaskAsync(TaskCardVM card)
    {
        if (card is null) return;
        foreach (var t in CurrentTasks) t.IsSelected = ReferenceEquals(t, card);
        SelectedTask = card;
        if (OpenTaskDetail is not null) await OpenTaskDetail(card.TaskId);
        await RefreshOpenAsync();
    }

    public async Task SetDailyPlanAsync(TaskCardVM card)
    {
        var task = await _db.GetTaskAsync(card.TaskId);
        if (task is null) return;
        if (card.InDailyPlan && !task.InDailyPlan)
        {
            if (await _db.CountDailyPlanAsync() >= AppDatabase.DailyTaskLimit)
            {
                if (Alert is not null) await Alert($"В дневном плане максимум {AppDatabase.DailyTaskLimit} задания.");
                card.InDailyPlan = false;
                return;
            }
        }
        task.InDailyPlan = card.InDailyPlan;
        await _db.SaveTaskAsync(task);
        await LoadDailyPlanAsync();
    }

    public async Task ToggleCurrentSubtaskAsync(TaskCardVM card)
    {
        if (card?.CurrentSubtaskId is null) return;
        await MarkSubtaskDoneAsync(card.TaskId, card.CurrentSubtaskId.Value);
        var subs = await _db.GetSubtasksAsync(card.TaskId);
        var next = subs.FirstOrDefault(s => !s.IsDone);
        card.CurrentSubtaskId = next?.Id;
        card.CurrentSubtaskText = next?.Title ?? string.Empty;
        card.HasCurrentSubtask = next is not null;
        card.Progress = Percent(subs);
        await LoadDailyPlanAsync();
    }

    public async Task ToggleDailySubtaskAsync(DailyCardVM card)
    {
        if (card?.CurrentSubtaskId is null) return;
        await MarkSubtaskDoneAsync(card.TaskId, card.CurrentSubtaskId.Value);
        var subs = await _db.GetSubtasksAsync(card.TaskId);
        var next = subs.FirstOrDefault(s => !s.IsDone);
        card.CurrentSubtaskId = next?.Id;
        card.CurrentSubtaskText = next?.Title ?? string.Empty;
        card.HasCurrentSubtask = next is not null;
        card.Progress = Percent(subs);
    }

    private async Task MarkSubtaskDoneAsync(int taskId, int subId)
    {
        var subs = await _db.GetSubtasksAsync(taskId);
        var sub = subs.FirstOrDefault(s => s.Id == subId);
        if (sub is null) return;
        sub.IsDone = true;
        await _db.SaveSubtaskAsync(sub);
        await _db.RefreshTaskProgressAsync(taskId);
    }

    // ---- Синхронизация (пути даёт View) ----
    public Task ExportAsync(string path) => _sync.SaveToFileAsync(path);
    public Task ImportAsync(string path) => _sync.LoadFromFileAsync(path);
    public Task<SyncService.SyncResult> SyncAsync(string path) => _sync.SyncAsync(path);
}
