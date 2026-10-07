namespace MyDayQuest.Services;

/// <summary>
/// Виджет рабочего стола. Есть только на Android; на остальных платформах
/// вызовы молчат, чтобы не обкладывать условной компиляцией вызывающий код.
/// </summary>
public static class WidgetService
{
    /// <summary>Перерисовать виджет — после любой правки дневного плана.</summary>
    public static void Refresh()
    {
#if ANDROID
        try { Platforms.Android.DailyWidgetProvider.Refresh(); }
        catch { /* виджета может не быть на рабочем столе — это нормально */ }
#endif
    }
}
