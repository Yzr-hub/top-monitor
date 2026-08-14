using System.Diagnostics;
using TopMonitor.Application.Fps;

namespace TopMonitor.Infrastructure.Fps;

public sealed class PresentMonSessionFactory(string executablePath)
    : IPresentMonSessionFactory
{
    public async Task<IPresentMonSession> StartAsync(
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

        await CleanupExistingSessionAsync(cancellationToken);

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

            return new PresentMonProcessSession(process);
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
        startInfo.ArgumentList.Add("--session_name");
        startInfo.ArgumentList.Add("TopMonitorCapture");
        return startInfo;
    }

    public static ProcessStartInfo CreateCleanupStartInfo(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var startInfo = CreateBaseStartInfo(executablePath);
        startInfo.ArgumentList.Add("--terminate_existing_session");
        startInfo.ArgumentList.Add("--session_name");
        startInfo.ArgumentList.Add("TopMonitorCapture");
        return startInfo;
    }

    private async Task CleanupExistingSessionAsync(
        CancellationToken cancellationToken)
    {
        using var cleanup = new Process
        {
            StartInfo = CreateCleanupStartInfo(executablePath)
        };
        if (!cleanup.Start())
        {
            throw new InvalidOperationException(
                "PresentMon 会话清理进程启动失败。");
        }

        var output = cleanup.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = cleanup.StandardError.ReadToEndAsync(cancellationToken);
        await cleanup.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(output, error);
        var errorMessage = (await error).Trim();
        if (!IsCleanupSuccessful(cleanup.ExitCode, errorMessage))
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(errorMessage)
                    ? $"PresentMon 会话清理失败，代码：{cleanup.ExitCode}。"
                    : errorMessage);
        }
    }

    public static bool IsCleanupSuccessful(int exitCode, string standardError) =>
        exitCode == 0 ||
        (exitCode == 7 && standardError.Contains(
            "no existing sessions found",
            StringComparison.OrdinalIgnoreCase));

    private static ProcessStartInfo CreateBaseStartInfo(string executablePath) =>
        new(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
}
