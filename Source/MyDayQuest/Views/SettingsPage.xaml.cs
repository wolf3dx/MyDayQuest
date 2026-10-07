using MyDayQuest.Services;

namespace MyDayQuest.Views;

public partial class SettingsPage : ContentPage
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();

        _loading = true;
        ReminderSwitch.IsToggled = ReminderSettings.Enabled;
        ReminderTime.Time = ReminderSettings.Time;
        ReminderSwitch.IsEnabled = ReminderService.Supported;
        TimeRow.IsVisible = ReminderSettings.Enabled;
        ReminderNote.IsVisible = ReminderSettings.Enabled;
        _loading = false;

        if (!ReminderService.Supported)
            ReminderNote.Text = "Напоминания пока работают только на Android.";
    }

    private async void OnReminderToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading) return;

        if (e.Value)
        {
            // Без разрешения уведомление просто не покажется, а переключатель
            // останется включённым — получится молчаливый обман.
            if (!await ReminderService.RequestPermissionAsync())
            {
                _loading = true;
                ReminderSwitch.IsToggled = false;
                _loading = false;
                await DisplayAlertAsync("Напоминание",
                    "Без разрешения на уведомления напоминать не получится. "
                    + "Разрешение можно выдать в настройках телефона.", "OK");
                return;
            }
        }

        ReminderSettings.Enabled = e.Value;
        TimeRow.IsVisible = e.Value;
        ReminderNote.IsVisible = e.Value;

        if (e.Value) ReminderService.Reschedule();
        else ReminderService.Cancel();
    }

    private void OnTimeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_loading || e.PropertyName != nameof(TimePicker.Time)) return;

        ReminderSettings.Time = ReminderTime.Time ?? ReminderSettings.Time;
        if (ReminderSettings.Enabled) ReminderService.Reschedule();
    }
}
