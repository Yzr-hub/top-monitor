using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using TopMonitor.App.ViewModels;
using TopMonitor.Application.Displays;
using TopMonitor.Domain.Configuration;
using TopMonitor.Infrastructure.Windows;

namespace TopMonitor.App;

/// <summary>
/// 顶部硬件监控悬浮窗。业务状态由 <see cref="OverlayViewModel"/> 提供。
/// </summary>
public partial class MainWindow : Window
{
    private readonly OverlayViewModel _viewModel;
    private readonly WindowInteractionService _windowInteraction;
    private readonly IDisplayService _displayService;
    private readonly ILogger<MainWindow> _logger;
    private nint _windowHandle;
    private bool _repositionPending;

    public MainWindow(
        OverlayViewModel viewModel,
        WindowInteractionService windowInteraction,
        IDisplayService displayService,
        ILogger<MainWindow> logger)
    {
        _viewModel = viewModel;
        _windowInteraction = windowInteraction;
        _displayService = displayService;
        _logger = logger;
        DataContext = viewModel;
        InitializeComponent();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Closing += OnClosing;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.PlacementChanged += OnPlacementChanged;
    }

    public bool AllowClose { get; set; }

    public event EventHandler? ExitRequested;

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            SchedulePositionAtConfiguredAnchor();
        }
    }

    public void RepositionToConfiguredDisplay() =>
        SchedulePositionAtConfiguredAnchor();

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        ApplyWindowStyles();
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs) =>
        SchedulePositionAtConfiguredAnchor();

    private void OnSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        if (_viewModel.CurrentSettings.Overlay.Anchor != OverlayAnchor.Custom)
        {
            SchedulePositionAtConfiguredAnchor();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!_viewModel.IsLocked &&
            !_viewModel.ClickThrough &&
            eventArgs.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(OverlayViewModel.ClickThrough))
        {
            ApplyWindowStyles();
        }
    }

    private void ApplyWindowStyles()
    {
        try
        {
            _windowInteraction.ApplyOverlayStyles(
                _windowHandle,
                _viewModel.ClickThrough);
        }
        catch (Win32Exception exception)
        {
            _logger.LogWarning(
                exception,
                "无法更新悬浮窗鼠标穿透样式，窗口将保持可交互");
        }
    }

    private void SchedulePositionAtConfiguredAnchor()
    {
        if (_repositionPending)
        {
            return;
        }

        _repositionPending = true;
        _ = Dispatcher.BeginInvoke(
            new Action(() =>
            {
                _repositionPending = false;
                PositionAtConfiguredAnchor();
            }),
            DispatcherPriority.Loaded);
    }

    private void PositionAtConfiguredAnchor()
    {
        var settings = _viewModel.CurrentSettings;
        var display = _displayService.GetDisplays()
            .FirstOrDefault(item => item.Id == settings.DisplayId)
            ?? _displayService.GetDisplays().FirstOrDefault(item => item.IsPrimary);
        var left = display?.Left ?? SystemParameters.WorkArea.Left;
        var top = display?.Top ?? SystemParameters.WorkArea.Top;
        var width = display?.Width ?? SystemParameters.WorkArea.Width;
        var height = display?.Height ?? SystemParameters.WorkArea.Height;
        var desiredLeft = OverlayPlacement.CalculateLeft(
            settings.Overlay.Anchor,
            left,
            width,
            ActualWidth,
            settings.Overlay.OffsetX);
        var desiredTop = top + settings.Overlay.OffsetY;
        Left = Math.Clamp(
            desiredLeft,
            left,
            left + Math.Max(0, width - ActualWidth));
        Top = Math.Clamp(
            desiredTop,
            top,
            top + Math.Max(0, height - ActualHeight));
    }

    private void OnPlacementChanged(object? sender, EventArgs eventArgs) =>
        SchedulePositionAtConfiguredAnchor();

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (AllowClose)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.PlacementChanged -= OnPlacementChanged;
            SizeChanged -= OnSizeChanged;
            return;
        }

        if (_viewModel.CurrentSettings.CloseBehavior == CloseBehavior.Exit)
        {
            eventArgs.Cancel = true;
            ExitRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        eventArgs.Cancel = true;
        Hide();
    }
}
