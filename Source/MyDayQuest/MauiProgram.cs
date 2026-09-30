using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MyDayQuest.Cloud;
using MyDayQuest.CloudAuth;
using MyDayQuest.Data;
using MyDayQuest.ViewModels;
using MyDayQuest.Views;

namespace MyDayQuest;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Данные (путь к БД задаёт MAUI-голова)
		builder.Services.AddSingleton(new AppDatabase(FileSystem.AppDataDirectory));
		builder.Services.AddSingleton<SyncService>();
		builder.Services.AddSingleton<UpdateService>();

		// Облачная синхронизация: client_id берём из cloud.config.json рядом с exe
		// либо из каталога данных приложения (в репозиторий файл не попадает).
		builder.Services.AddSingleton(CloudConfig.Load(
			AppContext.BaseDirectory, FileSystem.AppDataDirectory));
		builder.Services.AddSingleton<ITokenStore>(
			new FileTokenStore(FileSystem.AppDataDirectory));
		builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });
		builder.Services.AddSingleton(sp => new CloudStorageFactory(
			sp.GetRequiredService<HttpClient>(),
			sp.GetRequiredService<CloudConfig>(),
			sp.GetRequiredService<ITokenStore>(),
			app => new MauiAuthBrowser(app)));
		builder.Services.AddSingleton(sp => new CloudSyncManager(
			sp.GetRequiredService<AppDatabase>(),
			sp.GetRequiredService<SyncService>(),
			sp.GetRequiredService<CloudStorageFactory>(),
			FileSystem.AppDataDirectory));

		// ViewModels
		builder.Services.AddSingleton<MainViewModel>();
		builder.Services.AddTransient<QuestEditViewModel>();
		builder.Services.AddTransient<TaskDetailViewModel>();

		// Pages
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddTransient<QuestEditPage>();
		builder.Services.AddTransient<AboutPage>();
		builder.Services.AddTransient<TaskDetailPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
