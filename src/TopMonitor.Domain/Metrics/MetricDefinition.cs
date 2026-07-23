namespace TopMonitor.Domain.Metrics;

/// <summary>
/// 描述一个可发现指标的稳定元数据。
/// </summary>
public sealed record MetricDefinition(
    MetricId Id,
    string DisplayName,
    MetricCategory Category,
    string Unit,
    TimeSpan RecommendedInterval,
    bool RequiresAdministrator);
