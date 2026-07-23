using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Tests;

public sealed class MetricIdTests
{
    [Fact]
    public void Equal_values_are_equal_and_have_the_same_hash_code()
    {
        var first = new MetricId("hardware.cpu.load.total");
        var second = new MetricId("hardware.cpu.load.total");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
