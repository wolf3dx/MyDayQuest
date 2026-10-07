using Android.App;
using Android.Content;
using Android.OS;
using MyDayQuest.Services;

namespace MyDayQuest.Platforms.Android;

/// <summary>Заводит и снимает ежедневный будильник напоминания.</summary>
public static class Reminders
{
    private const int RequestCode = 2001;

    /// <summary>Канал уведомлений. Создаётся один раз, повторный вызов безвреден.</summary>
    public static void EnsureChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var ctx = Platform.AppContext;
        var mgr = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);
        var channel = new NotificationChannel(ReminderReceiver.ChannelId,
            "Задания на сегодня", NotificationImportance.Default)
        {
            Description = "Напоминание о заданиях из дневного плана",
        };
        mgr?.CreateNotificationChannel(channel);
    }

    /// <summary>
    /// Поставить будильник на ближайшее наступление выбранного времени.
    /// Будильник неточный: напоминание о делах на день не стоит того, чтобы
    /// просить у системы право будить телефон с точностью до секунды.
    /// </summary>
    public static void Schedule()
    {
        Cancel();
        if (!ReminderSettings.Enabled) return;

        var ctx = Platform.AppContext;
        var mgr = (AlarmManager?)ctx.GetSystemService(Context.AlarmService);
        if (mgr is null) return;

        var t = ReminderSettings.Time;
        var next = DateTime.Now.Date + t;
        if (next <= DateTime.Now) next = next.AddDays(1);

        var atMs = (long)(next.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds;
        mgr.SetWindow(AlarmType.RtcWakeup, atMs, 10 * 60 * 1000, Pending()!);
    }

    public static void Cancel()
    {
        var ctx = Platform.AppContext;
        var mgr = (AlarmManager?)ctx.GetSystemService(Context.AlarmService);
        mgr?.Cancel(Pending()!);
    }

    private static PendingIntent? Pending()
    {
        var ctx = Platform.AppContext;
        var intent = new Intent(ctx, typeof(ReminderReceiver));
        return PendingIntent.GetBroadcast(ctx, RequestCode, intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }
}
