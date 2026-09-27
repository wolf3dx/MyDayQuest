using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
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
