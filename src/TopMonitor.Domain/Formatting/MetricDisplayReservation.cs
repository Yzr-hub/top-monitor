using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Formatting;

public static class MetricDisplayReservation
{
    public static string Create(
        MetricDefinition definition,
        WidgetConfig widget,
        bool showLabel,
        bool showUnit)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(widget);

        var reservedValue = CreateReservedValue(definition, showUnit);
        var label = showLabel && !string.IsNullOrWhiteSpace(widget.Label)
            ? $"{widget.Label} "
            : string.Empty;
        return $"{label}{reservedValue}";
    }

    private static string CreateReservedValue(
        MetricDefinition definition,
        bool showUnit)
    {
        if (definition.Id == MetricIds.MemoryUsedBytes)
        {
            return showUnit ? "999.9GB" : "999.9";
        }

        if (definition.Id == MetricIds.ActiveNetworkDownload ||
            definition.Id == MetricIds.ActiveNetworkUpload)
        {
            return showUnit ? "999.9GB/s" : "999.9";
        }

        var value = definition.Id switch
        {
            var id when id == MetricIds.CpuTemperaturePackage => "125",
            var id when id == MetricIds.CpuTotalLoad => "100",
            var id when id == MetricIds.Gpu0CoreTemperature => "125",
            var id when id == MetricIds.Gpu0CoreLoad => "100",
            var id when id.Value == "graphics.foreground.fps" => "9999",
            var id when id == MetricIds.MemoryUsagePercent => "100",
            _ => "9999.9"
        };
        var unit = showUnit ? definition.Unit : string.Empty;
        return $"{value}{unit}";
    }
}
