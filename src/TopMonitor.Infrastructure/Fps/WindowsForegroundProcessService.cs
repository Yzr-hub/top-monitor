using System.ComponentModel;
using System.Diagnostics;
using TopMonitor.Application.Fps;
using TopMonitor.Infrastructure.Windows;

namespace TopMonitor.Infrastructure.Fps;

public sealed class WindowsForegroundProcessService : IForegroundProcessService
{
    private static readonly HashSet<string> ExcludedProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer",
            "dwm",
            "ShellExperienceHost",
            "SearchHost",
            "StartMenuExperienceHost"
        };

    public ForegroundProcessInfo? GetForegroundProcess()
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == nint.Zero ||
            NativeMethods.GetWindowThreadProcessId(window, out var processId) == 0 ||
            processId == 0 ||
            processId == Environment.ProcessId)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (process.HasExited ||
                ExcludedProcesses.Contains(process.ProcessName))
            {
                return null;
            }

            return new ForegroundProcessInfo(
                process.Id,
                process.ProcessName,
                new DateTimeOffset(process.StartTime).ToUniversalTime());
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }
}
