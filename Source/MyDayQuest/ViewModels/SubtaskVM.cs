using CommunityToolkit.Mvvm.ComponentModel;

namespace MyDayQuest.ViewModels;

/// <summary>Подзадание (строка) на экране деталей задания.</summary>
public partial class SubtaskVM : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isDone;

    /// <summary>Выделено для удаления кнопкой «–».</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Сюда сейчас метят перетаскиваемым подзаданием.</summary>
    [ObservableProperty]
    private bool _isDropTarget;

    /// <summary>
    /// Срок задан. Отдельный признак нужен потому, что DatePicker не умеет
    /// пустую дату: он всегда показывает какой-то день, и без галочки «к дате»
    /// любое подзадание выглядело бы срочным.
    /// </summary>
    [ObservableProperty]
    private bool _hasDue;

    /// <summary>День, к которому подзадание должно быть выполнено.</summary>
    [ObservableProperty]
    private DateTime _dueDate = DateTime.Now.Date;

    /// <summary>Срок миновал, а подзадание не отмечено.</summary>
    public bool IsMissed => HasDue && !IsDone && DueDate.Date < DateTime.Now.Date;

    partial void OnHasDueChanged(bool value) => OnPropertyChanged(nameof(IsMissed));
    partial void OnDueDateChanged(DateTime value) => OnPropertyChanged(nameof(IsMissed));
    partial void OnIsDoneChanged(bool value) => OnPropertyChanged(nameof(IsMissed));
}
