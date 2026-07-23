using System.Globalization;
using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Formatting;

/// <summary>
/// 将领域采样值转换为悬浮窗可展示文本，不依赖 WPF。
/// </summary>
public static class MetricFormatter
{
    public static string Format(
        MetricDefinition definition,
        WidgetConfig widget,
        MetricValue value,
        bool showLabel,
        bool showUnit,
        IFormatProvider? formatProvider = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(value);

        var provider = formatProvider ?? CultureInfo.CurrentCulture;
        var formattedValue = value.Status == MetricStatus.Available && value.Value is { } number
            ? number.ToString(widget.NumberFormat, provider)
            : "--";
        var label = showLabel && !string.IsNullOrWhiteSpace(widget.Label)
            ? $"{widget.Label} "
            : string.Empty;
        var unit = showUnit && value.Status == MetricStatus.Available
            ? definition.Unit
            : string.Empty;

        return $"{label}{formattedValue}{unit}";
    }
}
