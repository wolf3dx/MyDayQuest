using Android.Content;

namespace MyDayQuest.Platforms.Android;

/// <summary>Установка скачанного обновления.</summary>
public static class Installer
{
    /// <summary>
    /// Разрешено ли приложению ставить пакеты. Одного объявления разрешения в
    /// манифесте мало: пользователь ещё должен разрешить это именно нам —
    /// иначе установщик закрывается, ничего не показав.
    /// </summary>
    public static bool CanInstall
        => global::Android.App.Application.Context.PackageManager?.CanRequestPackageInstalls() ?? false;

    /// <summary>Открыть системный экран, где это разрешение выдают.</summary>
    public static void OpenPermissionSettings()
    {
        var ctx = global::Android.App.Application.Context;
        var intent = new Intent(
            global::Android.Provider.Settings.ActionManageUnknownAppSources,
            global::Android.Net.Uri.Parse("package:" + ctx.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        ctx.StartActivity(intent);
    }
}
