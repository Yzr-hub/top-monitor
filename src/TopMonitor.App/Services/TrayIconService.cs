using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using TopMonitor.App.Commands;
using TopMonitor.App.ViewModels;
using TopMonitor.Application.Metrics;

namespace TopMonitor.App.Services;

/// <summary>
/// 系统托盘图标及菜单。窗口和采样服务通过显式回调协作。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _window;
    private readonly OverlayViewModel _viewModel;
    private readonly MetricSamplingService _samplingService;
    private readonly Func<Task> _exitAsync;
    private readonly Action _openSettings;
    private readonly ILogger<TrayIconService> _logger;
    private readonly TaskbarIcon _taskbarIcon;
    private readonly MenuItem _lockItem;
    private readonly MenuItem _clickThroughItem;
    private bool _disposed;

    public TrayIconService(
        MainWindow window,
        OverlayViewModel viewModel,
        MetricSamplingService samplingService,
        Action openSettings,
        Func<Task> exitAsync,
        ILogger<TrayIconService> logger)
    {
        _window = window;
        _viewModel = viewModel;
        _samplingService = samplingService;
        _openSettings = openSettings;
        _exitAsync = exitAsync;
        _logger = logger;

        _lockItem = CreateMenuItem(string.Empty, (_, _) => _viewModel.ToggleLockCommand.Execute(null));
        _clickThroughItem = CreateMenuItem(
            string.Empty,
            (_, _) => _viewModel.ToggleClickThroughCommand.Execute(null));
        var contextMenu = BuildContextMenu();
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "TopMonitor",
            IconSource = new GeneratedIconSource
            {
                Text = "TM",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(25, 30, 39)),
                FontSize = 42,
                FontWeight = FontWeights.Bold
            },
            ContextMenu = contextMenu,
            LeftClickCommand = new RelayCommand(_window.ToggleVisibility)
        };

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateToggleHeaders();
        _taskbarIcon.ForceCreate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _taskbarIcon.Dispose();
        _disposed = true;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("显示/隐藏悬浮窗", (_, _) => _window.ToggleVisibility()));
        menu.Items.Add(CreateMenuItem(
            "打开设置",
            (_, _) => _openSettings()));
        menu.Items.Add(new Separator());
        menu.Items.Add(_lockItem);
        menu.Items.Add(_clickThroughItem);
        menu.Items.Add(CreateMenuItem("刷新硬件设备", OnRefreshHardware));
        menu.Items.Add(CreateMenuItem("查看日志目录", OnOpenLogs));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(
            "关于",
            (_, _) => MessageBox.Show(
                "TopMonitor\n.NET 10 WPF 硬件监控悬浮窗",
                "关于 TopMonitor",
                MessageBoxButton.OK,
                MessageBoxImage.Information)));
        menu.Items.Add(CreateMenuItem("退出", OnExit));
        return menu;
    }

    private async void OnRefreshHardware(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            await _samplingService.RescanProvidersAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "从托盘刷新硬件设备失败");
        }
    }

    private void OnOpenLogs(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TopMonitor",
                "logs");
            Directory.CreateDirectory(logDirectory);
            Process.Start(new ProcessStartInfo(logDirectory) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "打开日志目录失败");
        }
    }

    private async void OnExit(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            await _exitAsync();
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "退出 TopMonitor 时发生错误");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(OverlayViewModel.IsLocked)
            or nameof(OverlayViewModel.ClickThrough))
        {
            UpdateToggleHeaders();
        }
    }

    private void UpdateToggleHeaders()
    {
        _lockItem.Header = _viewModel.IsLocked ? "解锁位置" : "锁定位置";
        _clickThroughItem.Header =
            _viewModel.ClickThrough ? "关闭鼠标穿透" : "开启鼠标穿透";
    }

    private static MenuItem CreateMenuItem(
        string header,
        RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        return item;
    }
}
