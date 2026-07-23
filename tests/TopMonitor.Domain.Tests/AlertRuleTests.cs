using TopMonitor.Domain.Alerts;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Tests;

public sealed class AlertRuleTests
{
    [Theory]
    [InlineData(79.9, false)]
    [InlineData(80, true)]
    [InlineData(95, true)]
    public void Greater_than_or_equal_rule_uses_configured_threshold(double value, bool expected)
    {
        var rule = new AlertRule(
            "cpu-high",
            MetricIds.CpuTotalLoad,
            AlertComparison.GreaterThanOrEqual,
            80,
            true);
        var metricValue = MetricValue.Create(MetricIds.CpuTotalLoad, value, DateTimeOffset.UtcNow);

        Assert.Equal(expected, rule.IsTriggeredBy(metricValue));
    }

    [Fact]
    public void Rule_does_not_trigger_for_unavailable_value()
    {
        var rule = new AlertRule(
            "cpu-high",
            MetricIds.CpuTotalLoad,
            AlertComparison.GreaterThanOrEqual,
            80,
            true);
        var metricValue = MetricValue.Unavailable(
            MetricIds.CpuTotalLoad,
            DateTimeOffset.UtcNow,
            "传感器不可用");

        Assert.False(rule.IsTriggeredBy(metricValue));
    }
}
