using MyDayQuest.ViewModels;

namespace MyDayQuest.Views;

public partial class QuestEditPage : ContentPage
{
    public QuestEditPage(QuestEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
