using Android.Provider;
using MyDayQuest.Data;
using AndroidUri = Android.Net.Uri;

namespace MyDayQuest.Platforms.Android;

/// <summary>
/// Цель синхронизации — документ, выбранный через системный проводник.
/// Файл может лежать в облачном диске: пути у него нет, обращение идёт по ссылке
/// content:// к поставщику документов, который сам качает и выгружает содержимое.
/// </summary>
public sealed class SafSyncTarget : ISyncTarget
{
    private readonly AndroidUri _uri;

    public SafSyncTarget(string uri) => _uri = AndroidUri.Parse(uri)
        ?? throw new ArgumentException("Неверная ссылка на документ.", nameof(uri));

    private static global::Android.Content.ContentResolver Resolver =>
        Platform.AppContext.ContentResolver
        ?? throw new InvalidOperationException("Нет доступа к хранилищу документов.");

    public string Describe
    {
        get
        {
            try
            {
                using var c = Resolver.Query(_uri, new[] { DocumentsContract.Document.ColumnDisplayName },
                    null, null, null);
                if (c is not null && c.MoveToFirst() && !c.IsNull(0))
                    return c.GetString(0) ?? _uri.ToString()!;
            }
            catch { /* имя не обязательно, покажем ссылку */ }
            return _uri.ToString()!;
        }
    }

    public Task<bool> ExistsAsync()
    {
        try
        {
            using var c = Resolver.Query(_uri, null, null, null, null);
            return Task.FromResult(c is not null && c.MoveToFirst());
        }
        catch { return Task.FromResult(false); }
    }

    public async Task<string?> ReadAsync()
    {
        try
        {
            using var stream = Resolver.OpenInputStream(_uri);
            if (stream is null) return null;
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch { return null; }
    }

    public async Task WriteAsync(string json)
    {
        // Режим "wt" обрезает прежнее содержимое. Без t поставщик может оставить
        // хвост от более длинного файла, и получится битый JSON.
        try
        {
            using var stream = Resolver.OpenOutputStream(_uri, "wt")
                ?? throw new IOException("Хранилище не отдало поток для записи.");
            using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);
            await writer.WriteAsync(json);
        }
        catch (Java.Lang.UnsupportedOperationException)
        {
            throw new IOException(
                "Это хранилище не разрешает запись в файл. Положите файл синхронизации "
                + "в память телефона или на другой диск.");
        }
        catch (Java.Lang.SecurityException)
        {
            throw new IOException(
                "Нет прав на запись в этот файл. Выберите его заново кнопкой «Load».");
        }
        catch (Java.Lang.IllegalArgumentException)
        {
            // Так выглядит потерянное разрешение: после переустановки приложения
            // Android отзывает доступ к выбранному документу.
            throw new IOException(
                "Доступ к файлу потерян — так бывает после переустановки приложения. "
                + "Откройте его заново кнопкой «Load».");
        }
    }
}
