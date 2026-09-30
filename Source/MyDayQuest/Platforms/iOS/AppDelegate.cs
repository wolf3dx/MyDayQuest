using Foundation;
using UIKit;

namespace MyDayQuest;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// Возврат из окна входа в облако по схеме mydayquest://auth
	public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
	{
		if (Microsoft.Maui.Authentication.WebAuthenticator.Default.OpenUrl(new Uri(url.AbsoluteString!)))
			return true;

		return base.OpenUrl(app, url, options);
	}
}
