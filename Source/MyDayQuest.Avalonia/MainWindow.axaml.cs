using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MyDayQuest.Avalonia.ViewModels;
using MyDayQuest.Cloud;
using MyDayQuest.Data;

namespace MyDayQuest.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly AppDatabase _db;
    private readonly string _syncPathFile;

    public MainWindow(MainViewModel vm, AppDatabase db, string appFolder)
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = _vm = vm;
        _db = db;
        _syncPathFile = Path.Combine(appFolder, "syncpath.txt");

        _vm.PromptText = (t, p) => Dialogs.PromptAsync(this, t, p);
        _vm.Confirm = (t, m) => Dialogs.ConfirmAsync(this, t, m);
        _vm.Alert = m => Dialogs.AlertAsync(this, "My Day Quest", m);
        _vm.ConfirmUpdate = (t, m) => Dialogs.ConfirmAsync(this, t, m);
        _vm.OpenUrl = async u => { var tl = GetTopLevel(this); if (tl is not null) await tl.Launcher.LaunchUriAsync(new System.Uri(u)); };
        _vm.OpenTaskDetail = OpenTaskDetailAsync;

        Opened += async (_, _) =>
        {
            await _vm.LoadAsync();
            _ = _vm.CheckUpdateAsync(); // проверка обновления в фоне
            _ = _vm.StartCloudSyncAsync(); // автосинхронизация с облаком, если оно подключено
        };
    }

    private async Task OpenTaskDetailAsync(int taskId)
    {
        var win = new TaskDetailWindow(new TaskDetailViewModel(_db, taskId));
        await win.ShowDialog(this);
    }

    // ---- Карточки заданий ----
    private void OnTaskTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { Tag: TaskCardVM card })
            _vm.SelectTaskCommand.Execute(card);
    }

    private void OnTaskDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { Tag: TaskCardVM card })
            _vm.OpenTaskCommand.Execute(card);
    }

    private async void OnPlanChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: TaskCardVM card })
            await _vm.SetDailyPlanAsync(card);
    }

    private async void OnTaskSubtaskChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { IsChecked: true, Tag: TaskCardVM card } cb)
        {
            await _vm.ToggleCurrentSubtaskAsync(card);
            cb.IsChecked = false;
        }
    }

    private async void OnDailySubtaskChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { IsChecked: true, Tag: DailyCardVM card } cb)
        {
            await _vm.ToggleDailySubtaskAsync(card);
            cb.IsChecked = false;
        }
    }

    // ---- Шапка ----
    private async void OnAbout(object? sender, RoutedEventArgs e)
        => await Dialogs.AlertAsync(this, "О приложении",
            "My Day Quest (M.D.Q)\n\nПланировщик целей: листы → задания → подзадания, единый дневной план.\n\nАвтор: Виталий Коновалов. Версия 0.1.0.");

    private async void OnUpdateClick(object? sender, PointerPressedEventArgs e)
        => await _vm.UpdateAppAsync();

    private static readonly FilePickerFileType MdqType = new("MyDayQuest (.mdq)") { Patterns = new[] { "*.mdq" } };

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить данные", SuggestedFileName = "MyDayQuest.mdq",
                FileTypeChoices = new[] { MdqType },
            });
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;
            await _vm.ExportAsync(path);
            SaveSyncPath(path);
            await Dialogs.AlertAsync(this, "Сохранено", $"Данные сохранены:\n{path}");
        }
        catch (Exception ex) { await Dialogs.AlertAsync(this, "Ошибка", ex.Message); }
    }

    private async void OnLoad(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Загрузить данные", AllowMultiple = false, FileTypeFilter = new[] { MdqType },
            });
            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(path)) return;
            if (!await Dialogs.ConfirmAsync(this, "Загрузка", "Текущие данные будут заменены. Продолжить?")) return;
            await _vm.ImportAsync(path);
            SaveSyncPath(path);
            await _vm.LoadAsync();
            await Dialogs.AlertAsync(this, "Готово", "Данные загружены.");
        }
        catch (Exception ex) { await Dialogs.AlertAsync(this, "Ошибка", ex.Message); }
    }

    private async void OnSync(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = LoadSyncPath();
            if (string.IsNullOrEmpty(path))
            {
                await Dialogs.AlertAsync(this, "Синхронизация", "Сначала нажмите «Save» или «Load», чтобы выбрать файл.");
                return;
            }
            var res = await _vm.SyncAsync(path);
            var msg = res switch
            {
                SyncService.SyncResult.PushedToFile => "Локальные данные новее — выгружены в файл.",
                SyncService.SyncResult.PulledFromFile => "Файл новее — данные загружены.",
                SyncService.SyncResult.UpToDate => "Уже синхронизировано.",
                _ => "Файл не найден.",
            };
            if (res == SyncService.SyncResult.PulledFromFile) await _vm.LoadAsync();
            await Dialogs.AlertAsync(this, "Синхронизация", msg);
        }
        catch (Exception ex) { await Dialogs.AlertAsync(this, "Ошибка", ex.Message); }
    }

    /// <summary>Меню облака: подключить провайдера, синхронизировать вручную, отключить.</summary>
    private async void OnCloud(object? sender, RoutedEventArgs e)
    {
        var options = new System.Collections.Generic.List<string>();
        if (_vm.IsCloudEnabled)
        {
            options.Add("Синхронизировать сейчас");
            options.Add("Отключить облако");
        }
        options.Add("Подключить Яндекс.Диск");
        options.Add("Подключить OneDrive");
        options.Add("Подключить Google Drive");

        var choice = await Dialogs.ChooseAsync(this, _vm.CloudStatusText, options.ToArray());
        switch (choice)
        {
            case "Синхронизировать сейчас":
                var report = await _vm.SyncCloudNowAsync();
                await Dialogs.AlertAsync(this, "Облако", report.Message);
                break;

            case "Отключить облако":
                if (await Dialogs.ConfirmAsync(this, "Облако",
                        "Отключить облако? Файл в облаке останется, приложение перестанет его обновлять."))
                    _vm.DisconnectCloud();
                break;

            case "Подключить Яндекс.Диск":
                await ConnectCloudAsync(CloudProvider.YandexDisk);
                break;
            case "Подключить OneDrive":
                await ConnectCloudAsync(CloudProvider.OneDrive);
                break;
            case "Подключить Google Drive":
                await ConnectCloudAsync(CloudProvider.GoogleDrive);
                break;
        }
    }

    private async Task ConnectCloudAsync(CloudProvider provider)
    {
        if (!_vm.IsCloudConfigured(provider))
        {
            await Dialogs.AlertAsync(this, "Облако",
                $"Для «{provider.Display()}» не задан client_id. Создайте файл {CloudConfig.FileName} " +
                "рядом с программой по образцу cloud.config.sample.json.");
            return;
        }

        try
        {
            var report = await _vm.ConnectCloudAsync(provider);
            await Dialogs.AlertAsync(this, "Облако подключено",
                $"{report.Message} Файл данных: {_vm.CloudLocation}. Дальше приложение обновляет его само.");
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync(this, "Не удалось подключить облако", ex.Message);
        }
    }

    private void SaveSyncPath(string path) { try { File.WriteAllText(_syncPathFile, path); } catch { } }
    private string? LoadSyncPath() { try { return File.Exists(_syncPathFile) ? File.ReadAllText(_syncPathFile).Trim() : null; } catch { return null; } }
}
