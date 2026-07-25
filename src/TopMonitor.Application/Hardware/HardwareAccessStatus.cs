namespace TopMonitor.Application.Hardware;

public sealed record HardwareAccessStatus(
    bool IsInstalled,
    Version? Version,
    bool InstallerAvailable,
    string Message);
