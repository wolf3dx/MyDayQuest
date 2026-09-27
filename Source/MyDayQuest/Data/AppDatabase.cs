using MyDayQuest.Models;
using SQLite;

namespace MyDayQuest.Data;

/// <summary>
/// Локальное хранилище на SQLite. Спроектировано «sync-ready»:
/// вся запись идёт через этот слой, поверх которого позже встанет синхронизация.
/// </summary>
public class AppDatabase
{
    /// <summary>Максимум задач в дневном плане — правило из описания.</summary>
    public const int DailyTaskLimit = 4;

    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_db is not null)
            return _db;

        var path = Path.Combine(FileSystem.AppDataDirectory, "mydayquest.db3");
        _db = new SQLiteAsyncConnection(path,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _db.CreateTableAsync<Quest>();
        await _db.CreateTableAsync<QuestTask>();
        await _db.CreateTableAsync<Subtask>();
        await BackfillAsync(_db);
        return _db;
    }

    /// <summary>
    /// Заполнить поля, добавленные вместе с синхронизацией, для строк, созданных ранее.
    /// Только через SQL — без материализации объектов, иначе чтение упадёт на NULL в
    /// не-nullable UpdatedUtc (DateTime хранится как ticks).
    /// </summary>
    private static async Task BackfillAsync(SQLiteAsyncConnection db)
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        foreach (var table in new[] { "quests", "quest_tasks", "subtasks" })
        {
            await db.ExecuteAsync($"UPDATE {table} SET UpdatedUtc = ? WHERE UpdatedUtc IS NULL", nowTicks);
            await db.ExecuteAsync($"UPDATE {table} SET Uid = lower(hex(randomblob(16))) WHERE Uid IS NULL OR Uid = ''");
        }
        await db.ExecuteAsync("UPDATE quest_tasks SET QuestUid = '' WHERE QuestUid IS NULL");
        await db.ExecuteAsync("UPDATE subtasks SET TaskUid = '' WHERE TaskUid IS NULL");
    }

    // ---- Quests (листы) ----

    public async Task<List<Quest>> GetQuestsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Quest>().OrderBy(q => q.SortOrder).ToListAsync();
    }

    public async Task<int> CountQuestsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Quest>().CountAsync();
    }

    public async Task<int> SaveQuestAsync(Quest quest)
    {
        var db = await GetConnectionAsync();
        quest.UpdatedUtc = DateTime.UtcNow;
        if (string.IsNullOrEmpty(quest.Uid)) quest.Uid = Guid.NewGuid().ToString();
        if (quest.Id == 0)
        {
            await db.InsertAsync(quest);
            return quest.Id;
        }
        await db.UpdateAsync(quest);
        return quest.Id;
    }

    public async Task DeleteQuestAsync(int questId)
    {
        var db = await GetConnectionAsync();
        await db.RunInTransactionAsync(t =>
        {
            // подзадания всех заданий этого листа
            t.Execute(
                "DELETE FROM subtasks WHERE TaskId IN (SELECT Id FROM quest_tasks WHERE QuestId = ?)",
                questId);
            t.Execute("DELETE FROM quest_tasks WHERE QuestId = ?", questId);
            t.Execute("DELETE FROM quests WHERE Id = ?", questId);
        });
    }

    // ---- Tasks (задачи) ----

    public async Task<List<QuestTask>> GetTasksAsync(int questId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<QuestTask>()
            .Where(t => t.QuestId == questId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
    }

    /// <summary>Текущая задача листа — первая невыполненная по порядку («задание на сегодня»).</summary>
    public async Task<QuestTask?> GetCurrentTaskAsync(int questId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<QuestTask>()
            .Where(t => t.QuestId == questId && !t.IsDone)
            .OrderBy(t => t.SortOrder)
            .FirstOrDefaultAsync();
    }

    public async Task SaveTaskAsync(QuestTask task)
    {
        var db = await GetConnectionAsync();
        task.UpdatedUtc = DateTime.UtcNow;
        if (string.IsNullOrEmpty(task.Uid)) task.Uid = Guid.NewGuid().ToString();
        if (task.Id == 0)
            await db.InsertAsync(task);
        else
            await db.UpdateAsync(task);
    }

    public async Task DeleteTaskAsync(int taskId)
    {
        var db = await GetConnectionAsync();
        await db.RunInTransactionAsync(t =>
        {
            t.Execute("DELETE FROM subtasks WHERE TaskId = ?", taskId);
            t.Execute("DELETE FROM quest_tasks WHERE Id = ?", taskId);
        });
    }

    public async Task<QuestTask?> GetTaskAsync(int taskId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<QuestTask>().Where(t => t.Id == taskId).FirstOrDefaultAsync();
    }

    /// <summary>Задания, добавленные в Главный Лист (дневной план).</summary>
    public async Task<List<QuestTask>> GetDailyPlanTasksAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<QuestTask>().Where(t => t.InDailyPlan)
            .OrderBy(t => t.SortOrder).ToListAsync();
    }

    public async Task<int> CountDailyPlanAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<QuestTask>().Where(t => t.InDailyPlan).CountAsync();
    }

    // ---- Subtasks (подзадания) ----

    public async Task<List<Subtask>> GetSubtasksAsync(int taskId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Subtask>()
            .Where(s => s.TaskId == taskId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task SaveSubtaskAsync(Subtask subtask)
    {
        var db = await GetConnectionAsync();
        subtask.UpdatedUtc = DateTime.UtcNow;
        if (string.IsNullOrEmpty(subtask.Uid)) subtask.Uid = Guid.NewGuid().ToString();
        if (subtask.Id == 0)
            await db.InsertAsync(subtask);
        else
            await db.UpdateAsync(subtask);
    }

    public async Task DeleteSubtaskAsync(int subtaskId)
    {
        var db = await GetConnectionAsync();
        await db.DeleteAsync<Subtask>(subtaskId);
    }

    /// <summary>Прогресс задания в % = выполнено подзаданий / всего. Без подзаданий — 0.</summary>
    public async Task<int> ComputeTaskProgressAsync(int taskId)
    {
        var subs = await GetSubtasksAsync(taskId);
        if (subs.Count == 0) return 0;
        var done = subs.Count(s => s.IsDone);
        return (int)Math.Round(done * 100.0 / subs.Count);
    }

    /// <summary>Пересчитать и сохранить прогресс задания по его подзаданиям.</summary>
    public async Task<int> RefreshTaskProgressAsync(int taskId)
    {
        var task = await GetTaskAsync(taskId);
        if (task is null) return 0;
        var progress = await ComputeTaskProgressAsync(taskId);
        task.Progress = progress;
        task.IsDone = progress == 100;
        await SaveTaskAsync(task);
        return progress;
    }

    // ---- Экспорт / импорт (синхронизация) ----

    public async Task<List<QuestTask>> GetAllTasksAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<QuestTask>().ToListAsync();
    }

    public async Task<List<Subtask>> GetAllSubtasksAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Subtask>().ToListAsync();
    }

    public async Task ClearAllAsync()
    {
        var db = await GetConnectionAsync();
        await db.RunInTransactionAsync(t =>
        {
            t.Execute("DELETE FROM subtasks");
            t.Execute("DELETE FROM quest_tasks");
            t.Execute("DELETE FROM quests");
        });
    }

    /// <summary>Вставка без перештамповки UpdatedUtc/Uid — для импорта из файла.</summary>
    public async Task<int> InsertQuestRawAsync(Quest q)
    { var db = await GetConnectionAsync(); q.Id = 0; await db.InsertAsync(q); return q.Id; }

    public async Task<int> InsertTaskRawAsync(QuestTask t)
    { var db = await GetConnectionAsync(); t.Id = 0; await db.InsertAsync(t); return t.Id; }

    public async Task InsertSubtaskRawAsync(Subtask s)
    { var db = await GetConnectionAsync(); s.Id = 0; await db.InsertAsync(s); }

    /// <summary>Самое свежее время изменения среди всех данных (для сравнения при синхронизации).</summary>
    public async Task<DateTime> GetLastChangeUtcAsync()
    {
        var quests = await GetQuestsAsync();
        var tasks = await GetAllTasksAsync();
        var subs = await GetAllSubtasksAsync();
        var times = quests.Select(x => x.UpdatedUtc)
            .Concat(tasks.Select(x => x.UpdatedUtc))
            .Concat(subs.Select(x => x.UpdatedUtc));
        return times.DefaultIfEmpty(DateTime.MinValue).Max();
    }

    public async Task ReplaceTasksAsync(int questId, IEnumerable<QuestTask> tasks)
    {
        var db = await GetConnectionAsync();
        await db.RunInTransactionAsync(t =>
        {
            t.Execute("DELETE FROM quest_tasks WHERE QuestId = ?", questId);
            var order = 0;
            foreach (var task in tasks)
            {
                task.QuestId = questId;
                task.SortOrder = order++;
                t.Insert(task);
            }
        });
    }
}
