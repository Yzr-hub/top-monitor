using TopMonitor.Application.Fps;
using TopMonitor.Infrastructure.Fps;

namespace TopMonitor.Application.Tests;

public sealed class FpsSlidingWindowTests
{
    [Fact]
    public void Sixty_evenly_spaced_intervals_report_sixty_fps()
    {
        var window = new FpsSlidingWindow(TimeSpan.FromSeconds(1));
        for (var index = 0; index <= 60; index++)
        {
            window.Add(new PresentedFrame(42, index / 60d, "Hardware"));
        }

        Assert.Equal(60, window.GetFps(1d));
    }
}
