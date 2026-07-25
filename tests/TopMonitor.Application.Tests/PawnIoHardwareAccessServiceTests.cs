using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using TopMonitor.Infrastructure.Hardware;

namespace TopMonitor.Application.Tests;

public sealed class PawnIoHardwareAccessServiceTests
{
    [Fact]
    public void Missing_installer_is_reported_without_starting_a_process()
    {
        var runner = new FakeElevatedProcessRunner();
        var service = new PawnIoHardwareAccessService(
            new FakePawnIoProbe(false, null),
            runner,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            NullLogger<PawnIoHardwareAccessService>.Instance);

        var status = service.GetStatus();

        Assert.False(status.IsInstalled);
        Assert.False(status.InstallerAvailable);
        Assert.Empty(runner.Starts);
    }

    [Fact]
    public async Task Successful_install_rechecks_PawnIo_status()
    {
        var appDirectory = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));
        var dependencyDirectory = Path.Combine(appDirectory, "Dependencies");
        Directory.CreateDirectory(dependencyDirectory);
        var installerPath = Path.Combine(dependencyDirectory, "PawnIO_setup.exe");
        await File.WriteAllBytesAsync(installerPath, []);

        try
        {
            var probe = new FakePawnIoProbe(false, null);
            var runner = new FakeElevatedProcessRunner
            {
                OnRun = () =>
                {
                    probe.SetInstalled(new Version(2, 1));
                    return 0;
                }
            };
            var service = new PawnIoHardwareAccessService(
                probe,
                runner,
                appDirectory,
                NullLogger<PawnIoHardwareAccessService>.Instance);

            var status = await service.InitializeAsync(CancellationToken.None);

            Assert.True(status.IsInstalled);
            Assert.Equal(new Version(2, 1), status.Version);
            Assert.Equal([(installerPath, "-install")], runner.Starts);
        }
        finally
        {
            Directory.Delete(appDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Cancelled_elevation_returns_previous_status()
    {
        var appDirectory = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));
        var dependencyDirectory = Path.Combine(appDirectory, "Dependencies");
        Directory.CreateDirectory(dependencyDirectory);
        await File.WriteAllBytesAsync(
            Path.Combine(dependencyDirectory, "PawnIO_setup.exe"),
            []);

        try
        {
            var runner = new FakeElevatedProcessRunner
            {
                OnRun = () => throw new Win32Exception(1223)
            };
            var service = new PawnIoHardwareAccessService(
                new FakePawnIoProbe(false, null),
                runner,
                appDirectory,
                NullLogger<PawnIoHardwareAccessService>.Instance);

            var status = await service.InitializeAsync(CancellationToken.None);

            Assert.False(status.IsInstalled);
            Assert.Contains("取消", status.Message);
        }
        finally
        {
            Directory.Delete(appDirectory, recursive: true);
        }
    }

    private sealed class FakePawnIoProbe(bool isInstalled, Version? version) : IPawnIoProbe
    {
        private bool _isInstalled = isInstalled;
        private Version? _version = version;

        public bool IsInstalled => _isInstalled;

        public Version? Version => _version;

        public void SetInstalled(Version version)
        {
            _isInstalled = true;
            _version = version;
        }
    }

    private sealed class FakeElevatedProcessRunner : IElevatedProcessRunner
    {
        public List<(string FileName, string Arguments)> Starts { get; } = [];

        public Func<int> OnRun { get; init; } = () => 0;

        public Task<int> RunAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken)
        {
            Starts.Add((fileName, arguments));
            return Task.FromResult(OnRun());
        }
    }
}
