using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Alerts;

/// <summary>
/// 单指标阈值告警规则。
/// </summary>
public sealed record AlertRule(
    string Id,
    MetricId MetricId,
    AlertComparison Comparison,
    double Threshold,
    bool Enabled)
{
    public bool IsTriggeredBy(MetricValue metricValue)
    {
        ArgumentNullException.ThrowIfNull(metricValue);

        if (!Enabled ||
            metricValue.Id != MetricId ||
            metricValue.Status != MetricStatus.Available ||
            metricValue.Value is not { } value)
        {
            return false;
        }

        return Comparison switch
        {
            AlertComparison.GreaterThan => value > Threshold,
            AlertComparison.GreaterThanOrEqual => value >= Threshold,
            AlertComparison.LessThan => value < Threshold,
            AlertComparison.LessThanOrEqual => value <= Threshold,
            _ => false
        };
    }
}
