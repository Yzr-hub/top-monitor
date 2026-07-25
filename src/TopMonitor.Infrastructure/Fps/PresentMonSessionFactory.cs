using System.Diagnostics;
using TopMonitor.Application.Fps;

namespace TopMonitor.Infrastructure.Fps;

public sealed class PresentMonSessionFactory(string executablePath)
    : IPresentMonSessionFactory
{
    public Task<IPresentMonSession> StartAsync(
        int processId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "未找到 PresentMon 运行文件。",
                executablePath);
        }

        var process = new Process
        {
            StartInfo = CreateStartInfo(executablePath, processId)
        };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("PresentMon 进程启动失败。");
            }

            return Task.FromResult<IPresentMonSession>(
                new PresentMonProcessSession(process));
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    public static ProcessStartInfo CreateStartInfo(
        string executablePath,
        int processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--process_id");
        startInfo.ArgumentList.Add(
            processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--output_stdout");
        startInfo.ArgumentList.Add("--v1_metrics");
        startInfo.ArgumentList.Add("--terminate_on_proc_exit");
        startInfo.ArgumentList.Add("--session_name");
        startInfo.ArgumentList.Add(
            $"TopMonitor_{Environment.ProcessId}_{processId}");
        return startInfo;
    }
}
