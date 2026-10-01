using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace MyDayQuest;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PickDocumentRequest = 4711;
    private static TaskCompletionSource<string?>? _pick;

    /// <summary>
    /// Системный выбор документа. В отличие от FilePicker из MAUI здесь берётся
    /// долгоживущее разрешение: ссылка переживает перезагрузку, и писать можно
    /// прямо в облачный диск, а не в копию файла в кэше приложения.
    /// </summary>
    public static Task<string?> PickDocumentAsync()
    {
        _pick?.TrySetResult(null);
        _pick = new TaskCompletionSource<string?>();

        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            _pick.TrySetResult(null);
            return _pick.Task;
        }

        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission
                        | ActivityFlags.GrantWriteUriPermission
                        | ActivityFlags.GrantPersistableUriPermission);
        activity.StartActivityForResult(intent, PickDocumentRequest);
        return _pick.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != PickDocumentRequest) return;

        var uri = resultCode == Result.Ok ? data?.Data : null;
        if (uri is not null)
        {
            // Без этого разрешение умрёт вместе с окном выбора.
            try
            {
                ContentResolver?.TakePersistableUriPermission(uri,
                    ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            }
            catch (Java.Lang.SecurityException)
            {
                // Поставщик не отдаёт долгоживущее разрешение — ссылка проживёт
                // только до перезапуска, пользователю скажем об этом отдельно.
            }
        }

        _pick?.TrySetResult(uri?.ToString());
        _pick = null;
    }
}
