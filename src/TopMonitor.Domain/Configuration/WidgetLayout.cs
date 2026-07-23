namespace TopMonitor.Domain.Configuration;

/// <summary>
/// 指标布局的纯领域规则。
/// </summary>
public static class WidgetLayout
{
    public static IReadOnlyList<WidgetConfig> OrderEnabled(IEnumerable<WidgetConfig> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);

        return widgets
            .Where(widget => widget.Enabled)
            .OrderBy(widget => widget.Order)
            .ThenBy(widget => widget.MetricId.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
