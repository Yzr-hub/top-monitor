namespace TopMonitor.Domain.Metrics;

/// <summary>
/// 指标的稳定业务标识。该值由 TopMonitor 定义，不使用底层传感器显示名称。
/// </summary>
public sealed record MetricId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
