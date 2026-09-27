using MyDayQuest.Views;

namespace MyDayQuest;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute(nameof(QuestEditPage), typeof(QuestEditPage));
		Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
		Routing.RegisterRoute(nameof(TaskDetailPage), typeof(TaskDetailPage));
	}
}
