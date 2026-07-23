using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Tests;

public sealed class WidgetLayoutTests
{
    [Fact]
    public void Order_enabled_sorts_by_order_and_excludes_disabled_widgets()
    {
        var widgets = new[]
        {
            new WidgetConfig(new MetricId("metric.third"), true, 30, "Third", "0"),
            new WidgetConfig(new MetricId("metric.disabled"), false, 5, "Disabled", "0"),
            new WidgetConfig(new MetricId("metric.first"), true, 10, "First", "0"),
            new WidgetConfig(new MetricId("metric.second"), true, 20, "Second", "0")
        };

        var ordered = WidgetLayout.OrderEnabled(widgets);

        Assert.Collection(
            ordered,
            widget => Assert.Equal("metric.first", widget.MetricId.Value),
            widget => Assert.Equal("metric.second", widget.MetricId.Value),
            widget => Assert.Equal("metric.third", widget.MetricId.Value));
    }
}
