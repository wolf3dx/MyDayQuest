using Android.App;
using Android.Content;
using Android.Content.PM;

namespace MyDayQuest.Platforms.Android;

/// <summary>
/// Возврат из окна входа в облако: система отдаёт приложению ссылку вида
/// mydayquest://auth?code=... . Схема должна совпадать с MobileRedirectUri в cloud.config.json.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = CallbackScheme)]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
    public const string CallbackScheme = "mydayquest";
}
