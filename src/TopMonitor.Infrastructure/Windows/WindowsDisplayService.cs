using TopMonitor.Application.Displays;

namespace TopMonitor.Infrastructure.Windows;

public sealed class WindowsDisplayService : IDisplayService
{
    private const uint PrimaryMonitorFlag = 1;

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var displays = new List<DisplayInfo>();
        NativeMethods.MonitorEnumProcedure callback = (
            monitor,
            _,
            _,
            _) =>
        {
            var info = NativeMethods.MonitorInfo.Create();
            if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            displays.Add(new DisplayInfo(
                info.DeviceName,
                info.DeviceName,
                info.WorkArea.Left,
                info.WorkArea.Top,
                info.WorkArea.Right - info.WorkArea.Left,
                info.WorkArea.Bottom - info.WorkArea.Top,
                (info.Flags & PrimaryMonitorFlag) != 0));
            return true;
        };
        NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, callback, nint.Zero);
        return displays;
    }
}
