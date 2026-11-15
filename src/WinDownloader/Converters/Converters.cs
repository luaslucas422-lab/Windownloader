using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WinDownloader.Models;

namespace WinDownloader.Converters;

/// <summary>Visible when the bound string equals the parameter (used for page switching).</summary>
public sealed class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class StringEqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
    public object? ConvertBack(object? value, Type t, object? parameter, CultureInfo c) =>
        value is true ? parameter?.ToString() : Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value is not true;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => value is not true;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Visible when a number is greater than zero (or, with parameter "zero", when it is zero).</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c)
    {
        int n = value is int i ? i : value is double d ? (int)d : 0;
        bool wantZero = string.Equals(parameter?.ToString(), "zero", StringComparison.OrdinalIgnoreCase);
        return (wantZero ? n == 0 : n > 0) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        UpdateSeverity.Critical => Palette.Red,
        UpdateSeverity.Important => Palette.Amber,
        _ => Palette.Blue
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        UpdateStatus.Installed => Palette.Green,
        UpdateStatus.Failed => Palette.Red,
        UpdateStatus.Downloading or UpdateStatus.Installing => Palette.Cyan,
        UpdateStatus.Skipped => Palette.Muted,
        _ => Palette.Muted
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>"ok" / "update" / "problem" / "old" → brush.</summary>
public sealed class StatusKindToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value?.ToString() switch
    {
        "problem" => Palette.Red,
        "update" => Palette.Blue,
        "old" => Palette.Amber,
        _ => Palette.Green
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class ScoreToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        int s = value is int i ? i : value is double d ? (int)d : -1;
        return Palette.ForScore(s);
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public static class Palette
{
    public static readonly SolidColorBrush Red = Freeze("#EF4444");
    public static readonly SolidColorBrush Amber = Freeze("#F59E0B");
    public static readonly SolidColorBrush Green = Freeze("#22C55E");
    public static readonly SolidColorBrush Blue = Freeze("#3B82F6");
    public static readonly SolidColorBrush Cyan = Freeze("#38BDF8");
    public static readonly SolidColorBrush Muted = Freeze("#94A3B8");

    public static SolidColorBrush ForScore(int s) => s < 0 ? Muted : s >= 100 ? Green : s >= 85 ? Cyan : s >= 60 ? Amber : Red;

    private static SolidColorBrush Freeze(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
