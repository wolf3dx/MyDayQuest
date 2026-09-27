using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.ViewModels;

/// <summary>
/// Вкладка листа в нижней ленте (наслаивающиеся карточки).
/// </summary>
public partial class QuestTab : ObservableObject
{
    public int QuestId { get; init; }

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Выделена / открыта в среднем поле.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Цвет карточки (пастельный, назначается по кругу).</summary>
    public string ColorHex { get; init; } = "#EDEDED";
}
