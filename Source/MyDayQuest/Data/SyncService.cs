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

    public async Task<string> ExportJsonAsync()
    {
        var file = new SyncFile { ExportedUtc = DateTime.UtcNow };
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
