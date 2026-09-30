using System.Net;
using System.Text;
using System.Text.Json;

namespace MyDayQuest.Cloud;

/// <summary>
/// Google Drive. Область <c>drive.file</c> даёт доступ только к файлам, созданным самим
/// приложением, при этом файл виден пользователю в «Мой диск».
/// </summary>
public class GoogleDriveStorage : CloudStorageBase
{
    private const string FilesApi = "https://www.googleapis.com/drive/v3/files";
    private const string UploadApi = "https://www.googleapis.com/upload/drive/v3/files";

    public static readonly OAuthEndpoints Endpoints = new(
        "https://accounts.google.com/o/oauth2/v2/auth",
        "https://oauth2.googleapis.com/token",
        "https://www.googleapis.com/auth/drive.file",
        "&access_type=offline&prompt=consent");

    private string? _fileId;

    public GoogleDriveStorage(HttpClient http, CloudAppConfig app, ITokenStore tokens, Func<CloudAppConfig, IAuthBrowser> browser)
        : base(http, app, tokens, browser, Endpoints) { }

    public override CloudProvider Provider => CloudProvider.GoogleDrive;
    public override string RemoteLocation => "Google Drive → Мой диск → " + RemoteFileName;

    /// <summary>Найти файл по имени среди созданных приложением; кешируем id.</summary>
    private async Task<(string Id, DateTime Modified)?> FindAsync(CancellationToken ct)
    {
        var query = Uri.EscapeDataString($"name = '{RemoteFileName}' and trashed = false");
        var url = $"{FilesApi}?q={query}&fields={Uri.EscapeDataString("files(id,modifiedTime)")}" +
                  "&orderBy=modifiedTime desc&pageSize=1";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await SendAsync(req, ct);
        await EnsureOkAsync(resp, "Google Drive: не удалось найти файл", ct);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var files = doc.RootElement.GetProperty("files");
        if (files.GetArrayLength() == 0) { _fileId = null; return null; }

        var first = files[0];
        _fileId = first.GetProperty("id").GetString();
        var modified = DateTimeOffset.Parse(first.GetProperty("modifiedTime").GetString()!).UtcDateTime;
        return (_fileId!, modified);
    }

    public override async Task<DateTime?> GetModifiedUtcAsync(CancellationToken ct = default) =>
        (await FindAsync(ct))?.Modified;

    public override async Task<string?> DownloadAsync(CancellationToken ct = default)
    {
        var found = await FindAsync(ct);
        if (found is null) return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{FilesApi}/{found.Value.Id}?alt=media");
        using var resp = await SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureOkAsync(resp, "Google Drive: не удалось скачать файл", ct);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public override async Task UploadAsync(string json, CancellationToken ct = default)
    {
        if (_fileId is null)
            await FindAsync(ct);

        if (_fileId is null)
            _fileId = await CreateEmptyAsync(ct);

        using var req = new HttpRequestMessage(HttpMethod.Patch, $"{UploadApi}/{_fileId}?uploadType=media")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        using var resp = await SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            // Файл удалили из Диска — создаём заново и повторяем один раз.
            _fileId = await CreateEmptyAsync(ct);
            using var retry = new HttpRequestMessage(HttpMethod.Patch, $"{UploadApi}/{_fileId}?uploadType=media")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            using var retryResp = await SendAsync(retry, ct);
            await EnsureOkAsync(retryResp, "Google Drive: не удалось выгрузить файл", ct);
            return;
        }
        await EnsureOkAsync(resp, "Google Drive: не удалось выгрузить файл", ct);
    }

    private async Task<string> CreateEmptyAsync(CancellationToken ct)
    {
        var metadata = JsonSerializer.Serialize(new { name = RemoteFileName });
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{FilesApi}?fields=id")
        {
            Content = new StringContent(metadata, Encoding.UTF8, "application/json"),
        };
        using var resp = await SendAsync(req, ct);
        await EnsureOkAsync(resp, "Google Drive: не удалось создать файл", ct);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("id").GetString()
               ?? throw new InvalidOperationException("Google Drive не вернул id файла.");
    }
}
