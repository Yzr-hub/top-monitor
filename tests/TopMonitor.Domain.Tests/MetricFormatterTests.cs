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

    [Fact]
    public void Memory_bytes_use_a_compact_binary_unit()
    {
        var definition = new MetricDefinition(
            MetricIds.MemoryUsedBytes,
            "内存已使用",
            MetricCategory.Memory,
            "B",
            TimeSpan.FromSeconds(1),
            false);
        var widget = new WidgetConfig(
            MetricIds.MemoryUsedBytes,
            true,
            10,
            "RAM",
            "0.0");
        var value = MetricValue.Create(
            MetricIds.MemoryUsedBytes,
            15_308_623_872,
            DateTimeOffset.UtcNow);

        var text = MetricFormatter.Format(
            definition,
            widget,
            value,
            showLabel: true,
            showUnit: true,
            CultureInfo.InvariantCulture);

        Assert.Equal("RAM 14.3GB", text);
    }

    [Fact]
    public void Network_bytes_per_second_use_a_compact_binary_unit()
    {
        var definition = new MetricDefinition(
            MetricIds.ActiveNetworkDownload,
            "下载速度",
            MetricCategory.Network,
            "B/s",
            TimeSpan.FromSeconds(1),
            false);
        var widget = new WidgetConfig(
            MetricIds.ActiveNetworkDownload,
            true,
            10,
            "↓",
            "0.0");
        var value = MetricValue.Create(
            MetricIds.ActiveNetworkDownload,
            12_582_912,
            DateTimeOffset.UtcNow);

        var text = MetricFormatter.Format(
            definition,
            widget,
            value,
            showLabel: true,
            showUnit: true,
            CultureInfo.InvariantCulture);

        Assert.Equal("↓ 12.0MB/s", text);
    }
}
