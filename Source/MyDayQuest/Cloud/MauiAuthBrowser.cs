using MyDayQuest.Cloud;

namespace MyDayQuest.CloudAuth;

/// <summary>
/// Показ страницы согласия облака средствами MAUI: на Android/iOS — системный
/// WebAuthenticator (возврат по схеме mydayquest://auth), на Windows/macOS —
/// обычный браузер и локальный слушатель 127.0.0.1.
/// </summary>
public class MauiAuthBrowser : IAuthBrowser
{
    private readonly IAuthBrowser _inner;

    public MauiAuthBrowser(CloudAppConfig app)
    {
#if ANDROID || IOS
        _inner = new WebAuthenticatorBrowser(
            string.IsNullOrWhiteSpace(app.MobileRedirectUri) ? "mydayquest://auth" : app.MobileRedirectUri);
#else
        _inner = new LoopbackAuthBrowser(app.DesktopRedirectUri,
            url => _ = Launcher.Default.OpenAsync(url));
#endif
    }

    public string RedirectUri => _inner.RedirectUri;

    public Task<IReadOnlyDictionary<string, string>> AuthorizeAsync(string authUrl, CancellationToken ct = default) =>
        _inner.AuthorizeAsync(authUrl, ct);
}

/// <summary>Мобильный вариант: системное окно входа, возврат по своей URL-схеме.</summary>
public class WebAuthenticatorBrowser : IAuthBrowser
{
    public WebAuthenticatorBrowser(string redirectUri) => RedirectUri = redirectUri;

    public string RedirectUri { get; }

    public async Task<IReadOnlyDictionary<string, string>> AuthorizeAsync(
        string authUrl, CancellationToken ct = default)
    {
        var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri(authUrl),
            CallbackUrl = new Uri(RedirectUri),
        });

        return new Dictionary<string, string>(result.Properties, StringComparer.OrdinalIgnoreCase);
    }
}
