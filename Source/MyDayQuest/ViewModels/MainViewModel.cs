using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyDayQuest.Data;
using MyDayQuest.Models;
using MyDayQuest.Services;
using MyDayQuest.Views;

namespace MyDayQuest.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly SyncService _sync;
    private readonly UpdateService _update;
    private UpdateInfo? _pendingUpdate;

    private const string SyncFileName = "MyDayQuest.mdq";


    // Пастельная палитра для карточек-вкладок (по кругу).
    private static readonly string[] Palette =
    {
        "#EBDCB8", "#E0CDA0", "#D5BE8C", "#F0E3C4", "#DCC79C", "#E6D5AE", "#CBB183",
    };

    public MainViewModel(AppDatabase db, SyncService sync, UpdateService update)
    {
        _db = db;
        _sync = sync;
        _update = update;
    }

    /// <summary>Ленты листов внизу.</summary>
    public ObservableCollection<QuestTab> Tabs { get; } = new();

    /// <summary>Задачи открытого листа (среднее поле).</summary>
    public ObservableCollection<TaskItemVM> CurrentTasks { get; } = new();

    /// <summary>Плашки Главного окна — задания, добавленные в дневной план (чек-бокс у Name).</summary>
    public ObservableCollection<DailyTaskVM> DailyItems { get; } = new();

    /// <summary>Открыт ли лист в среднем поле.</summary>
    [ObservableProperty]
    private bool _isListOpen;

    /// <summary>Выделенный лист (остаётся выделенным и после сворачивания).</summary>
    [ObservableProperty]
    private QuestTab? _selectedTab;

    [ObservableProperty]
    private string _currentListName = string.Empty;

    [ObservableProperty]
    private TaskItemVM? _selectedTask;

    public bool IsEmptyFieldVisible => !IsListOpen;

    partial void OnIsListOpenChanged(bool value) => OnPropertyChanged(nameof(IsEmptyFieldVisible));

    /// <summary>Доступно ли обновление приложения. Иконка в шапке видна ТОЛЬКО когда true.</summary>
    [ObservableProperty]
    private bool _isUpdateAvailable;

    /// <summary>Проверить GitLab Releases на новую версию.</summary>
    public async Task CheckUpdateAsync()
    {
        _pendingUpdate = await _update.CheckAsync(AppVersion.Current);
        IsUpdateAvailable = _pendingUpdate is not null;
    }

    /// <summary>Клик по иконке обновления — скачать и применить (по платформе).</summary>
    [RelayCommand]
    private async Task UpdateAppAsync()
    {
        if (_pendingUpdate is null) return;
        var up = _pendingUpdate;

        var ok = await Shell.Current.DisplayAlertAsync("Обновление",
            $"Доступна версия {up.Version}. Обновить сейчас?", "Обновить", "Позже");
        if (!ok) return;

        try
        {
            var platform = DeviceInfo.Current.Platform;
            if (platform == DevicePlatform.WinUI)
                await ApplyWindowsUpdateAsync(up);
            else if (platform == DevicePlatform.Android)
                await ApplyAndroidUpdateAsync(up);
            else
                await Launcher.Default.OpenAsync(up.ReleaseUrl); // macOS/iOS: открыть страницу релиза
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Ошибка обновления", ex.Message, "OK");
        }
    }

    private async Task ApplyWindowsUpdateAsync(UpdateInfo up)
    {
        var url = UpdateService.RawAssetUrl(up.Tag, "MyDayQuest-windows-x64.zip");
        var tmp = Path.Combine(Path.GetTempPath(), "mdq_update");
        Directory.CreateDirectory(tmp);
        var zip = Path.Combine(tmp, "update.zip");
        await UpdateService.DownloadAsync(url, zip);

        var appDir = AppContext.BaseDirectory.TrimEnd('\\');
        var exe = Path.Combine(appDir, "MyDayQuest.exe");
        var bat = Path.Combine(tmp, "apply-update.bat");
        File.WriteAllText(bat,
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            ":wait\r\n" +
            "tasklist /fi \"imagename eq MyDayQuest.exe\" | find /i \"MyDayQuest.exe\" >nul && (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
            $"powershell -NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -Force '{zip}' '{appDir}'\"\r\n" +
            $"start \"\" \"{exe}\"\r\n");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = bat,
            UseShellExecute = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        });
        Application.Current?.Quit();
    }

    private async Task ApplyAndroidUpdateAsync(UpdateInfo up)
    {
        var url = UpdateService.RawAssetUrl(up.Tag, "MyDayQuest-android.apk");
        var apk = Path.Combine(FileSystem.CacheDirectory, "MyDayQuest-update.apk");
        await UpdateService.DownloadAsync(url, apk);
        // Открыть системный установщик APK (пользователь подтверждает установку).
        await Launcher.Default.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(apk),
        });
    }

    public async Task LoadAsync()
    {
        Tabs.Clear();
        var quests = await _db.GetQuestsAsync();
        var i = 0;
        foreach (var q in quests)
        {
            Tabs.Add(new QuestTab
            {
                QuestId = q.Id,
                Name = q.Name,
                ColorHex = Palette[i++ % Palette.Length],
                IsSelected = SelectedTab?.QuestId == q.Id,
            });
        }

        // Если открытый лист удалён извне — свернуть.
        if (IsListOpen && SelectedTab is not null && Tabs.All(t => t.QuestId != SelectedTab.QuestId))
            Collapse();

        await LoadDailyPlanAsync();
    }

    /// <summary>Загрузить плашки Главного окна (задания дневного плана).</summary>
    public async Task LoadDailyPlanAsync()
    {
        DailyItems.Clear();
        var quests = await _db.GetQuestsAsync();
        var names = quests.ToDictionary(q => q.Id, q => q.Name);

        foreach (var task in await _db.GetDailyPlanTasksAsync())
        {
            var subs = await _db.GetSubtasksAsync(task.Id);
            var current = subs.FirstOrDefault(s => !s.IsDone);
            var progress = subs.Count == 0 ? 0
                : (int)Math.Round(subs.Count(s => s.IsDone) * 100.0 / subs.Count);

            DailyItems.Add(new DailyTaskVM
            {
                TaskId = task.Id,
                QuestName = names.TryGetValue(task.QuestId, out var n) ? n : "—",
                Title = task.Title,
                DateRange = FormatDates(task),
                Progress = progress,
                CurrentSubtaskId = current?.Id,
                CurrentSubtaskText = current?.Title ?? string.Empty,
                HasCurrentSubtask = current is not null,
            });
        }
    }

    /// <summary>Отметить текущее подзадание плашки Главного окна выполненным.</summary>
    [RelayCommand]
    private async Task ToggleDailySubtaskAsync(DailyTaskVM card)
    {
        if (card is null || card.CurrentSubtaskId is null) return;

        var subs = await _db.GetSubtasksAsync(card.TaskId);
        var sub = subs.FirstOrDefault(s => s.Id == card.CurrentSubtaskId);
        if (sub is null) return;

        sub.IsDone = true;
        await _db.SaveSubtaskAsync(sub);
        await _db.RefreshTaskProgressAsync(card.TaskId);

        var refreshed = await _db.GetSubtasksAsync(card.TaskId);
        var next = refreshed.FirstOrDefault(s => !s.IsDone);
        card.CurrentSubtaskId = next?.Id;
        card.CurrentSubtaskText = next?.Title ?? string.Empty;
        card.HasCurrentSubtask = next is not null;
        card.Progress = refreshed.Count == 0 ? 0
            : (int)Math.Round(refreshed.Count(s => s.IsDone) * 100.0 / refreshed.Count);
    }

    // --- Работа с лентой листов ---

    [RelayCommand]
    private async Task OpenTabAsync(QuestTab tab)
    {
        if (tab is null) return;

        // Повторный тап по уже открытой вкладке — свернуть.
        if (IsListOpen && SelectedTab is not null && SelectedTab.QuestId == tab.QuestId)
        {
            foreach (var t in Tabs) t.IsSelected = false;
            SelectedTab = null;
            Collapse();
            return;
        }

        foreach (var t in Tabs)
            t.IsSelected = ReferenceEquals(t, tab);

        SelectedTab = tab;
        CurrentListName = tab.Name;
        await LoadTasksAsync(tab.QuestId);
        IsListOpen = true;
    }

    [RelayCommand]
    private void Collapse()
    {
        IsListOpen = false;
        CurrentTasks.Clear();
        SelectedTask = null;
    }

    /// <summary>Кнопка QUEST — вернуться в Главное окно (свернуть лист) и показать дневной план.</summary>
    [RelayCommand]
    private async Task GoHomeAsync()
    {
        foreach (var t in Tabs) t.IsSelected = false;
        SelectedTab = null;
        Collapse();
        await LoadDailyPlanAsync();
    }

    /// <summary>Перезагрузить задачи открытого листа (после возврата с экрана деталей).</summary>
    public async Task RefreshOpenListAsync()
    {
        if (IsListOpen && SelectedTab is not null)
            await LoadTasksAsync(SelectedTab.QuestId);
    }

    private async Task LoadTasksAsync(int questId)
    {
        CurrentTasks.Clear();
        var tasks = await _db.GetTasksAsync(questId);
        foreach (var task in tasks)
        {
            var subs = await _db.GetSubtasksAsync(task.Id);
            var current = subs.FirstOrDefault(s => !s.IsDone);
            var progress = subs.Count == 0 ? 0
                : (int)Math.Round(subs.Count(s => s.IsDone) * 100.0 / subs.Count);

            CurrentTasks.Add(new TaskItemVM
            {
                TaskId = task.Id,
                Title = task.Title,
                IsDone = task.IsDone,
                DateRange = FormatDates(task),
                Progress = progress,
                CurrentSubtaskId = current?.Id,
                CurrentSubtaskText = current?.Title ?? string.Empty,
                HasCurrentSubtask = current is not null,
                InDailyPlan = task.InDailyPlan,
            });
        }
    }

    private static string FormatDates(QuestTask task)
    {
        var start = task.StartDate?.ToString("dd.MM.yy") ?? "Star Line";
        var dead = task.Deadline?.ToString("dd.MM.yy") ?? "Ded line";
        return $"DATE: {start} — {dead}";
    }

    // --- Контекстные кнопки + / – ---

    [RelayCommand]
    private async Task AddAsync()
    {
        if (IsListOpen && SelectedTab is not null)
            await AddTaskAsync(SelectedTab.QuestId);
        else
            await AddListAsync();
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        // Лист открыт и выбрана задача → удаляем задачу.
        // Иначе (лист открыт без выбранной задачи, либо свёрнут) → удаляем сам лист.
        if (IsListOpen && SelectedTask is not null)
            await RemoveTaskAsync();
        else
            await RemoveListAsync();
    }

    private async Task AddListAsync()
    {
        var name = await Shell.Current.DisplayPromptAsync(
            "Новый лист", "Название листа:", "Создать", "Отмена",
            placeholder: "Например: Карнеги Quest");
        if (string.IsNullOrWhiteSpace(name)) return;

        var order = await _db.CountQuestsAsync();
        var quest = new Quest { Name = name.Trim(), SortOrder = order };
        var id = await _db.SaveQuestAsync(quest);

        var tab = new QuestTab
        {
            QuestId = id,
            Name = quest.Name,
            ColorHex = Palette[(Tabs.Count) % Palette.Length],
        };
        Tabs.Add(tab);
        await OpenTabAsync(tab);
    }

    private async Task RemoveListAsync()
    {
        if (SelectedTab is null)
        {
            await Shell.Current.DisplayAlertAsync("Удаление",
                "Сначала выберите лист в ленте внизу.", "OK");
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Удалить лист?",
            $"«{SelectedTab.Name}» и все его задачи будут удалены.", "Удалить", "Отмена");
        if (!confirm) return;

        await _db.DeleteQuestAsync(SelectedTab.QuestId);
        var toRemove = Tabs.FirstOrDefault(t => t.QuestId == SelectedTab.QuestId);
        if (toRemove is not null) Tabs.Remove(toRemove);
        SelectedTab = null;
        Collapse();
    }

    private async Task AddTaskAsync(int questId)
    {
        // Пустое задание без диалога — название/даты/подзадания заполняются на экране деталей.
        var existing = await _db.GetTasksAsync(questId);
        var task = new QuestTask
        {
            QuestId = questId,
            Title = string.Empty,
            SortOrder = existing.Count,
            StartDate = DateTime.Today,
            Deadline = DateTime.Today.AddDays(1),
        };
        await _db.SaveTaskAsync(task);
        await LoadTasksAsync(questId);
    }

    private async Task RemoveTaskAsync()
    {
        if (SelectedTask is null)
        {
            await Shell.Current.DisplayAlertAsync("Удаление",
                "Сначала выберите задачу в списке.", "OK");
            return;
        }
        if (SelectedTab is null) return;

        await _db.DeleteTaskAsync(SelectedTask.TaskId);
        await LoadTasksAsync(SelectedTab.QuestId);
        SelectedTask = null;
    }

    [RelayCommand]
    private void SelectTask(TaskItemVM task)
    {
        foreach (var t in CurrentTasks)
            t.IsSelected = ReferenceEquals(t, task) && !t.IsSelected;
        SelectedTask = CurrentTasks.FirstOrDefault(t => t.IsSelected);
    }

    /// <summary>Чек-бокс у Name: добавить/убрать задание из Главного Листа (лимит 4).</summary>
    [RelayCommand]
    private async Task SetDailyPlanAsync(TaskItemVM card)
    {
        if (card is null) return;
        var task = await _db.GetTaskAsync(card.TaskId);
        if (task is null) return;

        if (card.InDailyPlan && !task.InDailyPlan)
        {
            var count = await _db.CountDailyPlanAsync();
            if (count >= AppDatabase.DailyTaskLimit)
            {
                await Shell.Current.DisplayAlertAsync("Главный Лист",
                    $"В дневном плане максимум {AppDatabase.DailyTaskLimit} задания.", "OK");
                card.InDailyPlan = false; // откат чек-бокса
                return;
            }
        }

        task.InDailyPlan = card.InDailyPlan;
        await _db.SaveTaskAsync(task);
    }

    /// <summary>Отметить текущее подзадание задания выполненным прямо с плашки → показать следующее.</summary>
    [RelayCommand]
    private async Task ToggleCurrentSubtaskAsync(TaskItemVM card)
    {
        if (card is null || card.CurrentSubtaskId is null) return;

        var subs = await _db.GetSubtasksAsync(card.TaskId);
        var sub = subs.FirstOrDefault(s => s.Id == card.CurrentSubtaskId);
        if (sub is null) return;

        sub.IsDone = true;
        await _db.SaveSubtaskAsync(sub);
        await _db.RefreshTaskProgressAsync(card.TaskId);

        var refreshed = await _db.GetSubtasksAsync(card.TaskId);
        var next = refreshed.FirstOrDefault(s => !s.IsDone);
        card.CurrentSubtaskId = next?.Id;
        card.CurrentSubtaskText = next?.Title ?? string.Empty;
        card.HasCurrentSubtask = next is not null;
        card.Progress = refreshed.Count == 0 ? 0
            : (int)Math.Round(refreshed.Count(s => s.IsDone) * 100.0 / refreshed.Count);
    }

    /// <summary>Тап по заданию → разворачивает его на экране деталей (название, даты, подзадания).
    /// Заодно помечает задание выбранным, чтобы «–» мог его удалить после возврата.</summary>
    [RelayCommand]
    private async Task OpenTaskAsync(TaskItemVM task)
    {
        if (task is null) return;
        foreach (var t in CurrentTasks)
            t.IsSelected = ReferenceEquals(t, task);
        SelectedTask = task;
        await Shell.Current.GoToAsync($"{nameof(TaskDetailPage)}?taskId={task.TaskId}");
    }

    // ---- Синхронизация: Save / Load / Sync ----

    /// <summary>Сохранить все данные в один файл (для облачной папки).</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var json = await _sync.ExportJsonAsync();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var result = await FileSaver.Default.SaveAsync(SyncFileName, stream);
            if (result.IsSuccessful)
            {
                SyncTargetStore.RememberPath(result.FilePath);
                await Shell.Current.DisplayAlertAsync("Сохранено",
                    $"Данные сохранены:\n{result.FilePath}", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Ошибка сохранения", ex.Message, "OK");
        }
    }

    /// <summary>Загрузить данные из файла (заменяет локальные).</summary>
    [RelayCommand]
    private async Task LoadFromFileAsync()
    {
        try
        {
            var target = await SyncTargetStore.PickAsync();
            if (target is null) return;

            var confirm = await Shell.Current.DisplayAlertAsync("Загрузка",
                "Текущие данные будут заменены данными из файла. Продолжить?", "Загрузить", "Отмена");
            if (!confirm) return;

            await _sync.PullAsync(target);
            Collapse();
            await LoadAsync();
            await Shell.Current.DisplayAlertAsync("Готово",
                $"Данные загружены из файла:\n{target.Describe}\n\n"
                + "Дальше обновлять его кнопкой «Sync».", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Ошибка загрузки", ex.Message, "OK");
        }
    }

    /// <summary>Синхронизировать: кто новее (локальные данные или файл), тот побеждает.</summary>
    [RelayCommand]
    private async Task SyncAsync()
    {
        try
        {
            var target = SyncTargetStore.Current;
            if (target is null)
            {
                await Shell.Current.DisplayAlertAsync("Синхронизация",
                    "Сначала откройте файл синхронизации кнопкой «Load» — например, "
                    + "в облачном диске. Или создайте его кнопкой «Save».", "OK");
                return;
            }

            var res = await _sync.SyncAsync(target);
            var msg = res switch
            {
                SyncService.SyncResult.PushedToFile => "Локальные данные новее — выгружены в файл.",
                SyncService.SyncResult.PulledFromFile => "Файл новее — данные загружены с файла.",
                SyncService.SyncResult.UpToDate => "Уже синхронизировано.",
                _ => "Файл не найден.",
            };

            if (res == SyncService.SyncResult.PulledFromFile)
            {
                Collapse();
                await LoadAsync();
            }
            await Shell.Current.DisplayAlertAsync("Синхронизация", msg, "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Ошибка синхронизации", ex.Message, "OK");
        }
    }

}
