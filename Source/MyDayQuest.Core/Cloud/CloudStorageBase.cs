using System.Net;
using System.Net.Http.Headers;

namespace MyDayQuest.Cloud;

/// <summary>Общая часть провайдеров: хранение токенов, вход, обновление access_token.</summary>
public abstract class CloudStorageBase : ICloudStorage
{
    /// <summary>Имя файла синхронизации в облаке — одинаковое у всех провайдеров.</summary>
    public const string RemoteFileName = "MyDayQuest.mdq";

    protected readonly HttpClient Http;
    protected readonly CloudAppConfig App;
    private readonly ITokenStore _tokens;
    private readonly Func<CloudAppConfig, IAuthBrowser> _browserFactory;
    private readonly PkceOAuthClient _oauth;

    protected CloudStorageBase(HttpClient http, CloudAppConfig app, ITokenStore tokens,
        Func<CloudAppConfig, IAuthBrowser> browserFactory, OAuthEndpoints endpoints)
    {
        Http = http;
        App = app;
        _tokens = tokens;
        _browserFactory = browserFactory;
        _oauth = new PkceOAuthClient(http, endpoints, app);
    }

    public abstract CloudProvider Provider { get; }
    public abstract string RemoteLocation { get; }

    public bool IsConfigured => App.IsConfigured;
    public bool IsSignedIn => _tokens.Get(Provider)?.HasAccess == true;

    public async Task SignInAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                $"{Provider.Display()}: не задан client_id. Заполните {CloudConfig.FileName} " +
                "(образец — cloud.config.sample.json).");

        var tokens = await _oauth.AuthorizeAsync(_browserFactory(App), ct);
        _tokens.Save(Provider, tokens);
    }

    public void SignOut() => _tokens.Clear(Provider);

    /// <summary>Действующий access_token: при необходимости обновляется по refresh_token.</summary>
    protected async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var tokens = _tokens.Get(Provider)
                     ?? throw new InvalidOperationException(
                         $"{Provider.Display()}: вход не выполнен. Нажмите «Подключить».");

        if (!tokens.IsExpired)
            return tokens.AccessToken;

        if (string.IsNullOrEmpty(tokens.RefreshToken))
            throw new InvalidOperationException(
                $"{Provider.Display()}: срок доступа истёк, войдите заново.");

        var refreshed = await _oauth.RefreshAsync(tokens.RefreshToken, ct);
        _tokens.Save(Provider, refreshed);
        return refreshed.AccessToken;
    }

    /// <summary>Схема заголовка Authorization: у Яндекса это «OAuth», у остальных «Bearer».</summary>
    protected virtual string AuthScheme => "Bearer";

    protected async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue(AuthScheme, await GetAccessTokenAsync(ct));
        return await Http.SendAsync(request, ct);
    }

    /// <summary>Бросить понятную ошибку, если ответ не успешный (404 обрабатывается вызывающим).</summary>
    protected static async Task EnsureOkAsync(HttpResponseMessage resp, string what, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        var hint = resp.StatusCode == HttpStatusCode.Unauthorized
            ? " Похоже, доступ отозван — подключитесь заново."
            : string.Empty;
        throw new InvalidOperationException($"{what}: {(int)resp.StatusCode} {resp.ReasonPhrase}.{hint} {body}");
    }

    public abstract Task<DateTime?> GetModifiedUtcAsync(CancellationToken ct = default);
    public abstract Task<string?> DownloadAsync(CancellationToken ct = default);
    public abstract Task UploadAsync(string json, CancellationToken ct = default);
}
