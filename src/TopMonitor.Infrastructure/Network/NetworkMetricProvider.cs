using System.Diagnostics;
using System.Net.NetworkInformation;
using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Infrastructure.Network;

/// <summary>
/// 根据每张可用网卡的累计字节差值，自动选择当前流量最活跃的网卡。
/// </summary>
public sealed class NetworkMetricProvider : IMetricProvider
{
    private static readonly IReadOnlyList<MetricDefinition> Definitions =
    [
        new(
            MetricIds.ActiveNetworkDownload,
            "网络下载速度",
            MetricCategory.Network,
            "B/s",
            TimeSpan.FromSeconds(1),
            false),
        new(
            MetricIds.ActiveNetworkUpload,
            "网络上传速度",
            MetricCategory.Network,
            "B/s",
            TimeSpan.FromSeconds(1),
            false)
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, CounterSnapshot> _previous = new(StringComparer.Ordinal);
    private IReadOnlyList<NetworkInterface>? _interfaces;

    public string ProviderId => "system.network";

    public async Task<IReadOnlyList<MetricDefinition>> DiscoverAsync(
        CancellationToken cancellationToken)
    {
        await RescanAsync(cancellationToken);
        return Definitions;
    }

    public async Task<IReadOnlyDictionary<MetricId, MetricValue>> ReadAsync(
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken)
    {
        var requested = metricIds
            .Where(id => id == MetricIds.ActiveNetworkDownload ||
                         id == MetricIds.ActiveNetworkUpload)
            .ToArray();
        if (requested.Length == 0)
        {
            return new Dictionary<MetricId, MetricValue>();
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            _interfaces ??= FindEligibleInterfaces();
            var timestamp = DateTimeOffset.UtcNow;
            var now = Stopwatch.GetTimestamp();
            var samples = new List<RateSample>(_interfaces.Count);

            foreach (var networkInterface in _interfaces)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var statistics = networkInterface.GetIPStatistics();
                    var current = new CounterSnapshot(
                        statistics.BytesReceived,
                        statistics.BytesSent,
                        now);
                    if (_previous.TryGetValue(networkInterface.Id, out var previous) &&
                        current.Received >= previous.Received &&
                        current.Sent >= previous.Sent)
                    {
                        var elapsed = Stopwatch.GetElapsedTime(previous.Timestamp, now).TotalSeconds;
                        if (elapsed > 0)
                        {
                            samples.Add(new RateSample(
                                networkInterface.Id,
                                (current.Received - previous.Received) / elapsed,
                                (current.Sent - previous.Sent) / elapsed));
                        }
                    }

                    _previous[networkInterface.Id] = current;
                }
                catch (NetworkInformationException)
                {
                    // 单张网卡读取失败不影响其他网卡，下一次采样会再次尝试。
                }
            }

            var active = samples
                .OrderByDescending(sample => sample.Download + sample.Upload)
                .FirstOrDefault();
            return CreateValues(requested, active, timestamp);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RescanAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _interfaces = FindEligibleInterfaces();
            _previous.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IReadOnlyDictionary<MetricId, MetricValue> CreateValues(
        IReadOnlyCollection<MetricId> requested,
        RateSample? active,
        DateTimeOffset timestamp)
    {
        var result = new Dictionary<MetricId, MetricValue>(requested.Count);
        foreach (var id in requested)
        {
            if (active is null)
            {
                result[id] = MetricValue.Unavailable(id, timestamp, "等待第二次网络采样。");
                continue;
            }

            var value = id == MetricIds.ActiveNetworkDownload
                ? active.Download
                : active.Upload;
            result[id] = MetricValue.Create(id, Math.Max(0, value), timestamp);
        }

        return result;
    }

    private static IReadOnlyList<NetworkInterface> FindEligibleInterfaces() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderBy(networkInterface => networkInterface.Id, StringComparer.Ordinal)
            .ToArray();

    private sealed record CounterSnapshot(long Received, long Sent, long Timestamp);

    private sealed record RateSample(string InterfaceId, double Download, double Upload);
}
