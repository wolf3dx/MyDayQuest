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

    /// <summary>
    /// Когда отмечено выполненным. По нему видно, был ли за сутки хоть какой-то
    /// шаг: без этого «ничего не сделано за день» не отличить от «сделано давно».
    /// </summary>
    public DateTime? DoneUtc { get; set; }

    /// <summary>
    /// Срок: день, к которому подзадание должно быть выполнено. Необязателен.
    /// Миновал, а подзадание не отмечено — задание краснеет.
    /// </summary>
    public DateTime? DueDate { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Время последнего изменения (для слияния при синхронизации).</summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
