using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using MyDayQuest.Data;

namespace MyDayQuest.Platforms.Android;

/// <summary>
/// Виджет рабочего стола со списком заданий на сегодня.
/// Базу читает сам: приложение в этот момент обычно закрыто.
/// </summary>
[BroadcastReceiver(Label = "@string/widget_label", Exported = true)]
[IntentFilter(new[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
[MetaData("android.appwidget.provider", Resource = "@xml/daily_widget_info")]
public class DailyWidgetProvider : AppWidgetProvider
{
    /// <summary>Своё намерение: приложение просит перерисовать виджет после правок.</summary>
    public const string ActionRefresh = "mydayquest.widget.REFRESH";

    /// <summary>Нажата галочка у цели — отметить её выполненной.</summary>
    public const string ActionDone = "mydayquest.widget.DONE";

    public const string ExtraSubtaskId = "subtaskId";

    private const int Rows = 4;

    public override void OnUpdate(Context? context, AppWidgetManager? manager, int[]? ids)
    {
        if (context is null || manager is null || ids is null) return;

        // Без GoAsync система вправе прибить приёмник сразу после возврата,
        // и чтение базы не успеет закончиться — виджет останется пустым.
        var pending = GoAsync();
        _ = RenderAsync(context, manager, ids).ContinueWith(_ => pending.Finish());
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);
        if (context is null) return;

        if (intent?.Action == ActionDone)
        {
            var subtaskId = intent.GetIntExtra(ExtraSubtaskId, 0);
            if (subtaskId > 0)
            {
                var pendingDone = GoAsync();
                _ = MarkDoneAsync(context, subtaskId).ContinueWith(_ => pendingDone.Finish());
            }
            return;
        }

        if (intent?.Action != ActionRefresh) return;

        var manager = AppWidgetManager.GetInstance(context);
        var ids = manager?.GetAppWidgetIds(new ComponentName(context,
            Java.Lang.Class.FromType(typeof(DailyWidgetProvider))));
        if (manager is not null && ids is { Length: > 0 })
        {
            var pending = GoAsync();
            _ = RenderAsync(context, manager, ids).ContinueWith(_ => pending.Finish());
        }
    }

    /// <summary>Попросить систему перерисовать все копии виджета.</summary>
    public static void Refresh()
    {
        var ctx = Platform.AppContext;
        var intent = new Intent(ctx, typeof(DailyWidgetProvider));
        intent.SetAction(ActionRefresh);
        ctx.SendBroadcast(intent);
    }

    /// <summary>
    /// Отметить цель выполненной и сразу перерисовать виджет. Работает, даже
    /// когда приложение закрыто: база своя, приложение для этого не нужно.
    /// </summary>
    private static async Task MarkDoneAsync(Context ctx, int subtaskId)
    {
        try
        {
            var db = new AppDatabase(FileSystem.AppDataDirectory);
            var sub = await db.GetSubtaskAsync(subtaskId);
            if (sub is null || sub.IsDone) return;

            sub.IsDone = true;
            await db.SaveSubtaskAsync(sub);          // отметку времени ставит он сам
            await db.RefreshTaskProgressAsync(sub.TaskId);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("MyDayQuest.Widget", ex.ToString());
        }

        var manager = AppWidgetManager.GetInstance(ctx);
        var ids = manager?.GetAppWidgetIds(new ComponentName(ctx,
            Java.Lang.Class.FromType(typeof(DailyWidgetProvider))));
        if (manager is not null && ids is { Length: > 0 })
            await RenderAsync(ctx, manager, ids);
    }

    private static async Task RenderAsync(Context ctx, AppWidgetManager manager, int[] ids)
    {
        try
        {
            var db = new AppDatabase(FileSystem.AppDataDirectory);
            var tasks = await db.GetDailyPlanTasksAsync();
            foreach (var repeating in await db.GetRepeatingTasksAsync())
                if (tasks.All(t => t.Id != repeating.Id))
                    tasks.Add(repeating);

            var overdue = tasks.Count(t => t.IsOverdue);
            var header = tasks.Count == 0
                ? "ЗАДАНИЯ НА СЕГОДНЯ"
                : overdue > 0
                    ? $"НА СЕГОДНЯ: {tasks.Count} · ПРОСРОЧЕНО {overdue}"
                    : $"ЗАДАНИЯ НА СЕГОДНЯ: {tasks.Count}";

            var views = new RemoteViews(ctx.PackageName, Resource.Layout.daily_widget);
            views.SetTextViewText(Resource.Id.widget_header, header);

            // Список наполняет служба строк.
            var service = new Intent(ctx, typeof(DailyWidgetService));
            views.SetRemoteAdapter(Resource.Id.widget_list, service);
            views.SetEmptyView(Resource.Id.widget_list, Resource.Id.widget_empty);

            // Одна заготовка намерения на весь список: строка лишь дополняет её
            // своим номером цели. Своё намерение на строку система не разрешает.
            var template = new Intent(ctx, typeof(DailyWidgetProvider));
            template.SetAction(ActionDone);
            var templatePending = PendingIntent.GetBroadcast(ctx, 0, template,
                PendingIntentFlags.Mutable | PendingIntentFlags.UpdateCurrent);
            views.SetPendingIntentTemplate(Resource.Id.widget_list, templatePending);

            var launch = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);
            if (launch is not null)
            {
                var open = PendingIntent.GetActivity(ctx, 0, launch,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
                views.SetOnClickPendingIntent(Resource.Id.widget_header, open);
            }

            manager.UpdateAppWidget(ids, views);
            manager.NotifyAppWidgetViewDataChanged(ids, Resource.Id.widget_list);
        }
        catch (Exception ex)
        {
            // Ронять процесс из-за виджета не стоит, но и молчать нельзя:
            // пустой виджет без следа в журнале не отличить от пустого плана.
            global::Android.Util.Log.Error("MyDayQuest.Widget", ex.ToString());
        }
    }
}
