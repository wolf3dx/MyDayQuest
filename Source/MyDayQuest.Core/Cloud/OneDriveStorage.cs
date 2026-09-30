using System.Net;
using System.Text;
using System.Text.Json;

namespace MyDayQuest.Cloud;

/// <summary>
/// OneDrive через Microsoft Graph. Файл лежит в папке приложения (<c>approot</c>) —
/// у пользователя это «OneDrive → Приложения → MyDayQuest»; права ограничены ею.
/// </summary>
public class OneDriveStorage : CloudStorageBase
{
    private const string ItemUrl =
        "https://graph.microsoft.com/v1.0/me/drive/special/approot:/" + RemoteFileName;

    public static readonly OAuthEndpoints Endpoints = new(
        "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
        "https://login.microsoftonline.com/common/oauth2/v2.0/token",
        "Files.ReadWrite.AppFolder offline_access");

    public OneDriveStorage(HttpClient http, CloudAppConfig app, ITokenStore tokens, Func<CloudAppConfig, IAuthBrowser> browser)
        : base(http, app, tokens, browser, Endpoints) { }

    public override CloudProvider Provider => CloudProvider.OneDrive;
    public override string RemoteLocation => "OneDrive → Приложения → MyDayQuest → " + RemoteFileName;

    public override async Task<DateTime?> GetModifiedUtcAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ItemUrl);
        using var resp = await SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureOkAsync(resp, "OneDrive: не удалось прочитать сведения о файле", ct);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("lastModifiedDateTime", out var m) && m.GetString() is { } s
            ? DateTimeOffset.Parse(s).UtcDateTime
            : null;
    }

    public override async Task<string?> DownloadAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ItemUrl + ":/content");
        using var resp = await SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureOkAsync(resp, "OneDrive: не удалось скачать файл", ct);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public override async Task UploadAsync(string json, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, ItemUrl + ":/content")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        using var resp = await SendAsync(req, ct);
        await EnsureOkAsync(resp, "OneDrive: не удалось выгрузить файл", ct);
    }
}
