using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyDayQuest.Cloud;

/// <summary>Регистрационные данные приложения в облаке (client_id и т.п.).</summary>
public class CloudAppConfig
{
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Нужен только Google (тип клиента «Desktop»); у Яндекса и OneDrive — пусто.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Redirect URI для настольных ОС. Должен быть зарегистрирован у провайдера.</summary>
    public string DesktopRedirectUri { get; set; } = string.Empty;

    /// <summary>Redirect URI для Android/iOS (своя схема, напр. mydayquest://auth).</summary>
    public string MobileRedirectUri { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}

/// <summary>
/// Настройки облаков. Секреты в репозиторий не кладём: файл <c>cloud.config.json</c>
/// лежит рядом с исполняемым файлом либо в каталоге данных приложения и внесён в .gitignore;
/// в репозитории есть только <c>cloud.config.sample.json</c>.
/// Переменные окружения (MDQ_YANDEX_CLIENT_ID и т.п.) перекрывают файл.
/// </summary>
public class CloudConfig
{
    public const string FileName = "cloud.config.json";
    public const int DefaultLoopbackPort = 52411;

    public CloudAppConfig Yandex { get; set; } = new();
    public CloudAppConfig OneDrive { get; set; } = new();
    public CloudAppConfig Google { get; set; } = new();

    public CloudAppConfig For(CloudProvider provider) => provider switch
    {
        CloudProvider.YandexDisk => Yandex,
        CloudProvider.OneDrive => OneDrive,
        CloudProvider.GoogleDrive => Google,
        _ => new CloudAppConfig(),
    };

    /// <summary>Прочитать конфиг из первого найденного файла, затем наложить переменные окружения.</summary>
    public static CloudConfig Load(params string[] folders)
    {
        var cfg = new CloudConfig();

        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            var path = Path.Combine(folder, FileName);
            if (!File.Exists(path)) continue;
            try
            {
                var loaded = JsonSerializer.Deserialize<CloudConfig>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loaded is not null) { cfg = loaded; break; }
            }
            catch { /* повреждённый конфиг — работаем на переменных окружения */ }
        }

        Apply(cfg.Yandex, "MDQ_YANDEX");
        Apply(cfg.OneDrive, "MDQ_ONEDRIVE");
        Apply(cfg.Google, "MDQ_GOOGLE");

        var loopback = $"http://127.0.0.1:{DefaultLoopbackPort}/";
        foreach (var app in new[] { cfg.Yandex, cfg.OneDrive, cfg.Google })
            if (string.IsNullOrWhiteSpace(app.DesktopRedirectUri))
                app.DesktopRedirectUri = loopback;

        return cfg;
    }

    private static void Apply(CloudAppConfig app, string prefix)
    {
        var id = Environment.GetEnvironmentVariable($"{prefix}_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(id)) app.ClientId = id;

        var secret = Environment.GetEnvironmentVariable($"{prefix}_CLIENT_SECRET");
        if (!string.IsNullOrWhiteSpace(secret)) app.ClientSecret = secret;

        var redirect = Environment.GetEnvironmentVariable($"{prefix}_REDIRECT_URI");
        if (!string.IsNullOrWhiteSpace(redirect)) app.DesktopRedirectUri = redirect;
    }
}
