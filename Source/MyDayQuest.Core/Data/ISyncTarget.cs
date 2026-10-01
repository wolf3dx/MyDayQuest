namespace MyDayQuest.Data;

/// <summary>
/// Куда синхронизируемся. На десктопе это обычный файл по пути, на Android —
/// документ облачного диска, выбранный через системный проводник: у него нет
/// пути, работать с ним можно только потоками через его ссылку.
/// </summary>
public interface ISyncTarget
{
    /// <summary>Что показать пользователю — путь или имя документа.</summary>
    string Describe { get; }

    Task<bool> ExistsAsync();

    /// <summary>Содержимое или null, если прочитать не удалось.</summary>
    Task<string?> ReadAsync();

    Task WriteAsync(string json);
}

/// <summary>Цель синхронизации — файл по пути. Десктоп и папка облачного клиента.</summary>
public sealed class FileSyncTarget(string path) : ISyncTarget
{
    public string Path { get; } = path;

    public string Describe => Path;

    public Task<bool> ExistsAsync() => Task.FromResult(File.Exists(Path));

    public async Task<string?> ReadAsync()
    {
        try { return await File.ReadAllTextAsync(Path, System.Text.Encoding.UTF8); }
        catch { return null; }
    }

    public Task WriteAsync(string json) =>
        File.WriteAllTextAsync(Path, json, System.Text.Encoding.UTF8);
}
