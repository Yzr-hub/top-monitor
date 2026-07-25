using TopMonitor.Infrastructure.Hardware;

namespace TopMonitor.Application.Tests;

public sealed class HardwareUpdateLimiterTests
{
    [Fact]
    public void Same_hardware_is_updated_once_inside_minimum_interval()
    {
        var limiter = new HardwareUpdateLimiter(TimeSpan.FromMilliseconds(400));
        var start = DateTimeOffset.Parse("2026-07-26T00:00:00Z");

        Assert.True(limiter.ShouldUpdate("/intelcpu/0", start));
        Assert.False(limiter.ShouldUpdate("/intelcpu/0", start.AddMilliseconds(250)));
        Assert.True(limiter.ShouldUpdate("/intelcpu/0", start.AddMilliseconds(400)));
    }

    [Fact]
    public void Different_hardware_has_independent_timestamps()
    {
        var limiter = new HardwareUpdateLimiter(TimeSpan.FromMilliseconds(400));
        var now = DateTimeOffset.Parse("2026-07-26T00:00:00Z");

        Assert.True(limiter.ShouldUpdate("/intelcpu/0", now));
        Assert.True(limiter.ShouldUpdate("/gpu-nvidia/0", now));
    }
}
