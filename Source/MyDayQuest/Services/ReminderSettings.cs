namespace MyDayQuest.Services;

/// <summary>Настройки напоминания о заданиях на сегодня.</summary>
public static class ReminderSettings
{
    private const string EnabledKey = "ReminderEnabled";
    private const string HourKey = "ReminderHour";
    private const string MinuteKey = "ReminderMinute";
    private const string BootKey = "ReminderStartOnBoot";

    public static bool Enabled
    {
        get => Preferences.Get(EnabledKey, false);
        set => Preferences.Set(EnabledKey, value);
    }

    /// <summary>
    /// Переживать перезагрузку телефона. По умолчанию выключено: это автозапуск,
    /// и включать его можно только по прямой воле пользователя.
    /// </summary>
    public static bool StartOnBoot
    {
        get => Preferences.Get(BootKey, false);
        set => Preferences.Set(BootKey, value);
    }

    /// <summary>Время напоминания. По умолчанию девять утра.</summary>
    public static TimeSpan Time
    {
        get => new(Preferences.Get(HourKey, 9), Preferences.Get(MinuteKey, 0), 0);
        set
        {
            Preferences.Set(HourKey, value.Hours);
            Preferences.Set(MinuteKey, value.Minutes);
        }
    }
}
