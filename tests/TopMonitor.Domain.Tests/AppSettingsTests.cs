using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Default_settings_have_balanced_refresh_and_expected_overlay_defaults()
    {
        var settings = AppSettings.CreateDefault();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(RefreshMode.Balanced, settings.RefreshMode);
        Assert.True(settings.Overlay.AlwaysOnTop);
        Assert.False(settings.Overlay.ClickThrough);
        Assert.False(settings.Overlay.Locked);
        Assert.Equal(0.75, settings.Overlay.Opacity);
        Assert.Equal(14, settings.Overlay.FontSize);
        Assert.Equal(OverlayAnchor.TopCenter, settings.Overlay.Anchor);
        Assert.Equal(8, settings.Overlay.OffsetY);
        Assert.Contains(
            settings.Widgets,
            widget => widget.MetricId == MetricIds.CpuTotalLoad && widget.Enabled);
    }

    [Fact]
    public void Default_settings_return_independent_widget_collections()
    {
        var first = AppSettings.CreateDefault();
        var second = AppSettings.CreateDefault();

        Assert.NotSame(first.Widgets, second.Widgets);
    }
}
