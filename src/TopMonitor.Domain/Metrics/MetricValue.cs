namespace TopMonitor.Domain.Metrics;

/// <summary>
/// 某个指标在特定时间点的采样结果。
/// </summary>
public sealed record MetricValue
{
    private MetricValue(
        MetricId id,
        double? value,
        DateTimeOffset timestamp,
        MetricStatus status,
        string? errorMessage)
    {
        Id = id;
        Value = value;
        Timestamp = timestamp;
        Status = status;
        ErrorMessage = errorMessage;
    }

    public MetricId Id { get; }

    public double? Value { get; }

    public DateTimeOffset Timestamp { get; }

    public MetricStatus Status { get; }

    public string? ErrorMessage { get; }

    /// <summary>
    /// 创建可用采样值。NaN 和无穷值会在领域边界被归一化为无效状态。
    /// </summary>
    public static MetricValue Create(MetricId id, double value, DateTimeOffset timestamp)
    {
        return double.IsFinite(value)
            ? new MetricValue(id, value, timestamp, MetricStatus.Available, null)
            : new MetricValue(id, null, timestamp, MetricStatus.Invalid, "采样值不是有限数字。");
    }

    public static MetricValue Unavailable(
        MetricId id,
        DateTimeOffset timestamp,
        string? reason = null) =>
        new(id, null, timestamp, MetricStatus.Unavailable, reason);

    public static MetricValue Restricted(
        MetricId id,
        DateTimeOffset timestamp,
        string? reason = null) =>
        new(id, null, timestamp, MetricStatus.Restricted, reason);

    public static MetricValue Failed(
        MetricId id,
        DateTimeOffset timestamp,
        string errorMessage) =>
        new(id, null, timestamp, MetricStatus.Error, errorMessage);
}
