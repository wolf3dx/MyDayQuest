namespace MyDayQuest.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(SettingsPage));
}
