namespace MyDayQuest.Cloud;

/// <summary>Облачное хранилище одного файла синхронизации <c>MyDayQuest.mdq</c>.</summary>
public interface ICloudStorage
{
    CloudProvider Provider { get; }

    /// <summary>Заданы ли client_id и redirect_uri (см. cloud.config.json).</summary>
    bool IsConfigured { get; }

    /// <summary>Есть ли сохранённые токены (вход уже выполнялся).</summary>
    bool IsSignedIn { get; }

    /// <summary>Где именно в облаке лежит файл — для показа пользователю.</summary>
    string RemoteLocation { get; }

    Task SignInAsync(CancellationToken ct = default);
    void SignOut();

    /// <summary>Время изменения файла в облаке; <c>null</c> — файла ещё нет.</summary>
    Task<DateTime?> GetModifiedUtcAsync(CancellationToken ct = default);

    Task<string?> DownloadAsync(CancellationToken ct = default);
    Task UploadAsync(string json, CancellationToken ct = default);
}
