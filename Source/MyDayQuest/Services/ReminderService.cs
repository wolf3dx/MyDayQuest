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

    /// <summary>Показать напоминание сейчас — чтобы убедиться, что оно дойдёт.</summary>
    public static async Task ShowNowAsync()
    {
#if ANDROID
        Platforms.Android.Reminders.EnsureChannel();
        await Platforms.Android.ReminderReceiver.ShowNowAsync();
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>Переживать ли перезагрузку телефона.</summary>
    public static void ApplyStartOnBoot()
    {
#if ANDROID
        Platforms.Android.Reminders.SetBootEnabled(
            ReminderSettings.Enabled && ReminderSettings.StartOnBoot);
#endif
    }
}
