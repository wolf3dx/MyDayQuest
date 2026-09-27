using SQLite;

namespace MyDayQuest.Models;

/// <summary>
/// Подзадание — простая строка внутри задания. Прогресс задания
/// считается как доля выполненных подзаданий.
/// </summary>
[Table("subtasks")]
public class Subtask
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Глобальный идентификатор для синхронизации.</summary>
    [Indexed]
    public string Uid { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public int TaskId { get; set; }

    /// <summary>Uid задания-владельца (для восстановления связей при импорте).</summary>
    public string TaskUid { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public bool IsDone { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Время последнего изменения (для слияния при синхронизации).</summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
