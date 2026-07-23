namespace TopMonitor.Domain.Configuration;

/// <summary>
/// 悬浮窗外观、定位和交互配置。
/// </summary>
public sealed record OverlayConfig(
    bool AlwaysOnTop,
    bool ClickThrough,
    bool Locked,
    double Opacity,
    double FontSize,
    string TextColor,
    double HorizontalSpacing,
    double CornerRadius,
    bool ShowMetricName,
    bool ShowUnit,
    OverlayAnchor Anchor,
    double OffsetX,
    double OffsetY);
