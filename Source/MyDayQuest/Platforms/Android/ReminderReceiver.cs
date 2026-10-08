using Android.App;
using Android.Content;
using AndroidX.Core.App;
using MyDayQuest.Data;

namespace MyDayQuest.Platforms.Android;

/// <summary>
/// Приходит по будильнику и показывает, что сегодня в плане. Читает базу сам:
/// приложение в этот момент может быть закрыто, и спросить у него некого.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public const string ChannelId = "mydayquest.daily";
    private const int NotificationId = 1001;

    public override void OnReceive(Context? context, Intent? intent)
    {
        var ctx = context ?? Platform.AppContext;
        var pending = GoAsync();
        _ = ShowAsync(ctx).ContinueWith(_ => pending.Finish());
    }

    /// <summary>Показать уведомление сейчас — тем же кодом, что и по будильнику.</summary>
    public static Task ShowNowAsync() => ShowAsync(Platform.AppContext, reschedule: false);

    private static async Task ShowAsync(Context ctx, bool reschedule = true)
    {
        try
        {
            var db = new AppDatabase(FileSystem.AppDataDirectory);
            var tasks = await db.GetDailyPlanTasksAsync();
            var names = await db.GetQuestNamesAsync();

            if (tasks.Count == 0)
            {
                if (reschedule) Reminders.Schedule();   // плана нет — просто переставляем на завтра
                return;
            }

            var overdue = tasks.Where(t => t.IsOverdue).ToList();
            var title = overdue.Count > 0
                ? $"Задания на сегодня — {overdue.Count} просрочено"
                : $"Задания на сегодня: {tasks.Count}";

            var lines = tasks.Select(t =>
            {
                var list = names.TryGetValue(t.QuestId, out var n) ? n : "—";
                return t.IsOverdue
                    ? $"❗ {t.Title} ({list}) — не сделано вовремя"
                    : $"• {t.Title} ({list})";
            }).ToList();

            var style = new NotificationCompat.InboxStyle().SetBigContentTitle(title);
            foreach (var l in lines) style.AddLine(l);

            var open = PendingIntent.GetActivity(ctx, 0,
                ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!),
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(ctx, ChannelId)
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle(title)
                .SetContentText(string.Join("; ", tasks.Select(t => t.Title)))
                .SetStyle(style)
                .SetAutoCancel(true)
                .SetContentIntent(open)
                .SetPriority(NotificationCompat.PriorityDefault);

            NotificationManagerCompat.From(ctx).Notify(NotificationId, builder.Build());
        }
        catch (Exception ex)
        {
            // Ронять приложение из-за напоминания не стоит, но и молчать нельзя:
            // не показанное уведомление без следа в журнале не отличить от
            // пустого плана.
            global::Android.Util.Log.Error("MyDayQuest.Reminder", ex.ToString());
        }
        finally
        {
            if (reschedule) Reminders.Schedule();   // следующий будильник на завтра
        }
    }
}
