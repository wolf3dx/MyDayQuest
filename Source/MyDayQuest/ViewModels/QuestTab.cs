using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.ViewModels;

/// <summary>
/// Вкладка листа в нижней ленте (наслаивающиеся карточки).
/// </summary>
public partial class QuestTab : ObservableObject
{
    public int QuestId { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortName))]
    private string _name = string.Empty;

    /// <summary>
    /// Название для закладки. Обрезаем в коде: у MAUI на Android LineBreakMode
    /// внутри карточки фиксированной ширины не срабатывает и текст вылезает за края.
    /// </summary>
    public string ShortName => Name.Length > MaxTabNameLength
        ? Name[..(MaxTabNameLength - 1)].TrimEnd() + "…"
        : Name;

    private const int MaxTabNameLength = 15;

    /// <summary>Выделена / открыта в среднем поле.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Цвет карточки (пастельный, назначается по кругу).</summary>
    public string ColorHex { get; init; } = "#EDEDED";
}
