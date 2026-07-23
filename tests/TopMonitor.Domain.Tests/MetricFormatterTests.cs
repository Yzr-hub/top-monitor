using System.Globalization;
using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Formatting;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Tests;

public sealed class MetricFormatterTests
{
    [Fact]
    public void Available_value_is_formatted_with_label_and_unit()
    {
        var definition = new MetricDefinition(
            MetricIds.CpuTemperaturePackage,
            "CPU 温度",
            MetricCategory.Temperature,
            "°C",
            TimeSpan.FromSeconds(1),
            false);
        var widget = new WidgetConfig(
            MetricIds.CpuTemperaturePackage,
            true,
            10,
            "CPU",
            "0");
        var value = MetricValue.Create(
            MetricIds.CpuTemperaturePackage,
            52.4,
            DateTimeOffset.UtcNow);

        var text = MetricFormatter.Format(
            definition,
            widget,
            value,
            showLabel: true,
            showUnit: true,
            CultureInfo.InvariantCulture);

        Assert.Equal("CPU 52°C", text);
    }

    [Fact]
    public void Unavailable_value_is_formatted_as_placeholder()
    {
        var definition = new MetricDefinition(
            MetricIds.CpuTemperaturePackage,
            "CPU 温度",
            MetricCategory.Temperature,
            "°C",
            TimeSpan.FromSeconds(1),
            false);
        var widget = new WidgetConfig(
            MetricIds.CpuTemperaturePackage,
            true,
            10,
            "CPU",
            "0");
        var value = MetricValue.Unavailable(
            MetricIds.CpuTemperaturePackage,
            DateTimeOffset.UtcNow,
            "传感器不可用");

        var text = MetricFormatter.Format(
            definition,
            widget,
            value,
            showLabel: true,
            showUnit: true,
            CultureInfo.InvariantCulture);

        Assert.Equal("CPU --", text);
    }
}
