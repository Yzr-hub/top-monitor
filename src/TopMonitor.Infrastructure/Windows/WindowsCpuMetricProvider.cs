using System.ComponentModel;
using System.Runtime.InteropServices;
using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Infrastructure.Windows;

/// <summary>
/// 使用 Windows 系统累计时间计算 CPU 总利用率。
/// </summary>
public sealed class WindowsCpuMetricProvider : IMetricProvider
{
    private static readonly IReadOnlyList<MetricDefinition> Definitions =
    [
        new(
            MetricIds.CpuTotalLoad,
            "CPU 总利用率",
            MetricCategory.Utilization,
            "%",
            TimeSpan.FromMilliseconds(500),
            false)
    ];

    private readonly object _sync = new();
    private CpuTimes? _previous;

    public string ProviderId => "windows.cpu";

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
        if (!metricIds.Contains(MetricIds.CpuTotalLoad))
        {
            return Task.FromResult<IReadOnlyDictionary<MetricId, MetricValue>>(
                new Dictionary<MetricId, MetricValue>());
        }

        var timestamp = DateTimeOffset.UtcNow;
        MetricValue value;

        if (!OperatingSystem.IsWindows())
        {
            value = MetricValue.Unavailable(
                MetricIds.CpuTotalLoad,
                timestamp,
                "CPU 系统时间仅能在 Windows 上读取。");
        }
        else if (!TryReadTimes(out var current, out var error))
        {
            value = MetricValue.Failed(MetricIds.CpuTotalLoad, timestamp, error);
        }
        else
        {
            lock (_sync)
            {
                value = CalculateValue(current, timestamp);
                _previous = current;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<MetricId, MetricValue>>(
            new Dictionary<MetricId, MetricValue> { [MetricIds.CpuTotalLoad] = value });
    }

    public Task RescanAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _previous = null;
        }

        return Task.CompletedTask;
    }

    private MetricValue CalculateValue(CpuTimes current, DateTimeOffset timestamp)
    {
        if (_previous is not { } previous ||
            current.Idle < previous.Idle ||
            current.Kernel < previous.Kernel ||
            current.User < previous.User)
        {
            return MetricValue.Unavailable(
                MetricIds.CpuTotalLoad,
                timestamp,
                "等待第二次 CPU 采样。");
        }

        var idleDelta = current.Idle - previous.Idle;
        var kernelDelta = current.Kernel - previous.Kernel;
        var userDelta = current.User - previous.User;
        var totalDelta = kernelDelta + userDelta;
        if (totalDelta == 0 || idleDelta > totalDelta)
        {
            return MetricValue.Unavailable(
                MetricIds.CpuTotalLoad,
                timestamp,
                "CPU 采样间隔过短或计数器无变化。");
        }

        var usage = 100d * (totalDelta - idleDelta) / totalDelta;
        return MetricValue.Create(MetricIds.CpuTotalLoad, Math.Clamp(usage, 0, 100), timestamp);
    }

    private static bool TryReadTimes(out CpuTimes times, out string error)
    {
        if (NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            times = new CpuTimes(idle.ToUInt64(), kernel.ToUInt64(), user.ToUInt64());
            error = string.Empty;
            return true;
        }

        times = default;
        error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        return false;
    }

    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
}
