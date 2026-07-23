namespace TopMonitor.Domain.Metrics;

/// <summary>
/// 指标来源设备的稳定描述，支持多 GPU、多磁盘和多网卡。
/// </summary>
public sealed record MonitorDescriptor(
    string DeviceId,
    string DisplayName,
    MetricCategory Category,
    bool IsAvailable,
    bool RequiresAdministrator,
    string? Details);
