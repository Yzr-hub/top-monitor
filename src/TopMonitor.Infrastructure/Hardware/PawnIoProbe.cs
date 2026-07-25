using LibreHardwareMonitor.PawnIo;

namespace TopMonitor.Infrastructure.Hardware;

public sealed class PawnIoProbe : IPawnIoProbe
{
    public bool IsInstalled => PawnIo.IsInstalled;

    public Version? Version => PawnIo.Version;
}
