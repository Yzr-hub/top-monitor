using TopMonitor.Domain.Metrics;

namespace TopMonitor.Application.Metrics;

/// <summary>
/// 一次采样中实际发生变化的指标集合。
/// </summary>
public sealed class MetricValuesChangedEventArgs(
    IReadOnlyList<MetricValue> values) : EventArgs
{
    public IReadOnlyList<MetricValue> Values { get; } = values;
}
