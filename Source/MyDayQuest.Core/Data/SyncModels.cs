namespace MyDayQuest.Data;

/// <summary>Единый файл синхронизации: все листы → задания → подзадания.</summary>
public class SyncFile
{
    public int Version { get; set; } = 1;
    public DateTime ExportedUtc { get; set; }
    public List<SyncQuest> Quests { get; set; } = new();
}

public class SyncQuest
{
    public string Uid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<SyncTask> Tasks { get; set; } = new();
}

public class SyncTask
{
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public int Progress { get; set; }
    public bool IsDone { get; set; }
    public bool InDailyPlan { get; set; }
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<SyncSubtask> Subtasks { get; set; } = new();
}

public class SyncSubtask
{
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
