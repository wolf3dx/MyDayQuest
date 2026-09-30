using System.Text.Json;

namespace MyDayQuest.Cloud;

/// <summary>
/// Что приложение помнит между запусками про облачную синхронизацию: выбранный провайдер,
/// отпечаток последних синхронизированных данных и время изменения файла в облаке.
/// Хранится рядом с базой, одинаково для MAUI и Avalonia.
/// </summary>
public class CloudSyncState
{
    public CloudProvider Provider { get; set; } = CloudProvider.None;
    public string LastSyncedHash { get; set; } = string.Empty;
    public DateTime? LastRemoteModifiedUtc { get; set; }
    public DateTime? LastSyncUtc { get; set; }

    private string? _path;

    public static CloudSyncState Load(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "cloud-sync.json");
        CloudSyncState state;
        try
        {
            state = File.Exists(path)
                ? JsonSerializer.Deserialize<CloudSyncState>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch { state = new(); }
        state._path = path;
        return state;
    }

    public void Save()
    {
        if (_path is null) return;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(this)); }
        catch { /* профиль недоступен — состояние проживёт до конца сеанса */ }
    }
}
