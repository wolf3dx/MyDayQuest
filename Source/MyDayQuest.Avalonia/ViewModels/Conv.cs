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

    private sealed class IsZeroConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is int i && i == 0;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class SelBrushConverter : IValueConverter
    {
        private static readonly IBrush Selected = new SolidColorBrush(Color.Parse("#512BD4"));
        private static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#50000000"));
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? Selected : Normal;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
