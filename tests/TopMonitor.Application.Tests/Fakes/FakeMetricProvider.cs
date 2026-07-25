using TopMonitor.Application.Metrics;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Application.Tests.Fakes;

internal sealed class FakeMetricProvider(
    IReadOnlyList<MetricDefinition> definitions) : IMetricProvider
{
    private int _readCount;

    public string ProviderId => "fake";

    public IReadOnlyList<MetricId> LastRequestedMetricIds { get; private set; } = [];

    public List<IReadOnlyCollection<MetricId>> Requests { get; } = [];

    public int ReadCount => Volatile.Read(ref _readCount);

    public Exception? ReadException { get; init; }

    public Func<MetricId, double> ValueFactory { get; init; } = _ => 42;

    public Task<IReadOnlyList<MetricDefinition>> DiscoverAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(definitions);
    }

    public Task<IReadOnlyDictionary<MetricId, MetricValue>> ReadAsync(
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _readCount);
        Requests.Add(metricIds);
        LastRequestedMetricIds = metricIds.ToArray();
        if (ReadException is not null)
        {
            throw ReadException;
        }

        IReadOnlyDictionary<MetricId, MetricValue> values = metricIds.ToDictionary(
            metricId => metricId,
            metricId => MetricValue.Create(
                metricId,
                ValueFactory(metricId),
                DateTimeOffset.UtcNow));
        return Task.FromResult(values);
    }

    public Task RescanAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
