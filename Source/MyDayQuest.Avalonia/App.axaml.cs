using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyDayQuest.Avalonia.ViewModels;
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

            var vm = new MainViewModel(db, sync, new UpdateService());

            desktop.MainWindow = new MainWindow(vm, db, appFolder);
        }

        base.OnFrameworkInitializationCompleted();
    }
}