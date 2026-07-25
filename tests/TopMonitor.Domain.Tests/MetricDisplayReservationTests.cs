using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Formatting;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Tests;

public sealed class MetricDisplayReservationTests
{
    [Theory]
    [InlineData("hardware.cpu.temperature.package", "CPU", "°C", "CPU 125°C")]
    [InlineData("hardware.cpu.load.total", "CPU", "%", "CPU 100%")]
    [InlineData("graphics.foreground.fps", "FPS", "", "FPS 9999")]
    [InlineData(
        "system.network.active.download.bytes_per_second",
        "↓",
        "B/s",
        "↓ 999.9GB/s")]
    public void Reservation_is_stable_for_metric_kind(
        string id,
        string label,
        string unit,
        string expected)
    {
        var definition = new MetricDefinition(
            new MetricId(id),
            label,
            MetricCategory.Other,
            unit,
            TimeSpan.FromSeconds(1),
            false);
        var widget = new WidgetConfig(definition.Id, true, 10, label, "0.0");

        Assert.Equal(
            expected,
            MetricDisplayReservation.Create(definition, widget, true, true));
    }
}
