using TopMonitor.Application.Metrics;

namespace TopMonitor.Application.Tests;

public sealed class MetricPreviewGateTests
{
    [Fact]
    public void Preview_processing_follows_window_activation()
    {
        var gate = new MetricPreviewGate();

        Assert.False(gate.ShouldProcess);
        gate.SetActive(true);
        Assert.True(gate.ShouldProcess);
        gate.SetActive(false);
        Assert.False(gate.ShouldProcess);
    }
}
