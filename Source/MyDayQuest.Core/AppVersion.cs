using System.Reflection;

namespace MyDayQuest;

/// <summary>Текущая версия приложения (из AssemblyInformationalVersion головы).</summary>
public static class AppVersion
{
    /// <summary>
    /// Версия, которую голова сообщает о себе при запуске.
    ///
    /// Без неё на Android версия бралась у ядра: там у процесса нет «входной»
    /// сборки, запасной путь приводил к MyDayQuest.Core с его 1.0.0, и
    /// проверка обновлений считала, что установлено новее любого выпуска.
    /// </summary>
    public static string? Reported { get; set; }

    public static string Current
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Reported)) return Reported!;
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info)) return info;
            return asm.GetName().Version?.ToString() ?? "0.0";
        }
    }

    /// <summary>Версия для показа человеку: без «+хеш коммита», который
    /// сборка дописывает к информационной версии.</summary>
    public static string Display => Current.Split('+')[0];
}
