using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Configuration;

/// <summary>
/// TopMonitor 的强类型用户配置根对象。
/// </summary>
public sealed record AppSettings(
    int SchemaVersion,
    RefreshMode RefreshMode,
    OverlayConfig Overlay,
    IReadOnlyList<WidgetConfig> Widgets,
    string? DisplayId,
    bool AutoStart,
    bool MinimizeOnStartup,
    CloseBehavior CloseBehavior)
{
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// 每次返回独立的默认配置，避免调用方意外共享可变集合。
    /// </summary>
    public static AppSettings CreateDefault() =>
        new(
            CurrentSchemaVersion,
            RefreshMode.Balanced,
            new OverlayConfig(
                AlwaysOnTop: true,
                ClickThrough: false,
                Locked: false,
                Opacity: 0.75,
                FontSize: 14,
                TextColor: "#FFFFFFFF",
                HorizontalSpacing: 12,
                CornerRadius: 8,
                ShowMetricName: true,
                ShowUnit: true,
                Anchor: OverlayAnchor.TopCenter,
                OffsetX: 0,
                OffsetY: 8),
            CreateDefaultWidgets(),
            DisplayId: null,
            AutoStart: false,
            MinimizeOnStartup: false,
            CloseBehavior.MinimizeToTray);

    private static IReadOnlyList<WidgetConfig> CreateDefaultWidgets() =>
        new[]
        {
            new WidgetConfig(MetricIds.CpuTemperaturePackage, true, 10, "CPU", "0"),
            new WidgetConfig(MetricIds.CpuTotalLoad, true, 20, "CPU", "0"),
            new WidgetConfig(MetricIds.Gpu0CoreTemperature, true, 30, "GPU", "0"),
            new WidgetConfig(MetricIds.Gpu0CoreLoad, true, 40, "GPU", "0"),
            new WidgetConfig(MetricIds.MemoryUsagePercent, true, 50, "RAM", "0"),
            new WidgetConfig(MetricIds.MemoryUsedBytes, false, 60, "RAM", "0.0"),
            new WidgetConfig(MetricIds.ActiveNetworkDownload, false, 70, "↓", "0.0"),
            new WidgetConfig(MetricIds.ActiveNetworkUpload, false, 80, "↑", "0.0"),
            new WidgetConfig(MetricIds.ForegroundFps, false, 90, "FPS", "0")
        };
}
