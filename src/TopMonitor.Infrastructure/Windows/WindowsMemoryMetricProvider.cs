using System.ComponentModel;
using System.Runtime.InteropServices;
using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Infrastructure.Windows;

/// <summary>
/// 通过 Windows 原生内存状态提供内存使用率和已用容量。
/// </summary>
public sealed class WindowsMemoryMetricProvider : IMetricProvider
{
    private static readonly IReadOnlyList<MetricDefinition> Definitions =
    [
        new(
            MetricIds.MemoryUsagePercent,
            "内存使用率",
            MetricCategory.Memory,
            "%",
            TimeSpan.FromSeconds(1),
            false),
        new(
            MetricIds.MemoryUsedBytes,
            "内存已使用",
            MetricCategory.Memory,
            "B",
            TimeSpan.FromSeconds(1),
            false)
    ];

    public string ProviderId => "windows.memory";

    public Task<IReadOnlyList<MetricDefinition>> DiscoverAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Definitions);
    }

    public Task<IReadOnlyDictionary<MetricId, MetricValue>> ReadAsync(
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = metricIds
            .Where(id => id == MetricIds.MemoryUsagePercent || id == MetricIds.MemoryUsedBytes)
            .ToArray();
        if (requested.Length == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<MetricId, MetricValue>>(
                new Dictionary<MetricId, MetricValue>());
        }

        var timestamp = DateTimeOffset.UtcNow;
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyDictionary<MetricId, MetricValue>>(
                requested.ToDictionary(
                    id => id,
                    id => MetricValue.Unavailable(id, timestamp, "物理内存指标仅能在 Windows 上读取。")));
        }

        var status = NativeMethods.MemoryStatusEx.Create();
        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
        {
            var message = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return Task.FromResult<IReadOnlyDictionary<MetricId, MetricValue>>(
                requested.ToDictionary(
                    id => id,
                    id => MetricValue.Failed(id, timestamp, message)));
        }

        var usedBytes = status.TotalPhysical - status.AvailablePhysical;
        var usagePercent = 100d * usedBytes / status.TotalPhysical;
        var result = new Dictionary<MetricId, MetricValue>(requested.Length);
        foreach (var id in requested)
        {
            var value = id == MetricIds.MemoryUsagePercent ? usagePercent : usedBytes;
            result[id] = MetricValue.Create(id, value, timestamp);
        }

        return Task.FromResult<IReadOnlyDictionary<MetricId, MetricValue>>(result);
    }

    public Task RescanAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
