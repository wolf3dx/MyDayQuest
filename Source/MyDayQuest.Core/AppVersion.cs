using System.Reflection;

namespace MyDayQuest;

/// <summary>Текущая версия приложения (из AssemblyInformationalVersion головы).</summary>
public static class AppVersion
{
    public static string Current
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info)) return info;
            return asm.GetName().Version?.ToString() ?? "0.0";
        }
    }
}
