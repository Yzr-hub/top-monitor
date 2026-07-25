using System.Diagnostics;

namespace TopMonitor.Infrastructure.Hardware;

public sealed class ElevatedProcessRunner : IElevatedProcessRunner
{
    public async Task<int> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true,
            Verb = "runas"
        }) ?? throw new InvalidOperationException(
            $"无法启动提权进程：{fileName}");

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
