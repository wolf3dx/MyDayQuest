using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MyDayQuest.Cloud;

/// <summary>Адреса OAuth-сервера провайдера.</summary>
public record OAuthEndpoints(string AuthorizeUrl, string TokenUrl, string Scope, string ExtraAuthParams = "");

/// <summary>
/// Общий для всех провайдеров поток OAuth 2.0 «authorization code + PKCE»:
/// приложение публичное, client_secret не требуется (кроме Google desktop-клиента).
/// </summary>
public class PkceOAuthClient
{
    private readonly HttpClient _http;
    private readonly OAuthEndpoints _endpoints;
    private readonly CloudAppConfig _app;

    public PkceOAuthClient(HttpClient http, OAuthEndpoints endpoints, CloudAppConfig app)
    {
        _http = http;
        _endpoints = endpoints;
        _app = app;
    }

    public async Task<CloudTokens> AuthorizeAsync(IAuthBrowser browser, CancellationToken ct = default)
    {
        var verifier = RandomUrlSafe(64);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = RandomUrlSafe(16);
        var redirect = browser.RedirectUri;

        var authUrl = $"{_endpoints.AuthorizeUrl}?response_type=code" +
                      $"&client_id={Uri.EscapeDataString(_app.ClientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirect)}" +
                      $"&scope={Uri.EscapeDataString(_endpoints.Scope)}" +
                      $"&state={state}" +
                      $"&code_challenge={challenge}&code_challenge_method=S256" +
                      _endpoints.ExtraAuthParams;

        var response = await browser.AuthorizeAsync(authUrl, ct);

        if (response.TryGetValue("error", out var error))
            throw new InvalidOperationException($"Провайдер отказал во входе: {error}. " +
                (response.TryGetValue("error_description", out var d) ? d : string.Empty));

        if (!response.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            throw new InvalidOperationException("Провайдер не вернул код авторизации.");

        if (response.TryGetValue("state", out var got) && got != state)
            throw new InvalidOperationException("Не совпал параметр state — ответ отброшен.");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = _app.ClientId,
            ["redirect_uri"] = redirect,
            ["code_verifier"] = verifier,
        };
        if (!string.IsNullOrEmpty(_app.ClientSecret))
            form["client_secret"] = _app.ClientSecret;

        return await PostTokenAsync(form, previousRefresh: null, ct);
    }

    public async Task<CloudTokens> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _app.ClientId,
        };
        if (!string.IsNullOrEmpty(_app.ClientSecret))
            form["client_secret"] = _app.ClientSecret;

        return await PostTokenAsync(form, refreshToken, ct);
    }

    private async Task<CloudTokens> PostTokenAsync(
        Dictionary<string, string> form, string? previousRefresh, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoints.TokenUrl)
        {
            Content = new FormUrlEncodedContent(form),
        };
        using var resp = await _http.SendAsync(request, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Обмен кода на токен не удался ({(int)resp.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var expires = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;

        return new CloudTokens
        {
            AccessToken = root.GetProperty("access_token").GetString() ?? string.Empty,
            RefreshToken = root.TryGetProperty("refresh_token", out var r)
                ? r.GetString() ?? previousRefresh ?? string.Empty
                : previousRefresh ?? string.Empty,
            ExpiresUtc = DateTime.UtcNow.AddSeconds(expires),
        };
    }

    private static string RandomUrlSafe(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
