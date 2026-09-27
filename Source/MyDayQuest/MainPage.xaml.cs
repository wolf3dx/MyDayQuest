using MyDayQuest.ViewModels;
using MyDayQuest.Views;

namespace MyDayQuest;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
        await _vm.RefreshOpenListAsync();
    }

    private async void OnAboutClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AboutPage));
    }

    private async void OnPlanChecked(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox cb && cb.BindingContext is ViewModels.TaskItemVM card)
            await _vm.SetDailyPlanCommand.ExecuteAsync(card);
    }

    private async void OnDailySubtaskChecked(object? sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return;
        if (sender is CheckBox cb && cb.BindingContext is ViewModels.DailyTaskVM card)
        {
            await _vm.ToggleDailySubtaskCommand.ExecuteAsync(card);
            cb.IsChecked = false;
        }
    }

    private async void OnCurrentSubtaskChecked(object? sender, CheckedChangedEventArgs e)
    {
        // Реагируем только на постановку галочки (сброс делаем сами — его игнорируем).
        if (!e.Value) return;
        if (sender is CheckBox cb && cb.BindingContext is ViewModels.TaskItemVM card)
        {
            await _vm.ToggleCurrentSubtaskCommand.ExecuteAsync(card);
            cb.IsChecked = false; // готовим чек-бокс к следующему подзаданию
        }
    }
}
