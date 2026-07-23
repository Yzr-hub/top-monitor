using TopMonitor.Domain.Configuration;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.App.ViewModels;

public sealed class MetricSettingItemViewModel(
    WidgetConfig config,
    Action changed) : ObservableObject
{
    private bool _enabled = config.Enabled;
    private int _order = config.Order;
    private string _currentValue = "--";
    private string _status = "等待采样";

    public MetricId MetricId => config.MetricId;
    public string Label => config.Label;
    public string NumberFormat => config.NumberFormat;
    public string MetricName => config.MetricId.Value;
    public string CurrentValue
    {
        get => _currentValue;
        private set => SetProperty(ref _currentValue, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }
    public bool RequiresAdministrator => false;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value))
            {
                changed();
            }
        }
    }

    public int Order
    {
        get => _order;
        set
        {
            if (SetProperty(ref _order, value))
            {
                changed();
            }
        }
    }

    public WidgetConfig ToConfig() =>
        new(MetricId, Enabled, Order, Label, NumberFormat);

    public void Update(MetricValue value)
    {
        CurrentValue = value.Status == MetricStatus.Available && value.Value is { } number
            ? number.ToString(NumberFormat)
            : "--";
        Status = value.Status switch
        {
            MetricStatus.Available => "可用",
            MetricStatus.Restricted => "需要管理员权限",
            MetricStatus.Unavailable => "不可用",
            MetricStatus.Invalid => "无效",
            _ => "读取错误"
        };
    }
}
