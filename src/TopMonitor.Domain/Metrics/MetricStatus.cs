namespace TopMonitor.Domain.Metrics;

/// <summary>
/// 一次指标采样的状态。
/// </summary>
public enum MetricStatus
{
    Available,
    Unavailable,
    Restricted,
    Invalid,
    Error
}
