namespace TopMonitor.Domain.Metrics;

/// <summary>
/// 指标的业务分类，用于展示分组和默认格式选择。
/// </summary>
public enum MetricCategory
{
    Temperature,
    Utilization,
    Frequency,
    Power,
    Memory,
    Network,
    Disk,
    Other
}
