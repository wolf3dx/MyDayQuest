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

    /// <summary>
    /// День, на который задание поставлено в план (местная дата, время нулевое).
    /// Нужен, чтобы отличить «на сегодня» от «висит с прошлых дней»: по одному
    /// флагу InDailyPlan этого не узнать. Проставляется при постановке в план.
    /// </summary>
    public DateTime? PlannedForDate { get; set; }

    /// <summary>
    /// Повторяется каждый день — чек-бокс внутри задания. Подзадания при этом
    /// не сбрасываются; повтор означает, что о задании напоминают ежедневно,
    /// пока оно не выполнено целиком.
    /// </summary>
    public bool IsRepeating { get; set; }

    // ---- Поля ниже заполняет слой данных, в таблице их нет ----

    /// <summary>Есть ли у задания подзадания. Без них просрочки не бывает.</summary>
    [Ignore]
    public bool HasSubtasks { get; set; }

    /// <summary>День последнего выполненного подзадания, если такой был.</summary>
    [Ignore]
    public DateTime? LastProgressDate { get; set; }

    /// <summary>Самый ранний пропущенный срок подзадания, если такой есть.</summary>
    [Ignore]
    public DateTime? MissedDueDate { get; set; }

    /// <summary>Есть ли подзадание с миновавшим сроком, которое не выполнено.</summary>
    [Ignore]
    public bool HasMissedSubtaskDue => MissedDueDate is not null;

    /// <summary>
    /// Задание просрочено и подсвечивается красным.
    ///
    /// Два повода. Первый: у подзадания стоял срок, он миновал, а подзадание не
    /// отмечено. Второй: сутки прошли впустую — последний шаг был раньше вчера,
    /// а если шагов не было вовсе, то уже прошёл день постановки в план.
    /// Вчерашний день не в счёт: сегодняшние сутки ещё не кончились.
    ///
    /// Задание без подзаданий просрочкой не считается — отмечать в нём нечего.
    /// </summary>
    [Ignore]
    public bool IsOverdue
    {
        get
        {
            if (IsDone || !HasSubtasks) return false;
            if (HasMissedSubtaskDue) return true;
            if (!InDailyPlan) return false;

            var today = DateTime.Now.Date;
            return LastProgressDate is { } last
                ? last.Date < today.AddDays(-1)
                : PlannedForDate is { } planned && planned.Date < today;
        }
    }

    /// <summary>
    /// С какого дня задание висит — для подписи на плашке. Пропущенный срок
    /// важнее остального: он и есть названная пользователем дата.
    /// </summary>
    [Ignore]
    public DateTime? OverdueSince => MissedDueDate ?? LastProgressDate ?? PlannedForDate;

    /// <summary>Время последнего изменения (для слияния при синхронизации).</summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
