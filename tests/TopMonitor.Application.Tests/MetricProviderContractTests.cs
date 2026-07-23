using TopMonitor.Application.Metrics;
using TopMonitor.Application.Tests.Fakes;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Application.Tests;

public sealed class MetricProviderContractTests
{
    [Fact]
    public async Task Fake_provider_discovers_definitions_and_reads_only_requested_metrics()
    {
        var cpuDefinition = new MetricDefinition(
            MetricIds.CpuTotalLoad,
            "CPU 利用率",
            MetricCategory.Utilization,
            "%",
            TimeSpan.FromMilliseconds(500),
            false);
        var memoryDefinition = new MetricDefinition(
            MetricIds.MemoryUsagePercent,
            "内存使用率",
            MetricCategory.Memory,
            "%",
            TimeSpan.FromSeconds(1),
            false);
        var provider = new FakeMetricProvider([cpuDefinition, memoryDefinition]);

        var discovered = await provider.DiscoverAsync(CancellationToken.None);
        var values = await provider.ReadAsync(
            [MetricIds.MemoryUsagePercent],
            CancellationToken.None);

        Assert.Equal("fake", provider.ProviderId);
        Assert.Equal(2, discovered.Count);
        Assert.Single(values);
        Assert.Contains(MetricIds.MemoryUsagePercent, values.Keys);
        Assert.DoesNotContain(MetricIds.CpuTotalLoad, values.Keys);
        Assert.Equal(
            [MetricIds.MemoryUsagePercent],
            provider.LastRequestedMetricIds);
    }

    [Fact]
    public async Task Fake_provider_honors_pre_cancelled_token()
    {
        var provider = new FakeMetricProvider([]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.DiscoverAsync(cancellation.Token));
    }

}
