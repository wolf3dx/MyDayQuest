using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyDayQuest.Models;

namespace MyDayQuest.Data;

/// <summary>
/// Синхронизация всех данных через один JSON-файл (кладётся в папку облачного
/// диска — OneDrive/Яндекс.Диск и т.п.). Save = экспорт (push), Load = импорт (pull),
/// Sync = сравнение времени: кто новее, тот побеждает.
/// </summary>
public class SyncService
{
    private readonly AppDatabase _db;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SyncService(AppDatabase db) => _db = db;

    public enum SyncResult { PushedToFile, PulledFromFile, UpToDate, NoFile }

    // ---- Сериализация ----

    /// <param name="exportedUtc">Штамп времени внутри файла. Для подсчёта отпечатка
    /// содержимого передают одно и то же значение, чтобы штамп на него не влиял.</param>
    public async Task<string> ExportJsonAsync(DateTime? exportedUtc = null)
    {
        var file = new SyncFile { ExportedUtc = exportedUtc ?? DateTime.UtcNow };
        var quests = await _db.GetQuestsAsync();
        var allTasks = await _db.GetAllTasksAsync();
        var allSubs = await _db.GetAllSubtasksAsync();

        foreach (var q in quests)
        {
            var qDto = new SyncQuest
            {
                Uid = q.Uid, Name = q.Name, SortOrder = q.SortOrder,
                CreatedUtc = q.CreatedUtc, UpdatedUtc = q.UpdatedUtc,
            };
            foreach (var t in allTasks.Where(x => x.QuestId == q.Id).OrderBy(x => x.SortOrder))
            {
                var tDto = new SyncTask
                {
                    Uid = t.Uid, Title = t.Title, StartDate = t.StartDate, Deadline = t.Deadline,
                    Progress = t.Progress, IsDone = t.IsDone, InDailyPlan = t.InDailyPlan,
                    SortOrder = t.SortOrder, UpdatedUtc = t.UpdatedUtc,
                };
                foreach (var s in allSubs.Where(x => x.TaskId == t.Id).OrderBy(x => x.SortOrder))
                {
                    tDto.Subtasks.Add(new SyncSubtask
                    {
                        Uid = s.Uid, Title = s.Title, IsDone = s.IsDone,
                        SortOrder = s.SortOrder, UpdatedUtc = s.UpdatedUtc,
                    });
                }
                qDto.Tasks.Add(tDto);
            }
            file.Quests.Add(qDto);
        }

        return JsonSerializer.Serialize(file, JsonOpts);
    }

    public async Task ImportReplaceAsync(string json)
    {
        var file = JsonSerializer.Deserialize<SyncFile>(json)
                   ?? throw new InvalidDataException("Файл синхронизации пуст или повреждён.");

        await _db.ClearAllAsync();
        foreach (var q in file.Quests)
        {
            var questId = await _db.InsertQuestRawAsync(new Quest
            {
                Uid = string.IsNullOrEmpty(q.Uid) ? Guid.NewGuid().ToString() : q.Uid,
                Name = q.Name, SortOrder = q.SortOrder,
                CreatedUtc = q.CreatedUtc == default ? DateTime.UtcNow : q.CreatedUtc,
                UpdatedUtc = q.UpdatedUtc == default ? DateTime.UtcNow : q.UpdatedUtc,
            });

            foreach (var t in q.Tasks)
            {
                var taskId = await _db.InsertTaskRawAsync(new QuestTask
                {
                    Uid = string.IsNullOrEmpty(t.Uid) ? Guid.NewGuid().ToString() : t.Uid,
                    QuestId = questId, QuestUid = q.Uid, Title = t.Title,
                    StartDate = t.StartDate, Deadline = t.Deadline, Progress = t.Progress,
                    IsDone = t.IsDone, InDailyPlan = t.InDailyPlan, SortOrder = t.SortOrder,
                    UpdatedUtc = t.UpdatedUtc == default ? DateTime.UtcNow : t.UpdatedUtc,
                });

                foreach (var s in t.Subtasks)
                {
                    await _db.InsertSubtaskRawAsync(new Subtask
                    {
                        Uid = string.IsNullOrEmpty(s.Uid) ? Guid.NewGuid().ToString() : s.Uid,
                        TaskId = taskId, TaskUid = t.Uid, Title = s.Title, IsDone = s.IsDone,
                        SortOrder = s.SortOrder,
                        UpdatedUtc = s.UpdatedUtc == default ? DateTime.UtcNow : s.UpdatedUtc,
                    });
                }
            }
        }
    }

    /// <summary>Отпечаток данных без учёта времени экспорта — «изменилось ли содержимое».</summary>
    public async Task<string> ContentHashAsync()
    {
        var json = await ExportJsonAsync(DateTime.MinValue);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Время экспорта, записанное внутри JSON-содержимого.</summary>
    public static DateTime? ReadExportedUtc(string json)
    {
        try { return JsonSerializer.Deserialize<SyncFile>(json)?.ExportedUtc; }
        catch { return null; }
    }

    // ---- Файл ----

    public async Task SaveToFileAsync(string path)
    {
        var json = await ExportJsonAsync();
        await File.WriteAllTextAsync(path, json, Encoding.UTF8);
    }

    public async Task LoadFromFileAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
        await ImportReplaceAsync(json);
    }

    /// <summary>Прочитать только время экспорта из файла (для сравнения при Sync).</summary>
    public async Task<DateTime?> ReadFileExportedUtcAsync(string path)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var file = JsonSerializer.Deserialize<SyncFile>(json);
            return file?.ExportedUtc;
        }
        catch { return null; }
    }

    // ---- Цель синхронизации (файл по пути или документ облачного диска) ----

    /// <summary>Отпечаток чужого содержимого без учёта времени экспорта.</summary>
    public static string ContentHashOf(string json)
    {
        var file = JsonSerializer.Deserialize<SyncFile>(json) ?? new SyncFile();
        file.ExportedUtc = DateTime.MinValue;
        var normalized = JsonSerializer.Serialize(file, JsonOpts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>
    /// Выгрузить данные в цель и убедиться, что они туда легли. Проверка нужна
    /// не для перестраховки: поставщик документов облачного диска может принять
    /// запись молча и никуда её не отправить — без чтения обратно это выглядит
    /// как успешная синхронизация, а изменения пропадают.
    /// </summary>
    public async Task PushAsync(ISyncTarget target)
    {
        var json = await ExportJsonAsync();
        await target.WriteAsync(json);

        var written = await target.ReadAsync();
        if (written is null || ContentHashOf(written) != ContentHashOf(json))
            throw new IOException(
                "Файл не сохранился: хранилище приняло запись, но содержимое не изменилось. "
                + "Так ведут себя некоторые облачные диски. Выберите файл в памяти телефона "
                + "или в папке другого диска.");
    }

    /// <summary>Забрать данные из цели (заменяет локальные).</summary>
    public async Task PullAsync(ISyncTarget target)
    {
        var json = await target.ReadAsync()
                   ?? throw new InvalidDataException("Не удалось прочитать файл синхронизации.");
        await ImportReplaceAsync(json);
    }

    /// <summary>
    /// Sync: кто новее (локальные данные или содержимое цели), тот и переносится.
    /// Пустая или нечитаемая цель считается отсутствующей — в неё просто выгружаем.
    /// </summary>
    public async Task<SyncResult> SyncAsync(ISyncTarget target)
    {
        if (!await target.ExistsAsync())
        {
            await PushAsync(target);
            return SyncResult.PushedToFile;
        }

        var json = await target.ReadAsync();
        var fileUtc = string.IsNullOrWhiteSpace(json) ? null : ReadExportedUtc(json);
        if (fileUtc is null)
        {
            await PushAsync(target);
            return SyncResult.PushedToFile;
        }

        // Сравнение идёт по содержимому, а не только по времени: после выгрузки
        // штамп в файле всегда свежее последней правки, и сравнение по времени
        // одно гоняло бы данные обратно при каждом следующем Sync.
        if (ContentHashOf(json!) == await ContentHashAsync())
            return SyncResult.UpToDate;

        var localUtc = await _db.GetLastChangeUtcAsync();
        if (localUtc > fileUtc)
        {
            await PushAsync(target);
            return SyncResult.PushedToFile;
        }

        await ImportReplaceAsync(json!);
        return SyncResult.PulledFromFile;
    }

    /// <summary>Sync: кто новее (локальные данные или файл), тот и переносится.</summary>
    public async Task<SyncResult> SyncAsync(string path)
    {
        if (!File.Exists(path))
        {
            await SaveToFileAsync(path);
            return SyncResult.PushedToFile;
        }

        var localUtc = await _db.GetLastChangeUtcAsync();
        var fileUtc = await ReadFileExportedUtcAsync(path) ?? File.GetLastWriteTimeUtc(path);

        if (localUtc > fileUtc)
        {
            await SaveToFileAsync(path);
            return SyncResult.PushedToFile;
        }
        if (fileUtc > localUtc)
        {
            await LoadFromFileAsync(path);
            return SyncResult.PulledFromFile;
        }
        return SyncResult.UpToDate;
    }
}
