namespace TopMonitor.Domain.Metrics;

/// <summary>
/// 第一版内置指标的稳定标识。
/// </summary>
public static class MetricIds
{
    public static readonly MetricId CpuTemperaturePackage = new("hardware.cpu.temperature.package");
    public static readonly MetricId CpuTotalLoad = new("hardware.cpu.load.total");
    public static readonly MetricId CpuAverageClock = new("hardware.cpu.clock.average");
    public static readonly MetricId CpuPackagePower = new("hardware.cpu.power.package");

    public static readonly MetricId Gpu0CoreTemperature = new("hardware.gpu.0.temperature.core");
    public static readonly MetricId Gpu0CoreLoad = new("hardware.gpu.0.load.core");
    public static readonly MetricId Gpu0MemoryUsed = new("hardware.gpu.0.memory.used");
    public static readonly MetricId Gpu0TotalPower = new("hardware.gpu.0.power.total");

    public static readonly MetricId MemoryUsagePercent = new("system.memory.usage.percent");
    public static readonly MetricId MemoryUsedBytes = new("system.memory.used.bytes");

    public static readonly MetricId ActiveNetworkDownload =
        new("system.network.active.download.bytes_per_second");

    public static readonly MetricId ActiveNetworkUpload =
        new("system.network.active.upload.bytes_per_second");

    public static readonly MetricId ActiveDiskRead =
        new("system.disk.active.read.bytes_per_second");

    public static readonly MetricId ActiveDiskWrite =
        new("system.disk.active.write.bytes_per_second");
}
