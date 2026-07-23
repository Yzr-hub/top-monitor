using System.Globalization;
using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Formatting;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.App.ViewModels;

public sealed class MetricItemViewModel(
    MetricDefinition definition,
    WidgetConfig widget,
    bool showLabel,
    bool showUnit) : ObservableObject
{
    private string _displayText = $"{widget.Label} --";

    public MetricId MetricId => widget.MetricId;

    public int Order => widget.Order;

    public string DisplayText
    {
        get => _displayText;
        private set => SetProperty(ref _displayText, value);
    }

    public void Update(MetricValue value)
    {
        DisplayText = MetricFormatter.Format(
            definition,
            widget,
            value,
            showLabel,
            showUnit,
            CultureInfo.CurrentCulture);
    }
}
