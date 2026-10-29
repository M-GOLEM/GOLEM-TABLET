using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Golem.Tablet.Core.Devices;
using Golem.Tablet.Core.Drivers;
using Golem.Tablet.Core.Firmware;
using Golem.Tablet.Core.Logging;

namespace Golem.Tablet.Converters;

public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns the larger of the bound value and the converter parameter (used as a floor).</summary>
public sealed class MaxValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double bound = value is double number && !double.IsNaN(number) ? number : 0;
        double floor = double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : 0;
        return Math.Max(bound, floor);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DeviceFamilyToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DeviceFamily.Samsung => Themed("InfoBrush", Brushes.SteelBlue),
        DeviceFamily.Qualcomm => Themed("DangerBrush", Brushes.IndianRed),
        DeviceFamily.MediaTek => Brushes.SandyBrown,
        DeviceFamily.Spreadtrum => Brushes.MediumPurple,
        DeviceFamily.GenericAndroid => Themed("SuccessBrush", Brushes.MediumSeaGreen),
        _ => Themed("FaintBrush", Brushes.Gray)
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Themed(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;
}

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        LogLevel.Error => Themed("DangerBrush", Brushes.IndianRed),
        LogLevel.Warning => Themed("WarningBrush", Brushes.Goldenrod),
        LogLevel.Debug => Themed("MutedBrush", Brushes.Gray),
        _ => Themed("TextBrush", Brushes.WhiteSmoke)
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Themed(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;
}

public sealed class FirmwareSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        FirmwareSeverity.Error => Themed("DangerBrush", Brushes.IndianRed),
        FirmwareSeverity.Warning => Themed("WarningBrush", Brushes.Goldenrod),
        _ => Themed("MutedBrush", Brushes.Gray)
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Themed(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;
}

public sealed class DriverSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DriverSeverity.Error => Themed("DangerBrush", Brushes.IndianRed),
        DriverSeverity.Warning => Themed("WarningBrush", Brushes.Goldenrod),
        _ => Themed("SuccessBrush", Brushes.MediumSeaGreen)
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Themed(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;
}
