using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using TopMonitor.Application.Configuration;
using TopMonitor.Domain.Configuration;

namespace TopMonitor.Infrastructure.Configuration;

/// <summary>
/// 使用 System.Text.Json 保存当前用户配置，支持原子写入和损坏恢复。
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly string _directory;
    private readonly string _settingsPath;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;

    public JsonSettingsService(string directory, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _settingsPath = Path.Combine(directory, "settings.json");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    public static JsonSettingsService CreateForCurrentUser(ILogger logger)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TopMonitor");
        return new JsonSettingsService(directory, logger);
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            await using var stream = new FileStream(
                _settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                _options,
                cancellationToken);
            return Normalize(settings);
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.Error(exception, "读取配置失败，将备份损坏文件并恢复默认配置");
            BackupCorruptFile();
            return AppSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(_directory);
        var temporaryPath = Path.Combine(
            _directory,
            $"settings.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings with { SchemaVersion = AppSettings.CurrentSchemaVersion },
                    _options,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(_settingsPath))
            {
                File.Copy(_settingsPath, $"{_settingsPath}.bak", overwrite: true);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private AppSettings Normalize(AppSettings? settings)
    {
        if (settings is null || settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
        {
            _logger.Warning("配置版本不受支持，恢复默认配置");
            return AppSettings.CreateDefault();
        }

        if (settings.SchemaVersion < AppSettings.CurrentSchemaVersion)
        {
            _logger.Information(
                "配置从版本 {OldVersion} 迁移到 {NewVersion}",
                settings.SchemaVersion,
                AppSettings.CurrentSchemaVersion);
        }

        return settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            Widgets = settings.Widgets ?? AppSettings.CreateDefault().Widgets
        };
    }

    private void BackupCorruptFile()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            Directory.CreateDirectory(_directory);
            var backupPath = Path.Combine(
                _directory,
                $"settings.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.json");
            File.Move(_settingsPath, backupPath, overwrite: true);
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "备份损坏配置文件失败");
        }
    }
}
