using MyDayQuest.Resources.Styles;

namespace MyDayQuest.Services;

public enum Skin
{
    /// <summary>Журнал заданий: кожа, пергамент, золото.</summary>
    Quest,

    /// <summary>Обычный вид MAUI: светлый фон, системный шрифт.</summary>
    Classic,
}

/// <summary>
/// Переключение оформления. Словарь скина подменяется целиком, поэтому все
/// скиновые ключи в разметке берутся через DynamicResource — StaticResource
/// подставляет значение один раз при разборе и переключения не заметит.
/// </summary>
public static class SkinService
{
    private const string Key = "Skin";

    public static Skin Current
    {
        get => Preferences.Get(Key, nameof(Skin.Quest)) == nameof(Skin.Classic)
            ? Skin.Classic
            : Skin.Quest;
        private set => Preferences.Set(Key, value.ToString());
    }

    /// <summary>Игровой скин рисует кожаную обложку под содержимым.</summary>
    public static bool IsQuest => Current == Skin.Quest;

    /// <summary>Скин сменился — головам пора перечитать то, что не на привязках.</summary>
    public static event Action? Changed;

    public static void Apply(Skin skin)
    {
        Current = skin;
        Reload();
        Changed?.Invoke();
    }

    /// <summary>Подставить словарь текущего скина вместо прежнего.</summary>
    public static void Reload()
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var wanted = Current == Skin.Classic
            ? new SkinClassic() as ResourceDictionary
            : new SkinQuest();

        var merged = resources.MergedDictionaries;
        var old = merged.FirstOrDefault(d => d is SkinQuest or SkinClassic);
        if (old is not null) merged.Remove(old);
        merged.Add(wanted);
    }
}
