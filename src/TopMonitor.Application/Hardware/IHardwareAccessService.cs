namespace TopMonitor.Application.Hardware;

public interface IHardwareAccessService
{
    HardwareAccessStatus GetStatus();

    Task<HardwareAccessStatus> InitializeAsync(
        CancellationToken cancellationToken);
}
