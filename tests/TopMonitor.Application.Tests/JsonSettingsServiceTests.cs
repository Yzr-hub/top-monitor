using Serilog;
using Serilog.Core;
using TopMonitor.Domain.Configuration;
using TopMonitor.Infrastructure.Configuration;

namespace TopMonitor.Application.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"TopMonitor.Tests.{Guid.NewGuid():N}");
    private readonly Logger _logger = new LoggerConfiguration().CreateLogger();

    [Fact]
    public async Task Missing_file_returns_default_settings()
    {
        var service = CreateService();

        var settings = await service.LoadAsync(CancellationToken.None);

        Assert.Equivalent(AppSettings.CreateDefault(), settings, strict: true);
    }

    [Fact]
    public async Task Saved_settings_can_be_loaded_again()
    {
        var service = CreateService();
        var expected = AppSettings.CreateDefault() with
        {
            RefreshMode = RefreshMode.PowerSaving,
            Overlay = AppSettings.CreateDefault().Overlay with { FontSize = 18 }
        };

        await service.SaveAsync(expected, CancellationToken.None);
        var loaded = await service.LoadAsync(CancellationToken.None);

        Assert.Equivalent(expected, loaded, strict: true);
        Assert.True(File.Exists(Path.Combine(_directory, "settings.json")));
    }

    [Fact]
    public async Task Corrupt_file_is_backed_up_and_defaults_are_returned()
    {
        Directory.CreateDirectory(_directory);
        var settingsPath = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ not-json");
        var service = CreateService();

        var settings = await service.LoadAsync(CancellationToken.None);

        Assert.Equivalent(AppSettings.CreateDefault(), settings, strict: true);
        Assert.NotEmpty(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    public void Dispose()
    {
        _logger.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private JsonSettingsService CreateService() =>
        new(_directory, _logger);
}
