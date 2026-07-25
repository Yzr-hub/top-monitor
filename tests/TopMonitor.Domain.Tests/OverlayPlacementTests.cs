using TopMonitor.Domain.Configuration;

namespace TopMonitor.Domain.Tests;

public sealed class OverlayPlacementTests
{
    [Theory]
    [InlineData(784, 888)]
    [InlineData(900, 830)]
    public void CalculateLeft_keeps_resized_top_center_window_centered(
        double windowWidth,
        double expectedLeft)
    {
        var left = OverlayPlacement.CalculateLeft(
            OverlayAnchor.TopCenter,
            workAreaLeft: 0,
            workAreaWidth: 2560,
            windowWidth,
            offsetX: 0);

        Assert.Equal(expectedLeft, left);
    }

    [Fact]
    public void CalculateLeft_preserves_custom_offset_after_resize()
    {
        var left = OverlayPlacement.CalculateLeft(
            OverlayAnchor.Custom,
            workAreaLeft: 100,
            workAreaWidth: 1920,
            windowWidth: 800,
            offsetX: 240);

        Assert.Equal(340, left);
    }
}
