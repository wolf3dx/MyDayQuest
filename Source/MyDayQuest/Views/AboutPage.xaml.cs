namespace MyDayQuest.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        // Версию берём у сборки, чтобы она не расходилась с выпуском.
        VersionLabel.Text = $"Версия {AppVersion.Display}";
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(SettingsPage));
}
