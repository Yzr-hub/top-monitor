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
        var formattedValue =
            value.Status == MetricStatus.Available && value.Value is { } number
                ? FormatAvailableValue(
                    definition,
                    widget,
                    number,
                    showUnit,
                    provider)
                : "--";
        var label = showLabel && !string.IsNullOrWhiteSpace(widget.Label)
            ? $"{widget.Label} "
            : string.Empty;

        return $"{label}{formattedValue}";
    }

    private static string FormatAvailableValue(
        MetricDefinition definition,
        WidgetConfig widget,
        double number,
        bool showUnit,
        IFormatProvider provider)
    {
        if (definition.Id == MetricIds.MemoryUsedBytes)
        {
            return FormatBytes(
                number,
                widget.NumberFormat,
                showUnit,
                perSecond: false,
                provider);
        }

        if (definition.Id == MetricIds.ActiveNetworkDownload ||
            definition.Id == MetricIds.ActiveNetworkUpload)
        {
            return FormatBytes(
                number,
                widget.NumberFormat,
                showUnit,
                perSecond: true,
                provider);
        }

        var unit = showUnit ? definition.Unit : string.Empty;
        return $"{number.ToString(widget.NumberFormat, provider)}{unit}";
    }

    private static string FormatBytes(
        double bytes,
        string numberFormat,
        bool showUnit,
        bool perSecond,
        IFormatProvider provider)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unitIndex = 0;
        var compactValue = bytes;
        while (Math.Abs(compactValue) >= 1024 && unitIndex < units.Length - 1)
        {
            compactValue /= 1024;
            unitIndex++;
        }

        var unit = showUnit
            ? $"{units[unitIndex]}{(perSecond ? "/s" : string.Empty)}"
            : string.Empty;
        return $"{compactValue.ToString(numberFormat, provider)}{unit}";
    }
}
