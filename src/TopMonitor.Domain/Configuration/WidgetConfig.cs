using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Configuration;

/// <summary>
/// 一个指标在悬浮窗中的持久化展示配置。
/// </summary>
public sealed record WidgetConfig(
    MetricId MetricId,
    bool Enabled,
    int Order,
    string Label,
    string NumberFormat);
