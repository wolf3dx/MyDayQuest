using System.Net;
using System.Text;

namespace MyDayQuest.Cloud;

/// <summary>
/// Показ страницы согласия провайдера и приём ответа. Реализация зависит от платформы:
/// на настольных ОС — системный браузер + локальный слушатель, на Android/iOS —
/// WebAuthenticator из MAUI.
/// </summary>
public interface IAuthBrowser
{
    /// <summary>Redirect URI, который умеет принять эта реализация.</summary>
    string RedirectUri { get; }

    /// <summary>Открыть <paramref name="authUrl"/> и вернуть query-параметры ответа.</summary>
    Task<IReadOnlyDictionary<string, string>> AuthorizeAsync(string authUrl, CancellationToken ct = default);
}

/// <summary>
/// Настольный вариант: поднимает HttpListener на 127.0.0.1 и ловит редирект с кодом.
/// Браузер открывает платформенная голова (передаётся делегатом).
/// </summary>
public class LoopbackAuthBrowser : IAuthBrowser
{
    private readonly Action<string> _openBrowser;

    public LoopbackAuthBrowser(string redirectUri, Action<string> openBrowser)
    {
        RedirectUri = redirectUri.EndsWith('/') ? redirectUri : redirectUri + "/";
        _openBrowser = openBrowser;
    }

    public string RedirectUri { get; }

    public async Task<IReadOnlyDictionary<string, string>> AuthorizeAsync(string authUrl, CancellationToken ct = default)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        try { listener.Start(); }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException(
                $"Не удалось занять адрес {RedirectUri} для приёма ответа провайдера. {ex.Message}", ex);
        }

        _openBrowser(authUrl);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        var contextTask = listener.GetContextAsync();
        var finished = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, timeout.Token));
        if (finished != contextTask)
            throw new TimeoutException("Вход в облако не завершён: ответ от провайдера не получен.");

        var context = await contextTask;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in context.Request.QueryString.AllKeys)
            if (key is not null)
                result[key] = context.Request.QueryString[key] ?? string.Empty;

        var ok = result.ContainsKey("code");
        var html = ok
            ? "<h2>MyDayQuest подключён</h2><p>Можно закрыть эту вкладку и вернуться в приложение.</p>"
            : "<h2>Вход не выполнен</h2><p>Вернитесь в приложение и попробуйте ещё раз.</p>";
        var bytes = Encoding.UTF8.GetBytes($"<html><head><meta charset=\"utf-8\"></head><body>{html}</body></html>");
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, ct);
        context.Response.Close();

        return result;
    }
}
