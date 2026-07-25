using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Serilog.Core;
using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Metrics;
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

    [Fact]
    public async Task Version_one_settings_add_FPS_without_replacing_user_widgets()
    {
        Directory.CreateDirectory(_directory);
        var defaults = AppSettings.CreateDefault();
        var legacy = defaults with
        {
            SchemaVersion = 1,
            Widgets =
            [
                new WidgetConfig(
                    MetricIds.CpuTotalLoad,
                    false,
                    20,
                    "My CPU",
                    "0")
            ]
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            JsonSerializer.Serialize(legacy, options));

        var loaded = await CreateService().LoadAsync(CancellationToken.None);

        Assert.Contains(
            loaded.Widgets,
            widget => widget.MetricId == MetricIds.ForegroundFps);
        Assert.Contains(
            loaded.Widgets,
            widget => widget.MetricId == MetricIds.CpuTotalLoad &&
                      !widget.Enabled &&
                      widget.Label == "My CPU");
        Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
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
