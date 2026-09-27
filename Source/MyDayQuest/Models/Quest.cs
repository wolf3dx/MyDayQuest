using SQLite;

namespace MyDayQuest.Models;

/// <summary>
/// Лист (Quest) — конечный список задач под одну цель.
/// Пример: «Карнеги Quest: читать 20 страниц в день».
/// </summary>
[Table("quests")]
public class Quest
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Глобальный идентификатор для синхронизации между устройствами.</summary>
    [Indexed]
    public string Uid { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Название листа (отображается на плашке).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Порядок отображения на главном экране.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Время последнего изменения (для слияния при синхронизации).</summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
