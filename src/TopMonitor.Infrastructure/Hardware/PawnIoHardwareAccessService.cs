using System.ComponentModel;
using Microsoft.Extensions.Logging;
using TopMonitor.Application.Hardware;

namespace TopMonitor.Infrastructure.Hardware;

public sealed class PawnIoHardwareAccessService : IHardwareAccessService
{
    private readonly IPawnIoProbe _probe;
    private readonly IElevatedProcessRunner _runner;
    private readonly string _installerPath;
    private readonly ILogger<PawnIoHardwareAccessService> _logger;

    public PawnIoHardwareAccessService(
        IPawnIoProbe probe,
        IElevatedProcessRunner runner,
        string appBaseDirectory,
        ILogger<PawnIoHardwareAccessService> logger)
    {
        _probe = probe;
        _runner = runner;
        _installerPath = Path.Combine(
            appBaseDirectory,
            "Dependencies",
            "PawnIO_setup.exe");
        _logger = logger;
    }

    public HardwareAccessStatus GetStatus()
    {
        var installed = _probe.IsInstalled;
        var installerAvailable = File.Exists(_installerPath);
        var message = installed
            ? $"PawnIO 已安装{FormatVersion(_probe.Version)}，CPU 温度访问已启用。"
            : installerAvailable
                ? "尚未安装 PawnIO。点击初始化后只需确认一次管理员权限。"
                : "未找到 PawnIO 安装程序，请重新发布或下载完整版本。";

        return new HardwareAccessStatus(
            installed,
            _probe.Version,
            installerAvailable,
            message);
    }

    public async Task<HardwareAccessStatus> InitializeAsync(
        CancellationToken cancellationToken)
    {
        var current = GetStatus();
        if (current.IsInstalled || !current.InstallerAvailable)
        {
            return current;
        }

        int exitCode;
        try
        {
            exitCode = await _runner.RunAsync(
                _installerPath,
                "-install",
                cancellationToken);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            _logger.LogInformation("PawnIO elevation was cancelled by the user");
            return current with
            {
                Message = "已取消 PawnIO 初始化，未对系统进行更改。"
            };
        }
        if (exitCode != 0)
        {
            _logger.LogWarning(
                "PawnIO installer exited with code {ExitCode}",
                exitCode);
            return current with
            {
                Message = $"PawnIO 安装未完成（退出代码 {exitCode}）。"
            };
        }

        var status = GetStatus();
        _logger.LogInformation(
            "PawnIO initialization completed; installed: {IsInstalled}, version: {Version}",
            status.IsInstalled,
            status.Version);
        return status;
    }

    private static string FormatVersion(Version? version) =>
        version is null ? string.Empty : $"（{version}）";
}
