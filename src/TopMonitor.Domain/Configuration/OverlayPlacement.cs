namespace TopMonitor.Domain.Configuration;

public static class OverlayPlacement
{
    public static double CalculateLeft(
        OverlayAnchor anchor,
        double workAreaLeft,
        double workAreaWidth,
        double windowWidth,
        double offsetX)
    {
        var availableWidth = Math.Max(0, workAreaWidth - windowWidth);
        var desiredLeft = anchor switch
        {
            OverlayAnchor.TopLeft => workAreaLeft,
            OverlayAnchor.TopRight => workAreaLeft + availableWidth,
            OverlayAnchor.Custom => workAreaLeft + offsetX,
            _ => workAreaLeft + (availableWidth / 2)
        };

        return Math.Clamp(
            desiredLeft,
            workAreaLeft,
            workAreaLeft + availableWidth);
    }
}
