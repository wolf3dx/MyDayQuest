using MyDayQuest.Data;

namespace MyDayQuest.Services;

/// <summary>
/// Запоминает, куда синхронизироваться, и предлагает выбрать это заново.
/// На Android хранится ссылка на документ (годится файл в облачном диске),
/// на остальных платформах — обычный путь к файлу.
/// </summary>
public static class SyncTargetStore
{
    private const string PathKey = "SyncFilePath";
    private const string UriKey = "SyncDocumentUri";

    /// <summary>Запомненная цель или null, если ещё ничего не выбирали.</summary>
    public static ISyncTarget? Current
    {
        get
        {
#if ANDROID
            var uri = Preferences.Get(UriKey, string.Empty);
            if (!string.IsNullOrEmpty(uri))
            {
                try { return new Platforms.Android.SafSyncTarget(uri); }
                catch { return null; }
            }
#endif
            var path = Preferences.Get(PathKey, string.Empty);
            return string.IsNullOrEmpty(path) ? null : new FileSyncTarget(path);
        }
    }

    /// <summary>Выбрать файл синхронизации. null — пользователь отказался.</summary>
    public static async Task<ISyncTarget?> PickAsync()
    {
#if ANDROID
        // Свой выбор вместо FilePicker: нужен доступ к самому файлу в облаке,
        // а FilePicker отдаёт копию в кэше приложения — запись в неё никуда не уедет.
        var uri = await MainActivity.PickDocumentAsync();
        if (string.IsNullOrEmpty(uri)) return null;

        Preferences.Set(UriKey, uri);
        Preferences.Remove(PathKey);
        return new Platforms.Android.SafSyncTarget(uri);
#else
        var pick = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Выберите файл синхронизации MyDayQuest",
        });
        if (pick is null) return null;

        Preferences.Set(PathKey, pick.FullPath);
        return new FileSyncTarget(pick.FullPath);
#endif
    }

    /// <summary>Запомнить путь, выбранный при сохранении.</summary>
    public static void RememberPath(string path)
    {
        Preferences.Set(PathKey, path);
#if ANDROID
        Preferences.Remove(UriKey);
#endif
    }
}
