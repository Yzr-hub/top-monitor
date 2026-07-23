using TopMonitor.Domain.Configuration;

namespace TopMonitor.Application.Configuration;

/// <summary>
/// 用户配置的持久化契约。
/// </summary>
public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
