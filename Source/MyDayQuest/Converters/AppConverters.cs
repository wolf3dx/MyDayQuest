using System.Globalization;

namespace MyDayQuest.Converters;

/// <summary>true → ☑, false → ☐ (кнопка-чек на плашке).</summary>
public class CheckGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "☑" : "☐";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Прогресс 0..100 % → доля 0..1 для ProgressBar.</summary>
public class PercentToFractionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value is int i ? i : 0;
        return Math.Clamp(percent, 0, 100) / 100.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
