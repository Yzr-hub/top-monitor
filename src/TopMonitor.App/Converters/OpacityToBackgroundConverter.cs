using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TopMonitor.App.Converters;

public sealed class OpacityToBackgroundConverter : IValueConverter
{
    public static OpacityToBackgroundConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var opacity = value is double number ? Math.Clamp(number, 0, 1) : 0.75;
        return new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(opacity * byte.MaxValue),
            25,
            30,
            39));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
