using System.Net.Http;
using System.Text.Json;

namespace MyDayQuest.Data;

/// <summary>Информация о доступном обновлении.</summary>
public record UpdateInfo(string Tag, string Version, string ReleaseUrl);

/// <summary>
/// Проверка новой версии через публичный GitLab Releases API и построение
/// прямых ссылок на ассеты в папке Release/ по тегу. Применение обновления —
/// на стороне платформенной головы (у неё свой способ установки).
/// </summary>
public class UpdateService
{
    // Основной репозиторий (GitLab, публичный — токен не нужен).
    public const string Owner = "vkandreevich";
    public const string Repo = "MyDayQuest";
    private const string ReleasesApi =
        "https://gitlab.com/api/v4/projects/vkandreevich%2FMyDayQuest/releases?per_page=1";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Есть ли релиз новее текущей версии. null — обновления нет/недоступно.</summary>
    public async Task<UpdateInfo?> CheckAsync(string currentVersion)
    {
        try
        {
            var json = await Http.GetStringAsync(ReleasesApi);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var rel = doc.RootElement[0];
            var tag = rel.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var url = rel.TryGetProperty("_links", out var l) && l.TryGetProperty("self", out var s)
                ? s.GetString() ?? WebUrl : WebUrl;
            if (string.IsNullOrEmpty(tag)) return null;

            if (TryParse(tag, out var latest) && TryParse(currentVersion, out var cur) && latest > cur)
                return new UpdateInfo(tag, latest.ToString(), url);

            return null;
        }
        catch
        {
            return null; // офлайн/ошибка — молча без иконки
        }
    }

    public static string WebUrl => $"https://gitlab.com/{Owner}/{Repo}";

    /// <summary>Прямая ссылка на ассет в Release/ по тегу.</summary>
    public static string RawAssetUrl(string tag, string fileName)
        => $"https://gitlab.com/{Owner}/{Repo}/-/raw/{tag}/Release/{fileName}";

    /// <summary>Скачать файл по URL в указанный путь.</summary>
    public static async Task DownloadAsync(string url, string destPath)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        await using var fs = File.Create(destPath);
        await resp.Content.CopyToAsync(fs);
    }

    /// <summary>Разбор версии из строки вида "v0.1.1"/"0.1.1".</summary>
    private static bool TryParse(string? s, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().TrimStart('v', 'V');
        // оставить только числа и точки
        var clean = new string(s.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        var norm = string.Join('.', parts.Length switch
        {
            1 => new[] { parts[0], "0" },
            _ => parts,
        });
        return Version.TryParse(norm, out version!);
    }
}
