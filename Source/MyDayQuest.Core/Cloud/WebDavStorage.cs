using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace MyDayQuest.Cloud;

/// <summary>
/// Хранилище по протоколу WebDAV — вход по логину и паролю, без регистрации
/// приложения у провайдера. Так работают Яндекс.Диск (webdav.yandex.ru),
/// Nextcloud, ownCloud и прочие серверы с поддержкой WebDAV.
/// </summary>
public class WebDavStorage : ICloudStorage
{
    /// <summary>Готовые адреса, чтобы пользователю не искать их самому.</summary>
    public const string YandexServer = "https://webdav.yandex.ru";

    private const string Folder = "MyDayQuest";
    private static readonly HttpMethod Propfind = new("PROPFIND");
    private static readonly HttpMethod Mkcol = new("MKCOL");

    private readonly HttpClient _http;
    private readonly ITokenStore _tokens;

    public WebDavStorage(HttpClient http, ITokenStore tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public CloudProvider Provider => CloudProvider.WebDav;

    /// <summary>Настраивать нечего: адрес сервера вводит сам пользователь.</summary>
    public bool IsConfigured => true;

    public bool IsSignedIn => Account is not null;

    public string RemoteLocation
    {
        get
        {
            var a = Account;
            return a is null
                ? $"WebDAV → {Folder}/{CloudStorageBase.RemoteFileName}"
                : $"{Host(a.Server)} → {Folder}/{CloudStorageBase.RemoteFileName}";
        }
    }

    private WebDavAccount? Account => _tokens.Get(CloudProvider.WebDav)?.WebDav;

    private static string Host(string server) =>
        Uri.TryCreate(server, UriKind.Absolute, out var u) ? u.Host : server;

    /// <summary>Вход выполняется не через браузер — см. <see cref="ConnectAsync"/>.</summary>
    public Task SignInAsync(CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "Для WebDAV вход выполняется логином и паролем, а не через браузер.");

    /// <summary>
    /// Проверить доступ и запомнить учётные данные. Папка приложения создаётся,
    /// если её ещё нет.
    /// </summary>
    public async Task ConnectAsync(string server, string user, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(server)) throw new ArgumentException("Не указан адрес сервера.");
        if (string.IsNullOrWhiteSpace(user)) throw new ArgumentException("Не указан логин.");

        server = server.Trim().TrimEnd('/');
        if (!server.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            server = "https://" + server;

        var account = new WebDavAccount { Server = server, User = user.Trim(), Password = password };

        // Проверяем доступ и заодно создаём папку приложения.
        using (var probe = new HttpRequestMessage(Propfind, $"{server}/"))
        {
            probe.Headers.Add("Depth", "0");
            Authorize(probe, account);
            using var resp = await _http.SendAsync(probe, ct);
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException(
                    "Сервер не принял логин или пароль. Если у аккаунта включена двухфакторная " +
                    "проверка, нужен пароль приложения, а не основной пароль.");
            if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.MultiStatus)
                throw new InvalidOperationException(
                    $"Сервер ответил {(int)resp.StatusCode} {resp.ReasonPhrase}. Проверьте адрес.");
        }

        using (var mkcol = new HttpRequestMessage(Mkcol, $"{server}/{Folder}"))
        {
            Authorize(mkcol, account);
            using var resp = await _http.SendAsync(mkcol, ct);
            // 405/409 — папка уже есть, это не ошибка.
            if (!resp.IsSuccessStatusCode
                && resp.StatusCode != HttpStatusCode.MethodNotAllowed
                && resp.StatusCode != HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException(
                    $"Не удалось создать папку {Folder}: {(int)resp.StatusCode} {resp.ReasonPhrase}.");
            }
        }

        _tokens.Save(CloudProvider.WebDav, new CloudTokens { WebDav = account });
    }

    public void SignOut() => _tokens.Clear(CloudProvider.WebDav);

    private static void Authorize(HttpRequestMessage request, WebDavAccount account)
    {
        var raw = Encoding.UTF8.GetBytes($"{account.User}:{account.Password}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
    }

    private WebDavAccount Require() =>
        Account ?? throw new InvalidOperationException("WebDAV: вход не выполнен.");

    private string FileUrl(WebDavAccount a) => $"{a.Server}/{Folder}/{CloudStorageBase.RemoteFileName}";

    public async Task<DateTime?> GetModifiedUtcAsync(CancellationToken ct = default)
    {
        var account = Require();
        using var req = new HttpRequestMessage(Propfind, FileUrl(account));
        req.Headers.Add("Depth", "0");
        Authorize(req, account);
        using var resp = await _http.SendAsync(req, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.MultiStatus)
            throw new InvalidOperationException(
                $"WebDAV: не удалось прочитать сведения о файле ({(int)resp.StatusCode}).");

        var body = await resp.Content.ReadAsStringAsync(ct);
        try
        {
            var dav = XNamespace.Get("DAV:");
            var value = XDocument.Parse(body).Descendants(dav + "getlastmodified").FirstOrDefault()?.Value;
            return value is not null && DateTimeOffset.TryParse(value, out var when)
                ? when.UtcDateTime
                : null;
        }
        catch { return null; }
    }

    public async Task<string?> DownloadAsync(CancellationToken ct = default)
    {
        var account = Require();
        using var req = new HttpRequestMessage(HttpMethod.Get, FileUrl(account));
        Authorize(req, account);
        using var resp = await _http.SendAsync(req, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"WebDAV: не удалось скачать файл ({(int)resp.StatusCode}).");

        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task UploadAsync(string json, CancellationToken ct = default)
    {
        var account = Require();
        using var req = new HttpRequestMessage(HttpMethod.Put, FileUrl(account))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        Authorize(req, account);
        using var resp = await _http.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"WebDAV: не удалось выгрузить файл ({(int)resp.StatusCode}).");
    }
}
