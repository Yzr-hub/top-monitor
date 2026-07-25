namespace TopMonitor.Application.Metrics;

public sealed class MetricPreviewGate
{
    private int _isActive;

    public bool ShouldProcess => Volatile.Read(ref _isActive) != 0;

    public void SetActive(bool active) =>
        Volatile.Write(ref _isActive, active ? 1 : 0);
}
