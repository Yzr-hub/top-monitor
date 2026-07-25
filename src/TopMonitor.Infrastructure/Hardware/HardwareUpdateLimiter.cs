namespace TopMonitor.Infrastructure.Hardware;

public sealed class HardwareUpdateLimiter(TimeSpan minimumInterval)
{
    private readonly Dictionary<string, DateTimeOffset> _lastUpdates =
        new(StringComparer.Ordinal);

    public bool ShouldUpdate(string hardwareId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hardwareId);

        if (_lastUpdates.TryGetValue(hardwareId, out var lastUpdate) &&
            now - lastUpdate < minimumInterval)
        {
            return false;
        }

        _lastUpdates[hardwareId] = now;
        return true;
    }
}
