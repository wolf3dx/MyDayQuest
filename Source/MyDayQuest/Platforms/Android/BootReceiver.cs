using Android.App;
using Android.Content;

namespace MyDayQuest.Platforms.Android;

/// <summary>
/// Взводит будильник напоминания после перезагрузки телефона.
/// Объявлен выключенным: включается только когда пользователь сам разрешил
/// переживать перезагрузку. Пока выключен — система его не поднимает вовсе.
/// </summary>
[BroadcastReceiver(Enabled = false, Exported = true, DirectBootAware = false)]
[IntentFilter(new[] { Intent.ActionBootCompleted, "android.intent.action.QUICKBOOT_POWERON" })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action is not (Intent.ActionBootCompleted or "android.intent.action.QUICKBOOT_POWERON"))
            return;

        Reminders.EnsureChannel();
        Reminders.Schedule();
    }
}
