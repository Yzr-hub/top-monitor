using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TopMonitor.App.Commands;
using TopMonitor.Application.Configuration;
using TopMonitor.Application.Displays;
using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Configuration;

namespace TopMonitor.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly OverlayViewModel _overlay;
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly SynchronizationContext _synchronizationContext;
    private readonly MetricSamplingService _samplingService;
    private readonly SemaphoreSlim _applyGate = new(1, 1);
    private int _applyRevision;
    private RefreshMode _refreshMode;
    private double _fontSize;
    private double _opacity;
    private double _horizontalSpacing;
    private double _cornerRadius;
    private string _textColor;
    private bool _showMetricName;
    private bool _showUnit;
    private bool _alwaysOnTop;
    private bool _clickThrough;
    private bool _locked;
    private bool _autoStart;
    private bool _minimizeOnStartup;
    private CloseBehavior _closeBehavior;
    private string? _selectedDisplayId;
    private OverlayAnchor _anchor;
    private double _offsetX;
    private double _offsetY;

    public SettingsViewModel(
        AppSettings settings,
        OverlayViewModel overlay,
        ISettingsService settingsService,
        IStartupService startupService,
        IDisplayService displayService,
        MetricSamplingService samplingService,
        ILogger<SettingsViewModel> logger)
    {
        _overlay = overlay;
        _settingsService = settingsService;
        _startupService = startupService;
        _logger = logger;
        _samplingService = samplingService;
        _synchronizationContext =
            SynchronizationContext.Current ?? new SynchronizationContext();
        _refreshMode = settings.RefreshMode;
        _fontSize = settings.Overlay.FontSize;
        _opacity = settings.Overlay.Opacity;
        _horizontalSpacing = settings.Overlay.HorizontalSpacing;
        _cornerRadius = settings.Overlay.CornerRadius;
        _textColor = settings.Overlay.TextColor;
        _showMetricName = settings.Overlay.ShowMetricName;
        _showUnit = settings.Overlay.ShowUnit;
        _alwaysOnTop = settings.Overlay.AlwaysOnTop;
        _clickThrough = settings.Overlay.ClickThrough;
        _locked = settings.Overlay.Locked;
        _autoStart = settings.AutoStart;
        _minimizeOnStartup = settings.MinimizeOnStartup;
        _closeBehavior = settings.CloseBehavior;
        _selectedDisplayId = settings.DisplayId;
        _anchor = settings.Overlay.Anchor;
        _offsetX = settings.Overlay.OffsetX;
        _offsetY = settings.Overlay.OffsetY;

        Widgets = new ObservableCollection<MetricSettingItemViewModel>(
            settings.Widgets
                .OrderBy(widget => widget.Order)
                .Select(widget => new MetricSettingItemViewModel(widget, QueueApply)));
        Displays = displayService.GetDisplays();
        MoveUpCommand = new RelayCommand<MetricSettingItemViewModel>(item => Move(item, -1));
        MoveDownCommand = new RelayCommand<MetricSettingItemViewModel>(item => Move(item, 1));
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        SaveCommand = new RelayCommand(QueueApply);
        samplingService.ValuesChanged += OnMetricValuesChanged;
    }

    public IReadOnlyList<RefreshMode> RefreshModes { get; } =
        Enum.GetValues<RefreshMode>();

    public IReadOnlyList<OverlayAnchor> Anchors { get; } =
        Enum.GetValues<OverlayAnchor>();
    public IReadOnlyList<CloseBehavior> CloseBehaviors { get; } =
        Enum.GetValues<CloseBehavior>();

    public IReadOnlyList<DisplayInfo> Displays { get; }

    public ObservableCollection<MetricSettingItemViewModel> Widgets { get; }

    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand RestoreDefaultsCommand { get; }
    public ICommand SaveCommand { get; }

    public RefreshMode RefreshMode { get => _refreshMode; set => SetAndApply(ref _refreshMode, value); }
    public double FontSize { get => _fontSize; set => SetAndApply(ref _fontSize, value); }
    public double Opacity { get => _opacity; set => SetAndApply(ref _opacity, value); }
    public double HorizontalSpacing { get => _horizontalSpacing; set => SetAndApply(ref _horizontalSpacing, value); }
    public double CornerRadius { get => _cornerRadius; set => SetAndApply(ref _cornerRadius, value); }
    public string TextColor { get => _textColor; set => SetAndApply(ref _textColor, value); }
    public bool ShowMetricName { get => _showMetricName; set => SetAndApply(ref _showMetricName, value); }
    public bool ShowUnit { get => _showUnit; set => SetAndApply(ref _showUnit, value); }
    public bool AlwaysOnTop { get => _alwaysOnTop; set => SetAndApply(ref _alwaysOnTop, value); }
    public bool ClickThrough { get => _clickThrough; set => SetAndApply(ref _clickThrough, value); }
    public bool Locked { get => _locked; set => SetAndApply(ref _locked, value); }
    public bool AutoStart { get => _autoStart; set => SetAndApply(ref _autoStart, value); }
    public bool MinimizeOnStartup { get => _minimizeOnStartup; set => SetAndApply(ref _minimizeOnStartup, value); }
    public CloseBehavior CloseBehavior { get => _closeBehavior; set => SetAndApply(ref _closeBehavior, value); }
    public string? SelectedDisplayId { get => _selectedDisplayId; set => SetAndApply(ref _selectedDisplayId, value); }
    public OverlayAnchor Anchor { get => _anchor; set => SetAndApply(ref _anchor, value); }
    public double OffsetX { get => _offsetX; set => SetAndApply(ref _offsetX, value); }
    public double OffsetY { get => _offsetY; set => SetAndApply(ref _offsetY, value); }

    public void Dispose()
    {
        _samplingService.ValuesChanged -= OnMetricValuesChanged;
        _applyGate.Dispose();
    }

    private void SetAndApply<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            QueueApply();
        }
    }

    private void OnMetricValuesChanged(object? sender, MetricValuesChangedEventArgs eventArgs)
    {
        _synchronizationContext.Post(
            _ =>
            {
                foreach (var value in eventArgs.Values)
                {
                    Widgets.FirstOrDefault(item => item.MetricId == value.Id)?.Update(value);
                }
            },
            null);
    }

    private void Move(MetricSettingItemViewModel? item, int delta)
    {
        if (item is null)
        {
            return;
        }

        var oldIndex = Widgets.IndexOf(item);
        var newIndex = Math.Clamp(oldIndex + delta, 0, Widgets.Count - 1);
        if (oldIndex == newIndex)
        {
            return;
        }

        Widgets.Move(oldIndex, newIndex);
        for (var index = 0; index < Widgets.Count; index++)
        {
            Widgets[index].Order = (index + 1) * 10;
        }
        QueueApply();
    }

    private void RestoreDefaults()
    {
        var defaults = AppSettings.CreateDefault();
        Widgets.Clear();
        foreach (var widget in defaults.Widgets)
        {
            Widgets.Add(new MetricSettingItemViewModel(widget, QueueApply));
        }
        RefreshMode = defaults.RefreshMode;
        FontSize = defaults.Overlay.FontSize;
        Opacity = defaults.Overlay.Opacity;
        QueueApply();
    }

    private async void QueueApply()
    {
        var revision = Interlocked.Increment(ref _applyRevision);
        var gateAcquired = false;
        try
        {
            await Task.Delay(150);
            if (revision != Volatile.Read(ref _applyRevision))
            {
                return;
            }

            await _applyGate.WaitAsync();
            gateAcquired = true;
            if (revision != Volatile.Read(ref _applyRevision))
            {
                return;
            }

            var current = _overlay.CurrentSettings;
            var settings = current with
            {
                RefreshMode = RefreshMode,
                DisplayId = SelectedDisplayId,
                AutoStart = AutoStart,
                MinimizeOnStartup = MinimizeOnStartup,
                CloseBehavior = CloseBehavior,
                Widgets = Widgets.Select(widget => widget.ToConfig()).ToArray(),
                Overlay = current.Overlay with
                {
                    AlwaysOnTop = AlwaysOnTop,
                    ClickThrough = ClickThrough,
                    Locked = Locked,
                    Opacity = Math.Clamp(Opacity, 0.2, 1),
                    FontSize = Math.Clamp(FontSize, 10, 36),
                    HorizontalSpacing = Math.Clamp(HorizontalSpacing, 0, 40),
                    CornerRadius = Math.Clamp(CornerRadius, 0, 30),
                    TextColor = TextColor,
                    ShowMetricName = ShowMetricName,
                    ShowUnit = ShowUnit,
                    Anchor = Anchor,
                    OffsetX = OffsetX,
                    OffsetY = OffsetY
                }
            };
            _startupService.SetEnabled(settings.AutoStart);
            await _overlay.ApplySettingsAsync(settings, CancellationToken.None);
            await _settingsService.SaveAsync(settings, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "即时应用设置失败");
        }
        finally
        {
            if (gateAcquired)
            {
                _applyGate.Release();
            }
        }
    }
}
