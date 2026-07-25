using LibreHardwareMonitor.Hardware;
using Serilog;
using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Infrastructure.Hardware;

/// <summary>
/// LibreHardwareMonitor 的 CPU/GPU 温度和 GPU 利用率适配器。
/// </summary>
public sealed class LibreHardwareMetricProvider(ILogger logger) : IMetricProvider, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<MetricId, SensorBinding> _bindings = [];
    private readonly Dictionary<MetricId, MetricDefinition> _definitions = [];
    private readonly HardwareUpdateLimiter _updateLimiter =
        new(TimeSpan.FromMilliseconds(400));
    private Computer? _computer;
    private DateTimeOffset _nextReadErrorLog;
    private bool _disposed;

    public string ProviderId => "libre-hardware";

    public async Task<IReadOnlyList<MetricDefinition>> DiscoverAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            DiscoverCore();
            return _definitions.Values.OrderBy(definition => definition.Id.Value).ToArray();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Warning(exception, "LibreHardwareMonitor 硬件发现失败，温度指标将降级为不可用");
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<MetricId, MetricValue>> ReadAsync(
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            if (_computer is null)
            {
                DiscoverCore();
            }

            var requestedIds = metricIds
                .Where(_definitions.ContainsKey)
                .ToArray();
            if (requestedIds.Length == 0)
            {
                return new Dictionary<MetricId, MetricValue>();
            }

            var requestedBindings = requestedIds
                .Where(_bindings.ContainsKey)
                .Select(id => _bindings[id])
                .ToArray();
            foreach (var hardware in requestedBindings
                         .Select(binding => binding.Hardware)
                         .Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                UpdateHardwareIfDue(hardware);
            }

            var timestamp = DateTimeOffset.UtcNow;
            return requestedIds.ToDictionary(
                id => id,
                id => _bindings.TryGetValue(id, out var binding) &&
                      binding.Sensor.Value is { } value
                    ? MetricValue.Create(id, value, timestamp)
                    : MetricValue.Unavailable(
                        id,
                        timestamp,
                        "LibreHardwareMonitor 未提供可用的传感器读数。"));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (DateTimeOffset.UtcNow >= _nextReadErrorLog)
            {
                logger.Warning(exception, "LibreHardwareMonitor 传感器读取失败");
                _nextReadErrorLog = DateTimeOffset.UtcNow.AddMinutes(1);
            }

            var timestamp = DateTimeOffset.UtcNow;
            return metricIds
                .Where(_definitions.ContainsKey)
                .ToDictionary(
                    id => id,
                    id => MetricValue.Failed(id, timestamp, exception.Message));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RescanAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            CloseComputer();
            DiscoverCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gate.Wait();
        try
        {
            CloseComputer();
            _disposed = true;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private void DiscoverCore()
    {
        CloseComputer();
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true
        };
        try
        {
            computer.Open();
        }
        catch
        {
            computer.Close();
            throw;
        }

        _computer = computer;
        _bindings.Clear();
        _definitions.Clear();

        var hardware = Flatten(computer.Hardware).ToArray();
        DiscoverCpu(hardware);
        DiscoverGpus(hardware);
        logger.Information(
            "LibreHardwareMonitor 发现 {HardwareCount} 个硬件节点和 {MetricCount} 个指标",
            hardware.Length,
            _definitions.Count);
    }

    private void DiscoverCpu(IReadOnlyCollection<IHardware> hardware)
    {
        var cpu = hardware
            .Where(item => item.HardwareType == HardwareType.Cpu)
            .OrderBy(item => item.Identifier.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();
        if (cpu is null)
        {
            return;
        }

        cpu.Update();
        var temperatureSensors = cpu.Sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .OrderBy(sensor => sensor.Index)
            .ToArray();
        var candidates = temperatureSensors
            .Select(sensor => new CpuTemperatureCandidate(
                sensor.Identifier.ToString(),
                sensor.Name,
                sensor.Value))
            .ToArray();
        var selection = CpuTemperatureSelector.Select(candidates);
        var temperature = selection is null
            ? null
            : temperatureSensors.FirstOrDefault(sensor =>
                string.Equals(
                    sensor.Identifier.ToString(),
                    selection.Candidate.Id,
                    StringComparison.Ordinal));
        AddDefinition(
            MetricIds.CpuTemperaturePackage,
            "CPU 温度",
            MetricCategory.Temperature,
            "°C",
            TimeSpan.FromSeconds(1));
        if (temperature is not null)
        {
            _bindings[MetricIds.CpuTemperaturePackage] =
                new SensorBinding(MetricIds.CpuTemperaturePackage, cpu, temperature);
        }

        logger.Information(
            "CPU temperature discovery for {HardwareName}: candidates {@Candidates}; selected {SelectedSensor} ({SelectionReason})",
            cpu.Name,
            candidates,
            selection?.Candidate.Name,
            selection?.Reason);
    }

    private void DiscoverGpus(IReadOnlyCollection<IHardware> hardware)
    {
        var gpus = hardware
            .Where(item => item.HardwareType is HardwareType.GpuAmd
                or HardwareType.GpuIntel
                or HardwareType.GpuNvidia)
            .OrderBy(item => item.Identifier.ToString(), StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < gpus.Length; index++)
        {
            var gpu = gpus[index];
            gpu.Update();
            var temperatureId = new MetricId($"hardware.gpu.{index}.temperature.core");
            var loadId = new MetricId($"hardware.gpu.{index}.load.core");
            var temperature = SelectSensor(gpu.Sensors, SensorType.Temperature, "Core");
            var load = SelectSensor(gpu.Sensors, SensorType.Load, "Core");
            AddDefinition(
                temperatureId,
                $"GPU {index + 1} 温度",
                MetricCategory.Temperature,
                "°C",
                TimeSpan.FromSeconds(1));
            AddDefinition(
                loadId,
                $"GPU {index + 1} 利用率",
                MetricCategory.Utilization,
                "%",
                TimeSpan.FromMilliseconds(500));

            if (temperature is not null)
            {
                _bindings[temperatureId] =
                    new SensorBinding(temperatureId, gpu, temperature);
            }

            if (load is not null)
            {
                _bindings[loadId] = new SensorBinding(loadId, gpu, load);
            }
        }
    }

    private void AddDefinition(
        MetricId id,
        string displayName,
        MetricCategory category,
        string unit,
        TimeSpan interval)
    {
        _definitions[id] = new MetricDefinition(
            id,
            displayName,
            category,
            unit,
            interval,
            RequiresAdministrator: false);
    }

    private static ISensor? SelectSensor(
        IEnumerable<ISensor> sensors,
        SensorType sensorType,
        string preferredName)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == sensorType)
            .OrderBy(sensor => sensor.Index)
            .ToArray();
        return candidates.FirstOrDefault(sensor =>
                   sensor.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase))
               ?? candidates.FirstOrDefault();
    }

    private void UpdateHardwareIfDue(IHardware hardware)
    {
        if (_updateLimiter.ShouldUpdate(
                hardware.Identifier.ToString(),
                DateTimeOffset.UtcNow))
        {
            hardware.Update();
        }
    }

    private static IEnumerable<IHardware> Flatten(IEnumerable<IHardware> hardware)
    {
        foreach (var item in hardware)
        {
            yield return item;
            foreach (var child in Flatten(item.SubHardware))
            {
                yield return child;
            }
        }
    }

    private void CloseComputer()
    {
        _computer?.Close();
        _computer = null;
        _bindings.Clear();
        _definitions.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed record SensorBinding(
        MetricId MetricId,
        IHardware Hardware,
        ISensor Sensor);
}
