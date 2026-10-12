using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MyDayQuest.Avalonia.ViewModels;

/// <summary>Конвертеры для привязок AXAML.</summary>
public static class Conv
{
    /// <summary>true → акцентная рамка, false → серая.</summary>
    public static readonly IValueConverter SelBrush = new SelBrushConverter();

    /// <summary>int == 0 → true (показать «пусто»).</summary>
    public static readonly IValueConverter IsZero = new IsZeroConverter();

    /// <summary>Закладка, на место которой метят переносом, → зелёная рамка.</summary>
    public static readonly IValueConverter DropBrush = new DropBrushConverter();

    /// <summary>0 — обычная рамка, 1 — выделение, 2 — цель переноса.</summary>
    public static readonly IValueConverter AccentBrush = new AccentBrushConverter();

    private static readonly IBrush Drop = new SolidColorBrush(Color.Parse("#5FB13A"));

    private sealed class DropBrushConverter : IValueConverter
    {
        private static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#C9AE7B"));
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? Drop : Normal;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class AccentBrushConverter : IValueConverter
    {
        private static readonly IBrush Selected = new SolidColorBrush(Color.Parse("#2E6FD6"));
        private static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#50000000"));
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value switch { 2 => Drop, 1 => Selected, _ => Normal };
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class IsZeroConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is int i && i == 0;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class SelBrushConverter : IValueConverter
    {
        private static readonly IBrush Selected = new SolidColorBrush(Color.Parse("#2E6FD6"));
        private static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#50000000"));
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? Selected : Normal;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
