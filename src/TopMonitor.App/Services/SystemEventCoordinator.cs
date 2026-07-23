using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TopMonitor.Application.Metrics;

namespace TopMonitor.App.Services;

/// <summary>
/// 将 Windows 静态系统事件转发到窗口和采样服务，并负责完整退订。
/// </summary>
public sealed class SystemEventCoordinator : IDisposable
{
    private readonly MainWindow _window;
    private readonly MetricSamplingService _samplingService;
    private readonly ILogger<SystemEventCoordinator> _logger;
    private bool _started;

    public SystemEventCoordinator(
        MainWindow window,
        MetricSamplingService samplingService,
        ILogger<SystemEventCoordinator> logger)
    {
        _window = window;
        _samplingService = samplingService;
        _logger = logger;
    }

    public void Start()
    {
        if (_started || !OperatingSystem.IsWindows())
        {
            return;
        }

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _started = true;
    }

    public void Dispose()
    {
        if (!_started)
        {
            return;
        }

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _started = false;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs eventArgs)
    {
        _ = _window.Dispatcher.BeginInvoke(_window.RepositionToConfiguredDisplay);
        _logger.LogInformation("显示器配置已变化，悬浮窗已重新定位");
    }

    private async void OnPowerModeChanged(object sender, PowerModeChangedEventArgs eventArgs)
    {
        if (eventArgs.Mode != PowerModes.Resume)
        {
            return;
        }

        try
        {
            await _samplingService.RescanProvidersAsync(CancellationToken.None);
            _ = _window.Dispatcher.BeginInvoke(_window.RepositionToConfiguredDisplay);
            _logger.LogInformation("系统已从休眠恢复，硬件和网络采样已重新初始化");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "系统恢复后的重新初始化失败");
        }
    }
}
