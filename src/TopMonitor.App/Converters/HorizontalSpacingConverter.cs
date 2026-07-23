using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TopMonitor.App.Converters;

public sealed class HorizontalSpacingConverter : IValueConverter
{
    public static HorizontalSpacingConverter Instance { get; } = new();

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var spacing = value is double number ? number / 2 : 0;
        return new Thickness(spacing, 0, spacing, 0);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}
