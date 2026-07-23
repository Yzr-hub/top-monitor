namespace TopMonitor.Domain.Configuration;

/// <summary>
/// 用户可选择的全局采样节奏。
/// </summary>
public enum RefreshMode
{
    Realtime,
    Balanced,
    PowerSaving
}
