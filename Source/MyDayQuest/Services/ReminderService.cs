namespace MyDayQuest.Services;

/// <summary>
/// Напоминание о заданиях на сегодня. Сейчас работает на Android; на остальных
/// платформах вызовы ничего не делают, чтобы страницу настроек не пришлось
/// обкладывать условной компиляцией.
/// </summary>
public static class ReminderService
{
    public static bool Supported =>
#if ANDROID
        true;
#else
        false;
#endif

    /// <summary>Спросить разрешение на уведомления. true — показывать можно.</summary>
    public static async Task<bool> RequestPermissionAsync()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted;
        }
        return true;
#else
        return await Task.FromResult(false);
#endif
    }

    /// <summary>Перевзвести будильник по текущим настройкам.</summary>
    public static void Reschedule()
    {
#if ANDROID
        Platforms.Android.Reminders.EnsureChannel();
        Platforms.Android.Reminders.Schedule();
#endif
    }

    public static void Cancel()
    {
#if ANDROID
        Platforms.Android.Reminders.Cancel();
#endif
    }
}
