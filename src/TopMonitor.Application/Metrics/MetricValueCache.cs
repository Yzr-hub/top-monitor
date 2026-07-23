using System.Collections.Concurrent;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Application.Metrics;

/// <summary>
/// 保存最新指标值的线程安全缓存。
/// </summary>
public sealed class MetricValueCache
{
    private readonly ConcurrentDictionary<MetricId, MetricValue> _values = [];

    public bool Update(MetricValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        while (true)
        {
            if (!_values.TryGetValue(value.Id, out var existing))
            {
                if (_values.TryAdd(value.Id, value))
                {
                    return true;
                }

                continue;
            }

            var changed = !HasSameObservableValue(existing, value);
            if (_values.TryUpdate(value.Id, value, existing))
            {
                return changed;
            }
        }
    }

    public bool TryGet(MetricId id, out MetricValue value) =>
        _values.TryGetValue(id, out value!);

    public IReadOnlyDictionary<MetricId, MetricValue> Snapshot() =>
        new Dictionary<MetricId, MetricValue>(_values);

    private static bool HasSameObservableValue(MetricValue first, MetricValue second) =>
        HasEquivalentNumber(first.Id, first.Value, second.Value) &&
        first.Status == second.Status &&
        string.Equals(first.ErrorMessage, second.ErrorMessage, StringComparison.Ordinal);

    private static bool HasEquivalentNumber(MetricId id, double? first, double? second)
    {
        if (first is null || second is null)
        {
            return first == second;
        }

        var tolerance = id.Value switch
        {
            var value when value.Contains(".temperature.", StringComparison.Ordinal) => 0.1,
            var value when value.Contains(".load.", StringComparison.Ordinal) => 0.1,
            "system.memory.usage.percent" => 0.1,
            "system.memory.used.bytes" => 1024d * 1024d,
            var value when value.Contains(
                "bytes_per_second",
                StringComparison.Ordinal) => 1024d,
            _ => 0d
        };
        return Math.Abs(first.Value - second.Value) < tolerance ||
               first.Value.Equals(second.Value);
    }
}
