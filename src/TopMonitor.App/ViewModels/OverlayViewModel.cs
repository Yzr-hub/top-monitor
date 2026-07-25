using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TopMonitor.App.Commands;
using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.App.ViewModels;

public sealed class OverlayViewModel : ObservableObject, IDisposable
{
    private readonly MetricSamplingService _samplingService;
    private AppSettings _settings;
    private readonly SynchronizationContext _synchronizationContext;
    private Dictionary<MetricId, MetricItemViewModel> _itemsById;
    private bool _alwaysOnTop;
    private bool _clickThrough;
    private bool _isLocked;
    private bool _disposed;

    public OverlayViewModel(MetricSamplingService samplingService, AppSettings settings)
    {
        _samplingService = samplingService;
        _settings = settings;
        _synchronizationContext =
            SynchronizationContext.Current ?? new SynchronizationContext();
        _alwaysOnTop = settings.Overlay.AlwaysOnTop;
        _clickThrough = settings.Overlay.ClickThrough;
        _isLocked = settings.Overlay.Locked;

        var items = CreateItems(settings);
        Metrics = new ObservableCollection<MetricItemViewModel>(items);
        _itemsById = items.ToDictionary(item => item.MetricId);
        ToggleLockCommand = new RelayCommand(ToggleLock);
        ToggleClickThroughCommand = new RelayCommand(
            () => ClickThrough = !ClickThrough);
        ToggleAlwaysOnTopCommand = new RelayCommand(
            () => AlwaysOnTop = !AlwaysOnTop);

        _samplingService.ValuesChanged += OnValuesChanged;
    }

    public ObservableCollection<MetricItemViewModel> Metrics { get; }

    public event EventHandler? PlacementChanged;

    public AppSettings CurrentSettings => _settings;

    public ICommand ToggleLockCommand { get; }

    public ICommand ToggleClickThroughCommand { get; }

    public ICommand ToggleAlwaysOnTopCommand { get; }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => SetProperty(ref _alwaysOnTop, value);
    }

    public bool ClickThrough
    {
        get => _clickThrough;
        set => SetProperty(ref _clickThrough, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        private set => SetProperty(ref _isLocked, value);
    }

    public double FontSize => _settings.Overlay.FontSize;

    public double Opacity => _settings.Overlay.Opacity;

    public double HorizontalSpacing => _settings.Overlay.HorizontalSpacing;

    public double CornerRadius => _settings.Overlay.CornerRadius;

    public string TextColor => _settings.Overlay.TextColor;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var definitions = CreateDefaultDefinitions();
        var subscriptions = Metrics.Select(item =>
            new MetricSubscription(
                item.MetricId,
                AdjustInterval(
                    definitions[item.MetricId].RecommendedInterval,
                    _settings.RefreshMode)));
        await _samplingService.UpdateSubscriptionsAsync(subscriptions, cancellationToken);
    }

    public async Task ApplySettingsAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        _settings = settings;
        AlwaysOnTop = settings.Overlay.AlwaysOnTop;
        ClickThrough = settings.Overlay.ClickThrough;
        IsLocked = settings.Overlay.Locked;

        var items = CreateItems(settings);
        Metrics.Clear();
        foreach (var item in items)
        {
            Metrics.Add(item);
        }

        _itemsById = items.ToDictionary(item => item.MetricId);
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(HorizontalSpacing));
        OnPropertyChanged(nameof(CornerRadius));
        OnPropertyChanged(nameof(TextColor));
        PlacementChanged?.Invoke(this, EventArgs.Empty);
        await InitializeAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _samplingService.ValuesChanged -= OnValuesChanged;
        _disposed = true;
    }

    private void ToggleLock()
    {
        IsLocked = !IsLocked;
        ClickThrough = IsLocked;
    }

    private void OnValuesChanged(object? sender, MetricValuesChangedEventArgs eventArgs)
    {
        _synchronizationContext.Post(
            _ =>
            {
                foreach (var value in eventArgs.Values)
                {
                    if (_itemsById.TryGetValue(value.Id, out var item))
                    {
                        item.Update(value);
                    }
                }
            },
            null);
    }

    private static TimeSpan AdjustInterval(TimeSpan recommended, RefreshMode refreshMode)
    {
        var minimum = refreshMode switch
        {
            RefreshMode.Realtime => TimeSpan.FromMilliseconds(500),
            RefreshMode.Balanced => TimeSpan.FromSeconds(1),
            RefreshMode.PowerSaving => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(1)
        };
        return recommended > minimum ? recommended : minimum;
    }

    private static IReadOnlyDictionary<MetricId, MetricDefinition> CreateDefaultDefinitions()
    {
        var definitions = new[]
        {
            new MetricDefinition(
                MetricIds.CpuTemperaturePackage,
                "CPU 温度",
                MetricCategory.Temperature,
                "°C",
                TimeSpan.FromSeconds(1),
                false),
            new MetricDefinition(
                MetricIds.CpuTotalLoad,
                "CPU 利用率",
                MetricCategory.Utilization,
                "%",
                TimeSpan.FromMilliseconds(500),
                false),
            new MetricDefinition(
                MetricIds.Gpu0CoreTemperature,
                "GPU 温度",
                MetricCategory.Temperature,
                "°C",
                TimeSpan.FromSeconds(1),
                false),
            new MetricDefinition(
                MetricIds.Gpu0CoreLoad,
                "GPU 利用率",
                MetricCategory.Utilization,
                "%",
                TimeSpan.FromMilliseconds(500),
                false),
            new MetricDefinition(
                MetricIds.ForegroundFps,
                "前台游戏帧率",
                MetricCategory.Other,
                string.Empty,
                TimeSpan.FromMilliseconds(500),
                false),
            new MetricDefinition(
                MetricIds.MemoryUsagePercent,
                "内存使用率",
                MetricCategory.Memory,
                "%",
                TimeSpan.FromSeconds(1),
                false),
            new MetricDefinition(
                MetricIds.MemoryUsedBytes,
                "内存已使用",
                MetricCategory.Memory,
                "B",
                TimeSpan.FromSeconds(1),
                false),
            new MetricDefinition(
                MetricIds.ActiveNetworkDownload,
                "下载速度",
                MetricCategory.Network,
                "B/s",
                TimeSpan.FromSeconds(1),
                false),
            new MetricDefinition(
                MetricIds.ActiveNetworkUpload,
                "上传速度",
                MetricCategory.Network,
                "B/s",
                TimeSpan.FromSeconds(1),
                false)
        };
        return definitions.ToDictionary(definition => definition.Id);
    }

    private static MetricItemViewModel[] CreateItems(AppSettings settings)
    {
        var definitions = CreateDefaultDefinitions();
        return WidgetLayout.OrderEnabled(settings.Widgets)
            .Where(widget => definitions.ContainsKey(widget.MetricId))
            .Select(widget => new MetricItemViewModel(
                definitions[widget.MetricId],
                widget,
                settings.Overlay.ShowMetricName,
                settings.Overlay.ShowUnit))
            .ToArray();
    }
}
