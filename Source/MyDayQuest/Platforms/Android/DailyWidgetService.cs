using Android.App;
using Android.Content;
using Android.Widget;
using MyDayQuest.Data;
using MyDayQuest.Models;

namespace MyDayQuest.Platforms.Android;

/// <summary>
/// Поставщик строк для списка виджета. Нужен потому, что прокручиваемый
/// список в виджете наполняется не напрямую, а через такую службу.
/// </summary>
[Service(Exported = true, Permission = "android.permission.BIND_REMOTEVIEWS")]
public class DailyWidgetService : RemoteViewsService
{
    public override IRemoteViewsFactory OnGetViewFactory(Intent? intent)
        => new DailyWidgetFactory(ApplicationContext!);
}

/// <summary>
/// Готовит строки списка. Данные читает сам: приложение в этот момент обычно
/// закрыто. Весь список собирается один раз в OnDataSetChanged, а не по строке
/// на запрос — иначе каждая строка стоила бы похода в базу.
/// </summary>
public class DailyWidgetFactory(Context ctx) : Java.Lang.Object, RemoteViewsService.IRemoteViewsFactory
{
    private sealed record Row(string Title, string Sub, bool IsOverdue, int GoalId, string GoalTitle);

    private List<Row> _rows = [];

    public int Count => _rows.Count;
    public bool HasStableIds => true;
    public int ViewTypeCount => 1;
    public RemoteViews? LoadingView => null;

    public long GetItemId(int position) => position;

    public void OnCreate() { }

    public void OnDataSetChanged()
    {
        try
        {
            _rows = BuildAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("MyDayQuest.Widget", ex.ToString());
            _rows = [];
        }
    }

    private static async Task<List<Row>> BuildAsync()
    {
        var db = new AppDatabase(FileSystem.AppDataDirectory);
        var tasks = await db.GetDailyPlanTasksAsync();

        // Повторяющееся задание попадает в виджет само, как и в напоминание.
        foreach (var repeating in await db.GetRepeatingTasksAsync())
            if (tasks.All(t => t.Id != repeating.Id))
                tasks.Add(repeating);

        var names = await db.GetQuestNamesAsync();
        var rows = new List<Row>();

        foreach (var t in tasks)
        {
            var list = names.TryGetValue(t.QuestId, out var n) ? n : "—";
            var mark = t.IsRepeating ? " · каждый день" : string.Empty;
            var sub = t.IsOverdue
                ? $"{list} · не сделано с {t.OverdueSince:dd.MM.yy}"
                : $"{list} · {t.Progress} %{mark}";

            var goal = (await db.GetSubtasksAsync(t.Id)).FirstOrDefault(x => !x.IsDone);
            rows.Add(new Row(t.Title, sub, t.IsOverdue, goal?.Id ?? 0, goal?.Title ?? string.Empty));
        }

        return rows;
    }

    public RemoteViews GetViewAt(int position)
    {
        var views = new RemoteViews(ctx.PackageName, Resource.Layout.widget_item);
        if (position < 0 || position >= _rows.Count) return views;

        var row = _rows[position];
        views.SetTextViewText(Resource.Id.item_title, row.Title);
        views.SetTextViewText(Resource.Id.item_sub, row.Sub);
        views.SetInt(Resource.Id.item_root, "setBackgroundResource", row.IsOverdue
            ? Resource.Drawable.widget_row_overdue_bg
            : Resource.Drawable.widget_row_bg);

        if (row.GoalId == 0)
        {
            views.SetViewVisibility(Resource.Id.item_goal_row, global::Android.Views.ViewStates.Gone);
            return views;
        }

        views.SetViewVisibility(Resource.Id.item_goal_row, global::Android.Views.ViewStates.Visible);
        views.SetTextViewText(Resource.Id.item_goal, row.GoalTitle);

        // Строки списка не заводят своих намерений: они дополняют заготовку,
        // которую поставщик виджета выдал всему списку разом.
        var fill = new Intent();
        fill.PutExtra(DailyWidgetProvider.ExtraSubtaskId, row.GoalId);
        views.SetOnClickFillInIntent(Resource.Id.item_check, fill);

        return views;
    }

    public void OnDestroy() => _rows = [];
}
