using System.ComponentModel;
using Microsoft.Extensions.Logging;
using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Infrastructure.Fps;

public sealed class PresentMonFpsProvider(
    ForegroundFpsTracker tracker,
    TimeProvider timeProvider,
    ILogger<PresentMonFpsProvider> logger)
    : IMetricProvider, IAsyncDisposable
{
    private static readonly MetricDefinition Definition = new(
        MetricIds.ForegroundFps,
        "前台游戏帧率",
        MetricCategory.Other,
        string.Empty,
        TimeSpan.FromMilliseconds(500),
        RequiresAdministrator: false);

    private DateTimeOffset _nextErrorLog;

    public string ProviderId => "presentmon";

    public Task<IReadOnlyList<MetricDefinition>> DiscoverAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MetricDefinition>>([Definition]);

    public async Task<IReadOnlyDictionary<MetricId, MetricValue>> ReadAsync(
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken)
    {
        if (!metricIds.Contains(MetricIds.ForegroundFps))
        {
            return new Dictionary<MetricId, MetricValue>();
        }

        var timestamp = timeProvider.GetUtcNow();
        MetricValue value;
        try
        {
            var fps = await tracker.GetCurrentFpsAsync(cancellationToken);
            value = fps is > 0
                ? MetricValue.Create(MetricIds.ForegroundFps, fps.Value, timestamp)
                : MetricValue.Unavailable(
                    MetricIds.ForegroundFps,
                    timestamp,
                    "当前前台进程没有可用帧数据。");
        }
        catch (FileNotFoundException exception)
        {
            value = MetricValue.Unavailable(
                MetricIds.ForegroundFps,
                timestamp,
                exception.Message);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException ||
            exception is Win32Exception { NativeErrorCode: 5 })
        {
            value = MetricValue.Restricted(
                MetricIds.ForegroundFps,
                timestamp,
                "PresentMon 没有 ETW 访问权限。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (timestamp >= _nextErrorLog)
            {
                logger.LogWarning(exception, "PresentMon FPS 采样失败");
                _nextErrorLog = timestamp.AddMinutes(1);
            }

            value = MetricValue.Failed(
                MetricIds.ForegroundFps,
                timestamp,
                exception.Message);
        }

        return new Dictionary<MetricId, MetricValue>
        {
            [MetricIds.ForegroundFps] = value
        };
    }

    public Task RescanAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => tracker.DisposeAsync();
}
