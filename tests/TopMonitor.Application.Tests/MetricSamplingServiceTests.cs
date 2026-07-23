using Microsoft.Extensions.Logging.Abstractions;
using TopMonitor.Application.Metrics;
using TopMonitor.Application.Tests.Fakes;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Application.Tests;

public sealed class MetricSamplingServiceTests
{
    [Fact]
    public async Task Runs_by_subscription_interval_and_stops_after_cancellation()
    {
        var provider = CreateProvider(MetricIds.CpuTotalLoad);
        var cache = new MetricValueCache();
        await using var service = CreateService(provider, cache);
        await service.UpdateSubscriptionsAsync(
            [new MetricSubscription(MetricIds.CpuTotalLoad, TimeSpan.FromMilliseconds(20))],
            CancellationToken.None);

        await service.StartAsync(CancellationToken.None);
        await EventuallyAsync(() => provider.ReadCount >= 3);
        await service.StopAsync(CancellationToken.None);
        var countAfterStop = provider.ReadCount;
        await Task.Delay(80);

        Assert.Equal(countAfterStop, provider.ReadCount);
    }

    [Fact]
    public async Task Provider_failure_does_not_stop_other_providers()
    {
        var failing = CreateProvider(new MetricId("metric.failing"));
        failing = new FakeMetricProvider(await failing.DiscoverAsync(CancellationToken.None))
        {
            ReadException = new InvalidOperationException("boom")
        };
        var healthy = CreateProvider(MetricIds.MemoryUsagePercent);
        var cache = new MetricValueCache();
        await using var service = CreateService([failing, healthy], cache);
        await service.UpdateSubscriptionsAsync(
            [
                new MetricSubscription(new MetricId("metric.failing"), TimeSpan.FromMilliseconds(20)),
                new MetricSubscription(MetricIds.MemoryUsagePercent, TimeSpan.FromMilliseconds(20))
            ],
            CancellationToken.None);

        await service.StartAsync(CancellationToken.None);
        await EventuallyAsync(() => cache.TryGet(MetricIds.MemoryUsagePercent, out _));

        Assert.True(failing.ReadCount > 0);
        Assert.True(healthy.ReadCount > 0);
    }

    [Fact]
    public async Task Reads_only_subscribed_metrics()
    {
        var provider = new FakeMetricProvider(
        [
            CreateDefinition(MetricIds.CpuTotalLoad),
            CreateDefinition(MetricIds.MemoryUsagePercent)
        ]);
        await using var service = CreateService(provider, new MetricValueCache());
        await service.UpdateSubscriptionsAsync(
            [new MetricSubscription(MetricIds.MemoryUsagePercent, TimeSpan.FromMilliseconds(20))],
            CancellationToken.None);

        await service.StartAsync(CancellationToken.None);
        await EventuallyAsync(() => provider.ReadCount > 0);

        Assert.Equal([MetricIds.MemoryUsagePercent], provider.LastRequestedMetricIds);
    }

    [Fact]
    public async Task Does_not_notify_again_when_value_is_unchanged()
    {
        var provider = CreateProvider(MetricIds.CpuTotalLoad);
        var notifications = 0;
        await using var service = CreateService(provider, new MetricValueCache());
        service.ValuesChanged += (_, _) => Interlocked.Increment(ref notifications);
        await service.UpdateSubscriptionsAsync(
            [new MetricSubscription(MetricIds.CpuTotalLoad, TimeSpan.FromMilliseconds(20))],
            CancellationToken.None);

        await service.StartAsync(CancellationToken.None);
        await EventuallyAsync(() => provider.ReadCount >= 3);

        Assert.Equal(1, Volatile.Read(ref notifications));
    }

    [Fact]
    public async Task Subscription_change_replaces_active_metric_set()
    {
        var provider = new FakeMetricProvider(
        [
            CreateDefinition(MetricIds.CpuTotalLoad),
            CreateDefinition(MetricIds.MemoryUsagePercent)
        ]);
        await using var service = CreateService(provider, new MetricValueCache());
        await service.UpdateSubscriptionsAsync(
            [new MetricSubscription(MetricIds.CpuTotalLoad, TimeSpan.FromMilliseconds(20))],
            CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        await EventuallyAsync(
            () => provider.LastRequestedMetricIds.Contains(MetricIds.CpuTotalLoad));

        await service.UpdateSubscriptionsAsync(
            [new MetricSubscription(MetricIds.MemoryUsagePercent, TimeSpan.FromMilliseconds(20))],
            CancellationToken.None);
        await EventuallyAsync(
            () => provider.LastRequestedMetricIds.Contains(MetricIds.MemoryUsagePercent));

        Assert.DoesNotContain(MetricIds.CpuTotalLoad, provider.LastRequestedMetricIds);
    }

    [Fact]
    public async Task Cache_ignores_timestamp_only_changes()
    {
        var cache = new MetricValueCache();
        var first = MetricValue.Create(MetricIds.CpuTotalLoad, 42, DateTimeOffset.UtcNow);
        var second = MetricValue.Create(
            MetricIds.CpuTotalLoad,
            42,
            DateTimeOffset.UtcNow.AddSeconds(1));

        var firstChanged = cache.Update(first);
        var secondChanged = cache.Update(second);

        Assert.True(firstChanged);
        Assert.False(secondChanged);
        Assert.True(cache.TryGet(MetricIds.CpuTotalLoad, out var stored));
        Assert.Equal(second.Timestamp, stored.Timestamp);
        await Task.CompletedTask;
    }

    [Fact]
    public void Cache_ignores_small_utilization_jitter()
    {
        var cache = new MetricValueCache();
        cache.Update(MetricValue.Create(
            MetricIds.CpuTotalLoad,
            42,
            DateTimeOffset.UtcNow));

        var changed = cache.Update(MetricValue.Create(
            MetricIds.CpuTotalLoad,
            42.05,
            DateTimeOffset.UtcNow.AddMilliseconds(500)));

        Assert.False(changed);
    }

    [Fact]
    public void Cache_notifies_for_meaningful_utilization_change()
    {
        var cache = new MetricValueCache();
        cache.Update(MetricValue.Create(
            MetricIds.CpuTotalLoad,
            42,
            DateTimeOffset.UtcNow));

        var changed = cache.Update(MetricValue.Create(
            MetricIds.CpuTotalLoad,
            42.2,
            DateTimeOffset.UtcNow.AddMilliseconds(500)));

        Assert.True(changed);
    }

    private static MetricSamplingService CreateService(
        FakeMetricProvider provider,
        MetricValueCache cache) =>
        CreateService([provider], cache);

    private static MetricSamplingService CreateService(
        IReadOnlyCollection<FakeMetricProvider> providers,
        MetricValueCache cache) =>
        new(providers, cache, NullLogger<MetricSamplingService>.Instance);

    private static FakeMetricProvider CreateProvider(MetricId id) =>
        new([CreateDefinition(id)]);

    private static MetricDefinition CreateDefinition(MetricId id) =>
        new(
            id,
            id.Value,
            MetricCategory.Other,
            string.Empty,
            TimeSpan.FromMilliseconds(20),
            false);

    private static async Task EventuallyAsync(
        Func<bool> condition,
        int timeoutMilliseconds = 2_000)
    {
        using var timeout = new CancellationTokenSource(timeoutMilliseconds);
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
