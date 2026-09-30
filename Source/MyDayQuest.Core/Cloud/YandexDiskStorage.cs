using System.Net;
using System.Text;
using System.Text.Json;

namespace MyDayQuest.Cloud;

/// <summary>
/// Яндекс.Диск. Файл кладётся в папку приложения (<c>app:/</c>) — у пользователя она видна
/// как «Диск → Приложения → MyDayQuest», а права ограничены только ею.
/// </summary>
public class YandexDiskStorage : CloudStorageBase
{
    private const string ApiBase = "https://cloud-api.yandex.net/v1/disk";
    private const string RemotePath = "app:/" + RemoteFileName;

    public static readonly OAuthEndpoints Endpoints = new(
        "https://oauth.yandex.ru/authorize",
        "https://oauth.yandex.ru/token",
        "cloud_api:disk.app_folder");

    public YandexDiskStorage(HttpClient http, CloudAppConfig app, ITokenStore tokens, Func<CloudAppConfig, IAuthBrowser> browser)
        : base(http, app, tokens, browser, Endpoints) { }

    public override CloudProvider Provider => CloudProvider.YandexDisk;
    public override string RemoteLocation => "Яндекс.Диск → Приложения → MyDayQuest → " + RemoteFileName;
    protected override string AuthScheme => "OAuth";

    private static string Resource(string tail) =>
        $"{ApiBase}/resources{tail}path={Uri.EscapeDataString(RemotePath)}";

    public override async Task<DateTime?> GetModifiedUtcAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Resource("?") + "&fields=modified");
        using var resp = await SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureOkAsync(resp, "Яндекс.Диск: не удалось прочитать сведения о файле", ct);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("modified", out var m) && m.GetString() is { } s
            ? DateTimeOffset.Parse(s).UtcDateTime
            : null;
    }

    public override async Task<string?> DownloadAsync(CancellationToken ct = default)
    {
        using var linkReq = new HttpRequestMessage(HttpMethod.Get, Resource("/download?"));
        using var linkResp = await SendAsync(linkReq, ct);
        if (linkResp.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureOkAsync(linkResp, "Яндекс.Диск: не удалось получить ссылку на скачивание", ct);

        var href = ReadHref(await linkResp.Content.ReadAsStringAsync(ct));
        using var fileResp = await Http.GetAsync(href, ct);
        await EnsureOkAsync(fileResp, "Яндекс.Диск: не удалось скачать файл", ct);
        return await fileResp.Content.ReadAsStringAsync(ct);
    }

    public override async Task UploadAsync(string json, CancellationToken ct = default)
    {
        using var linkReq = new HttpRequestMessage(HttpMethod.Get, Resource("/upload?") + "&overwrite=true");
        using var linkResp = await SendAsync(linkReq, ct);
        await EnsureOkAsync(linkResp, "Яндекс.Диск: не удалось получить ссылку на загрузку", ct);

        var href = ReadHref(await linkResp.Content.ReadAsStringAsync(ct));
        using var put = new HttpRequestMessage(HttpMethod.Put, href)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        using var putResp = await Http.SendAsync(put, ct);
        await EnsureOkAsync(putResp, "Яндекс.Диск: не удалось выгрузить файл", ct);
    }

    private static string ReadHref(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("href").GetString()
               ?? throw new InvalidOperationException("Яндекс.Диск не вернул ссылку на файл.");
    }
}
