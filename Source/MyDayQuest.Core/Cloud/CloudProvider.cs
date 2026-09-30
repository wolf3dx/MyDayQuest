namespace MyDayQuest.Cloud;

/// <summary>Куда синхронизируем данные.</summary>
public enum CloudProvider
{
    /// <summary>Синхронизация выключена.</summary>
    None = 0,

    /// <summary>Обычный файл на диске (в т.ч. в папке клиента облака) — прежний режим.</summary>
    LocalFile = 1,

    YandexDisk = 2,
    OneDrive = 3,
    GoogleDrive = 4,

    /// <summary>Любой WebDAV-сервер: вход логином и паролем, без регистрации приложения.</summary>
    WebDav = 5,
}

public static class CloudProviderNames
{
    public static string Display(this CloudProvider p) => p switch
    {
        CloudProvider.LocalFile => "Файл (.mdq)",
        CloudProvider.YandexDisk => "Яндекс.Диск",
        CloudProvider.OneDrive => "OneDrive",
        CloudProvider.GoogleDrive => "Google Drive",
        CloudProvider.WebDav => "WebDAV (логин и пароль)",
        _ => "Выключено",
    };
}
