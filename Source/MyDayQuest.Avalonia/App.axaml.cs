using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyDayQuest.Avalonia.ViewModels;
using MyDayQuest.Cloud;
using MyDayQuest.Data;

namespace MyDayQuest.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Каталог данных приложения (Linux/Windows/macOS).
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyDayQuest");
            Directory.CreateDirectory(appFolder);

            var db = new AppDatabase(appFolder);
            var sync = new SyncService(db);

            // Облачная синхронизация: client_id из cloud.config.json рядом с программой
            // или в каталоге данных; вход — в системном браузере, ответ ловит 127.0.0.1.
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var cloudFactory = new CloudStorageFactory(
                http,
                CloudConfig.Load(AppContext.BaseDirectory, appFolder),
                new FileTokenStore(appFolder),
                app => new LoopbackAuthBrowser(app.DesktopRedirectUri, OpenInBrowser));
            var cloud = new CloudSyncManager(db, sync, cloudFactory, appFolder);

            var vm = new MainViewModel(db, sync, new UpdateService(), cloud);

            desktop.MainWindow = new MainWindow(vm, db, appFolder);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OpenInBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* браузер не найден — пользователь увидит таймаут входа */ }
    }
}