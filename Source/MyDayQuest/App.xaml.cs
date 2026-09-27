using Microsoft.Extensions.DependencyInjection;

namespace MyDayQuest;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// Телефонная ширина фиксирована; высота тянется, но не выше экрана.
		// (на Android/iOS эти свойства игнорируются — там всегда полный экран)
		const double phoneWidth = 400;
		const double minHeight = 420;

		double screenHeightDip;
		try
		{
			var info = DeviceDisplay.Current.MainDisplayInfo;
			screenHeightDip = info.Height > 0 && info.Density > 0
				? info.Height / info.Density
				: 900;
		}
		catch { screenHeightDip = 900; }

		double maxHeight = Math.Max(minHeight, screenHeightDip - 60);

		// Ширина — жёстко фиксирована.
		window.Width = phoneWidth;
		window.MinimumWidth = phoneWidth;
		window.MaximumWidth = phoneWidth;

		// Высота — изменяется от минимума до высоты экрана.
		window.MinimumHeight = minHeight;
		window.MaximumHeight = maxHeight;
		window.Height = Math.Min(730, maxHeight);

		return window;
	}
}