namespace TopMonitor.Infrastructure.Hardware;

public interface IPawnIoProbe
{
    bool IsInstalled { get; }

    Version? Version { get; }
}
