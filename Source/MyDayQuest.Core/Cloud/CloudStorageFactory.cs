namespace MyDayQuest.Cloud;

/// <summary>Создаёт хранилище по выбранному провайдеру.</summary>
public class CloudStorageFactory
{
    private readonly HttpClient _http;
    private readonly CloudConfig _config;
    private readonly ITokenStore _tokens;
    private readonly Func<CloudAppConfig, IAuthBrowser> _browser;

    public CloudStorageFactory(HttpClient http, CloudConfig config, ITokenStore tokens, Func<CloudAppConfig, IAuthBrowser> browser)
    {
        _http = http;
        _config = config;
        _tokens = tokens;
        _browser = browser;
    }

    public ICloudStorage? Create(CloudProvider provider) => provider switch
    {
        CloudProvider.YandexDisk => new YandexDiskStorage(_http, _config.Yandex, _tokens, _browser),
        CloudProvider.OneDrive => new OneDriveStorage(_http, _config.OneDrive, _tokens, _browser),
        CloudProvider.GoogleDrive => new GoogleDriveStorage(_http, _config.Google, _tokens, _browser),
        _ => null,
    };

    public bool IsConfigured(CloudProvider provider) => _config.For(provider).IsConfigured;
}
