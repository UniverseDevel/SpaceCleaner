using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SpaceCleaner.Models;

namespace SpaceCleaner.Infrastructure;

/// <summary>Maps a <see cref="SafetyLevel"/> to its indicator colour (green / amber / red).</summary>
public sealed class SafetyLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Safe = Freeze(0x2E, 0x9E, 0x5B);
    private static readonly SolidColorBrush Caution = Freeze(0xE0, 0x92, 0x2F);
    private static readonly SolidColorBrush Review = Freeze(0xD6, 0x45, 0x3D);
    private static readonly SolidColorBrush Neutral = Freeze(0x9A, 0xA3, 0xAD);

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            SafetyLevel.Safe => Safe,
            SafetyLevel.Caution => Caution,
            SafetyLevel.Review => Review,
            _ => Neutral,
        };

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts a tree depth (int) to a left-indent <see cref="Thickness"/>. Indenting by margin
/// (rather than nested offset columns) keeps every row the same width, so trailing columns line up.</summary>
public sealed class DepthToIndentConverter : IValueConverter
{
    public double IndentPerLevel { get; set; } = 16;

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        int depth = value is int d ? d : 0;
        return new Thickness(depth * IndentPerLevel, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a <see cref="SafetyLevel"/> to a pale background tint for the risk callout box.</summary>
public sealed class SafetyLevelToTintConverter : IValueConverter
{
    private static readonly SolidColorBrush Safe = Freeze(0xEA, 0xF7, 0xEF);
    private static readonly SolidColorBrush Caution = Freeze(0xFF, 0xF6, 0xE5);
    private static readonly SolidColorBrush Review = Freeze(0xFC, 0xEB, 0xEA);

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            SafetyLevel.Safe => Safe,
            SafetyLevel.Caution => Caution,
            SafetyLevel.Review => Review,
            _ => Safe,
        };

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts a byte count (<see cref="long"/>) to a human-readable size string.</summary>
public sealed class ByteSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is long bytes ? ByteSize.Format(bytes) : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Collapses an element when its bound string is null or empty.</summary>
public sealed class NullOrEmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Collapses an element when its bound value is null.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows an element only when its bound value is null (inverse of the above).</summary>
public sealed class NullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows an element only when its bound count is zero (used for the empty-results hint).</summary>
public sealed class ZeroCountToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
