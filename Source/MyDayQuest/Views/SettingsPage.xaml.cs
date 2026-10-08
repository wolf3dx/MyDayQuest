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
        BootSwitch.IsToggled = ReminderSettings.StartOnBoot;
        TimeRow.IsVisible = ReminderSettings.Enabled;
        BootRow.IsVisible = ReminderSettings.Enabled;
        ReminderNote.IsVisible = ReminderSettings.Enabled;
        SkinQuestOption.IsChecked = SkinService.Current == Skin.Quest;
        SkinClassicOption.IsChecked = SkinService.Current == Skin.Classic;
        _loading = false;

        UpdateNote();
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
        BootRow.IsVisible = e.Value;
        ReminderNote.IsVisible = e.Value;

        if (e.Value) ReminderService.Reschedule();
        else ReminderService.Cancel();
        ReminderService.ApplyStartOnBoot();
        UpdateNote();
    }

    private void OnBootToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading) return;

        ReminderSettings.StartOnBoot = e.Value;
        ReminderService.ApplyStartOnBoot();
        UpdateNote();
    }

    /// <summary>Подпись честно говорит, что будет после перезагрузки.</summary>
    private void UpdateNote()
    {
        if (!ReminderService.Supported)
        {
            ReminderNote.Text = "Напоминания пока работают только на Android.";
            return;
        }

        ReminderNote.Text = ReminderSettings.StartOnBoot
            ? "После перезагрузки телефона напоминание включится само."
            : "После перезагрузки телефона напоминание включится снова, когда вы откроете приложение.";
    }

    private void OnSkinChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_loading || !e.Value) return;

        SkinService.Apply(SkinClassicOption.IsChecked ? Skin.Classic : Skin.Quest);
    }

    private void OnTimeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_loading || e.PropertyName != nameof(TimePicker.Time)) return;

        ReminderSettings.Time = ReminderTime.Time ?? ReminderSettings.Time;
        if (ReminderSettings.Enabled) ReminderService.Reschedule();
    }
}
