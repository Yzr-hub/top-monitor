using TopMonitor.Domain.Metrics;

namespace TopMonitor.Application.Metrics;

/// <summary>
/// 一个当前启用指标及其实际采样周期。
/// </summary>
public sealed record MetricSubscription(MetricId MetricId, TimeSpan Interval);
