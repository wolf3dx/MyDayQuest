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

    private const string ExtraSubtaskId = "subtaskId";

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
            var names = await db.GetQuestNamesAsync();

            var views = new RemoteViews(ctx.PackageName, Resource.Layout.daily_widget);

            var titleIds = new[] { Resource.Id.title_0, Resource.Id.title_1, Resource.Id.title_2, Resource.Id.title_3 };
            var subIds = new[] { Resource.Id.sub_0, Resource.Id.sub_1, Resource.Id.sub_2, Resource.Id.sub_3 };
            var rowIds = new[] { Resource.Id.row_0, Resource.Id.row_1, Resource.Id.row_2, Resource.Id.row_3 };
            var goalRowIds = new[] { Resource.Id.goal_row_0, Resource.Id.goal_row_1, Resource.Id.goal_row_2, Resource.Id.goal_row_3 };
            var goalIds = new[] { Resource.Id.goal_0, Resource.Id.goal_1, Resource.Id.goal_2, Resource.Id.goal_3 };
            var checkIds = new[] { Resource.Id.check_0, Resource.Id.check_1, Resource.Id.check_2, Resource.Id.check_3 };

            var overdue = tasks.Count(t => t.IsOverdue);
            views.SetTextViewText(Resource.Id.widget_header,
                overdue > 0
                    ? $"НА СЕГОДНЯ: {tasks.Count} · ПРОСРОЧЕНО {overdue}"
                    : tasks.Count > 0 ? $"ЗАДАНИЯ НА СЕГОДНЯ: {tasks.Count}" : "ЗАДАНИЯ НА СЕГОДНЯ");

            views.SetViewVisibility(Resource.Id.widget_empty,
                tasks.Count == 0 ? global::Android.Views.ViewStates.Visible : global::Android.Views.ViewStates.Gone);

            for (var i = 0; i < Rows; i++)
            {
                if (i >= tasks.Count)
                {
                    views.SetViewVisibility(rowIds[i], global::Android.Views.ViewStates.Gone);
                    continue;
                }

                var t = tasks[i];
                var list = names.TryGetValue(t.QuestId, out var n) ? n : "—";

                views.SetViewVisibility(rowIds[i], global::Android.Views.ViewStates.Visible);
                views.SetTextViewText(titleIds[i], t.Title);
                views.SetTextViewText(subIds[i], t.IsOverdue
                    ? $"{list} · не сделано с {t.OverdueSince:dd.MM.yy}"
                    : $"{list} · {t.Progress} %{(t.IsRepeating ? " · каждый день" : string.Empty)}");

                // Просроченное выделяется и здесь — красной рамкой, как в приложении.
                views.SetInt(rowIds[i], "setBackgroundResource", t.IsOverdue
                    ? Resource.Drawable.widget_row_overdue_bg
                    : Resource.Drawable.widget_row_bg);

                // Текущая цель задания — первая невыполненная.
                var subs = await db.GetSubtasksAsync(t.Id);
                var goal = subs.FirstOrDefault(x => !x.IsDone);
                if (goal is null)
                {
                    views.SetViewVisibility(goalRowIds[i], global::Android.Views.ViewStates.Gone);
                    continue;
                }

                views.SetViewVisibility(goalRowIds[i], global::Android.Views.ViewStates.Visible);
                views.SetTextViewText(goalIds[i], goal.Title);
                views.SetImageViewResource(checkIds[i], Resource.Drawable.widget_check_off);

                // Своя ссылка у каждой галочки: без неё система считает намерения
                // одинаковыми и переиспользует первое — отмечалась бы всегда
                // верхняя цель, чью бы галочку ни нажали.
                var done = new Intent(ctx, typeof(DailyWidgetProvider));
                done.SetAction(ActionDone);
                done.PutExtra(ExtraSubtaskId, goal.Id);
                done.SetData(global::Android.Net.Uri.Parse($"mydayquest://subtask/{goal.Id}"));
                var donePending = PendingIntent.GetBroadcast(ctx, goal.Id, done,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
                views.SetOnClickPendingIntent(checkIds[i], donePending);
            }

            var launch = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);
            if (launch is not null)
            {
                var open = PendingIntent.GetActivity(ctx, 0, launch,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
                views.SetOnClickPendingIntent(Resource.Id.widget_root, open);
            }

            manager.UpdateAppWidget(ids, views);
        }
        catch (Exception ex)
        {
            // Ронять процесс из-за виджета не стоит, но и молчать нельзя:
            // пустой виджет без следа в журнале не отличить от пустого плана.
            global::Android.Util.Log.Error("MyDayQuest.Widget", ex.ToString());
        }
    }
}
