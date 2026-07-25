using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using TopMonitor.App.Services;
using TopMonitor.App.ViewModels;
using TopMonitor.Application.Configuration;
using TopMonitor.Application.Displays;
using TopMonitor.Application.Fps;
using TopMonitor.Application.Hardware;
using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Configuration;
using TopMonitor.Infrastructure.Hardware;
using TopMonitor.Infrastructure.Configuration;
using TopMonitor.Infrastructure.Fps;
using TopMonitor.Infrastructure.Network;
using TopMonitor.Infrastructure.Windows;

namespace TopMonitor.App;

/// <summary>
/// WPF 组合根：创建日志、依赖注入、采样服务、悬浮窗和托盘。
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIconService? _trayIcon;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        ConfigureLogging();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var settingsService = JsonSettingsService.CreateForCurrentUser(Log.Logger);
            var settings = await settingsService.LoadAsync(CancellationToken.None);
            _services = ConfigureServices(settings, settingsService);
            _mainWindow = _services.GetRequiredService<MainWindow>();
            _settingsWindow = _services.GetRequiredService<SettingsWindow>();
            _mainWindow.ExitRequested += OnMainWindowExitRequested;
            var viewModel = _services.GetRequiredService<OverlayViewModel>();
            var samplingService = _services.GetRequiredService<MetricSamplingService>();
            var systemEvents = _services.GetRequiredService<SystemEventCoordinator>();

            await viewModel.InitializeAsync(CancellationToken.None);
            await samplingService.StartAsync(CancellationToken.None);
            systemEvents.Start();

            _trayIcon = new TrayIconService(
                _mainWindow,
                viewModel,
                samplingService,
                _settingsWindow.ShowAndActivate,
                RequestExitAsync,
                _services.GetRequiredService<ILogger<TrayIconService>>());
            if (!settings.MinimizeOnStartup)
            {
                _mainWindow.Show();
            }
            Log.Information("TopMonitor 启动完成");
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "TopMonitor 启动失败");
            MessageBox.Show(
                "TopMonitor 启动失败，请查看日志目录。",
                "TopMonitor",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            await RequestExitAsync();
        }
    }

    private static void ConfigureLogging()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TopMonitor",
            "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "topmonitor-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();
    }

    private static ServiceProvider ConfigureServices(
        AppSettings settings,
        ISettingsService settingsService)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false));
        services.AddSingleton<Serilog.ILogger>(Log.Logger);
        services.AddSingleton(settings);
        services.AddSingleton(settingsService);
        services.AddSingleton<IStartupService>(
            new WindowsStartupService(Environment.ProcessPath ?? "TopMonitor.exe"));
        services.AddSingleton<IDisplayService, WindowsDisplayService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPawnIoProbe, PawnIoProbe>();
        services.AddSingleton<IElevatedProcessRunner, ElevatedProcessRunner>();
        services.AddSingleton<PerformanceLogUsersService>();
        services.AddSingleton<MetricPreviewGate>();
        services.AddSingleton<IHardwareAccessService>(provider =>
            new PawnIoHardwareAccessService(
                provider.GetRequiredService<IPawnIoProbe>(),
                provider.GetRequiredService<IElevatedProcessRunner>(),
                AppContext.BaseDirectory,
                provider.GetRequiredService<
                    ILogger<PawnIoHardwareAccessService>>()));

        services.AddSingleton<IMetricProvider, LibreHardwareMetricProvider>();
        services.AddSingleton<IMetricProvider, WindowsCpuMetricProvider>();
        services.AddSingleton<IMetricProvider, WindowsMemoryMetricProvider>();
        services.AddSingleton<IMetricProvider, NetworkMetricProvider>();
        services.AddSingleton<IForegroundProcessService,
            WindowsForegroundProcessService>();
        services.AddSingleton<IPresentMonSessionFactory>(provider =>
            new PresentMonSessionFactory(Path.Combine(
                AppContext.BaseDirectory,
                "Dependencies",
                "PresentMon.exe")));
        services.AddSingleton<ForegroundFpsTracker>();
        services.AddSingleton<IMetricProvider, PresentMonFpsProvider>();

        services.AddSingleton<MetricValueCache>();
        services.AddSingleton<MetricSamplingService>();
        services.AddSingleton<WindowInteractionService>();
        services.AddSingleton<OverlayViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SystemEventCoordinator>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<SettingsWindow>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private async Task RequestExitAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Log.Information("TopMonitor 正在退出");

        try
        {
            if (_services is not null)
            {
                _services.GetRequiredService<SystemEventCoordinator>().Dispose();
                await _services
                    .GetRequiredService<MetricSamplingService>()
                    .StopAsync(CancellationToken.None);
            }

            _trayIcon?.Dispose();
            if (_mainWindow is not null)
            {
                _mainWindow.ExitRequested -= OnMainWindowExitRequested;
                _mainWindow.AllowClose = true;
                _mainWindow.Close();
            }
            if (_settingsWindow is not null)
            {
                _settingsWindow.AllowClose = true;
                _settingsWindow.Close();
            }

            if (_services is not null)
            {
                await _services.DisposeAsync();
            }
        }
        finally
        {
            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            Log.Information("TopMonitor 已退出");
            Log.CloseAndFlush();
            Shutdown();
        }
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs eventArgs)
    {
        Log.Error(eventArgs.Exception, "发生未观察到的后台任务异常");
        eventArgs.SetObserved();
    }

    private async void OnMainWindowExitRequested(object? sender, EventArgs eventArgs) =>
        await RequestExitAsync();

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        Log.Error(eventArgs.Exception, "发生未处理的 UI 线程异常");
        eventArgs.Handled = true;
    }
}
