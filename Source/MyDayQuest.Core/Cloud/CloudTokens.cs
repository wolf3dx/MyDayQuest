using System.Text.Json;

namespace MyDayQuest.Cloud;

/// <summary>Выданные провайдером токены.</summary>
public class CloudTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresUtc.AddMinutes(-2);
    public bool HasAccess => !string.IsNullOrEmpty(AccessToken);
}

public interface ITokenStore
{
    CloudTokens? Get(CloudProvider provider);
    void Save(CloudProvider provider, CloudTokens tokens);
    void Clear(CloudProvider provider);
}

/// <summary>
/// Токены в JSON-файле каталога данных приложения. Не шифруется: файл лежит в
/// профиле пользователя и в репозиторий не попадает.
/// </summary>
public class FileTokenStore : ITokenStore
{
    private readonly string _path;
    private Dictionary<string, CloudTokens> _cache;

    public FileTokenStore(string folder)
    {
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "cloud-tokens.json");
        _cache = Read();
    }

    public CloudTokens? Get(CloudProvider provider) =>
        _cache.TryGetValue(provider.ToString(), out var t) ? t : null;

    public void Save(CloudProvider provider, CloudTokens tokens)
    {
        _cache[provider.ToString()] = tokens;
        Write();
    }

    public void Clear(CloudProvider provider)
    {
        _cache.Remove(provider.ToString());
        Write();
    }

    private Dictionary<string, CloudTokens> Read()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<Dictionary<string, CloudTokens>>(File.ReadAllText(_path))
                       ?? new();
        }
        catch { /* битый файл — просто просим войти заново */ }
        return new();
    }

    private void Write()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_cache)); }
        catch { /* нет доступа к профилю — токены проживут только в памяти */ }
    }
}
