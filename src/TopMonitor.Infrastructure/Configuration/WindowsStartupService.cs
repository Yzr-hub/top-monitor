using System.Diagnostics;
using Microsoft.Win32;
using TopMonitor.Application.Configuration;

namespace TopMonitor.Infrastructure.Configuration;

public interface IStartupTaskController
{
    bool Exists();

    void Create(string executablePath);

    void Delete();
}

public interface ILegacyStartupEntry
{
    bool Exists();

    void Delete();
}

public sealed class WindowsStartupService : IStartupService
{
    private readonly string _executablePath;
    private readonly IStartupTaskController _taskController;
    private readonly ILegacyStartupEntry _legacyEntry;

    public WindowsStartupService(string executablePath)
        : this(
            executablePath,
            new ScheduledTaskController(),
            new LegacyStartupEntry())
    {
    }

    public WindowsStartupService(
        string executablePath,
        IStartupTaskController taskController,
        ILegacyStartupEntry legacyEntry)
    {
        _executablePath = executablePath;
        _taskController = taskController;
        _legacyEntry = legacyEntry;
    }

    public bool IsEnabled() =>
        OperatingSystem.IsWindows() &&
        (_taskController.Exists() || _legacyEntry.Exists());

    public void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (enabled)
        {
            if (!_taskController.Exists())
            {
                _taskController.Create(_executablePath);
            }

            _legacyEntry.Delete();
            return;
        }

        if (_taskController.Exists())
        {
            _taskController.Delete();
        }

        _legacyEntry.Delete();
    }
}

public sealed class ScheduledTaskController : IStartupTaskController
{
    private const string TaskName = "TopMonitor";
    private readonly string _schedulerPath =
        Path.Combine(Environment.SystemDirectory, "schtasks.exe");

    public bool Exists()
    {
        using var process = Start(
            useElevation: false,
            "/Query",
            "/TN",
            TaskName);
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    public void Create(string executablePath)
    {
        using var process = Start(
            useElevation: true,
            "/Create",
            "/TN",
            TaskName,
            "/TR",
            $"\"{executablePath}\"",
            "/SC",
            "ONLOGON",
            "/RL",
            "HIGHEST",
            "/IT",
            "/F");
        process.WaitForExit();
        EnsureSuccess(process, "创建");
    }

    public void Delete()
    {
        using var process = Start(
            useElevation: true,
            "/Delete",
            "/TN",
            TaskName,
            "/F");
        process.WaitForExit();
        EnsureSuccess(process, "删除");
    }

    private Process Start(bool useElevation, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(_schedulerPath)
        {
            UseShellExecute = useElevation,
            CreateNoWindow = !useElevation,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        if (useElevation)
        {
            startInfo.Verb = "runas";
        }
        else
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ??
               throw new InvalidOperationException("无法启动 Windows 任务计划程序。");
    }

    private static void EnsureSuccess(Process process, string operation)
    {
        if (process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{operation} TopMonitor 开机启动任务失败，退出代码：{process.ExitCode}。");
    }
}

public sealed class LegacyStartupEntry : ILegacyStartupEntry
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TopMonitor";

    public bool Exists()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(
            RunKeyPath,
            writable: false);
        return key?.GetValue(ValueName) is string value &&
               !string.IsNullOrWhiteSpace(value);
    }

    public void Delete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(
            RunKeyPath,
            writable: true);
        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
