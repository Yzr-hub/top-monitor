using TopMonitor.Domain.Metrics;

namespace TopMonitor.Application.Metrics;

/// <summary>
/// 指标数据源的统一应用层契约。
/// </summary>
public interface IMetricProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<MetricDefinition>> DiscoverAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<MetricId, MetricValue>> ReadAsync(
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// 重新发现设备。常规采样不得调用此方法。
    /// </summary>
    Task RescanAsync(CancellationToken cancellationToken);
}
