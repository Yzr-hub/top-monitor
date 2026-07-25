using TopMonitor.Infrastructure.Configuration;

namespace TopMonitor.Application.Tests;

public sealed class WindowsStartupServiceTests
{
    [Fact]
    public void Enabling_creates_elevated_task_and_removes_legacy_entry()
    {
        var tasks = new FakeStartupTaskController();
        var legacy = new FakeLegacyStartupEntry { ExistsValue = true };
        var service = new WindowsStartupService(
            @"C:\Program Files\TopMonitor\TopMonitor.exe",
            tasks,
            legacy);

        service.SetEnabled(true);

        Assert.Equal(
            [@"C:\Program Files\TopMonitor\TopMonitor.exe"],
            tasks.CreatedExecutables);
        Assert.Equal(1, legacy.DeleteCalls);
        Assert.True(service.IsEnabled());
    }

    [Fact]
    public void Enabling_existing_task_does_not_request_elevation_again()
    {
        var tasks = new FakeStartupTaskController { ExistsValue = true };
        var legacy = new FakeLegacyStartupEntry { ExistsValue = true };
        var service = new WindowsStartupService("TopMonitor.exe", tasks, legacy);

        service.SetEnabled(true);

        Assert.Empty(tasks.CreatedExecutables);
        Assert.Equal(1, legacy.DeleteCalls);
    }

    [Fact]
    public void Disabling_deletes_task_and_legacy_entry()
    {
        var tasks = new FakeStartupTaskController { ExistsValue = true };
        var legacy = new FakeLegacyStartupEntry { ExistsValue = true };
        var service = new WindowsStartupService("TopMonitor.exe", tasks, legacy);

        service.SetEnabled(false);

        Assert.Equal(1, tasks.DeleteCalls);
        Assert.Equal(1, legacy.DeleteCalls);
        Assert.False(service.IsEnabled());
    }

    private sealed class FakeStartupTaskController : IStartupTaskController
    {
        public bool ExistsValue { get; set; }
        public List<string> CreatedExecutables { get; } = [];
        public int DeleteCalls { get; private set; }

        public bool Exists() => ExistsValue;

        public void Create(string executablePath)
        {
            CreatedExecutables.Add(executablePath);
            ExistsValue = true;
        }

        public void Delete()
        {
            DeleteCalls++;
            ExistsValue = false;
        }
    }

    private sealed class FakeLegacyStartupEntry : ILegacyStartupEntry
    {
        public bool ExistsValue { get; set; }
        public int DeleteCalls { get; private set; }

        public bool Exists() => ExistsValue;

        public void Delete()
        {
            DeleteCalls++;
            ExistsValue = false;
        }
    }
}
