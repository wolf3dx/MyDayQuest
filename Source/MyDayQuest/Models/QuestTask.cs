using SQLite;

namespace MyDayQuest.Models;

/// <summary>
/// Задача внутри листа. На главный экран из каждого листа попадает
/// первая невыполненная задача — «задание на сегодня».
/// По описанию у задачи есть свои даты (начало/дедлайн) и прогресс в %.
/// </summary>
[Table("quest_tasks")]
public class QuestTask
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Глобальный идентификатор для синхронизации.</summary>
    [Indexed]
    public string Uid { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public int QuestId { get; set; }

    /// <summary>Uid листа-владельца (для восстановления связей при импорте).</summary>
    public string QuestUid { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Порядок задачи внутри листа (следующая = минимальный SortOrder среди невыполненных).</summary>
    public int SortOrder { get; set; }

    public bool IsDone { get; set; }

    public DateTime? DoneUtc { get; set; }

    /// <summary>Начало задачи (Start line).</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Дедлайн задачи (Dead line).</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Прогресс задачи 0..100 % (по подзаданиям).</summary>
    public int Progress { get; set; }

    /// <summary>Задание добавлено в Главный Лист (единый дневной план) — чек-бокс у Name.</summary>
    public bool InDailyPlan { get; set; }

    /// <summary>Время последнего изменения (для слияния при синхронизации).</summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
