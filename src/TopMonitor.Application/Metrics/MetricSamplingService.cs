using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TopMonitor.Domain.Metrics;

namespace TopMonitor.Application.Metrics;

/// <summary>
/// 按采样周期分组运行 Provider，并把变化写入线程安全缓存。
/// </summary>
public sealed class MetricSamplingService : IAsyncDisposable
{
    private static readonly TimeSpan ErrorLogInterval = TimeSpan.FromMinutes(1);

    private readonly IReadOnlyList<IMetricProvider> _providers;
    private readonly MetricValueCache _cache;
    private readonly ILogger<MetricSamplingService> _logger;
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private readonly Dictionary<IMetricProvider, SemaphoreSlim> _providerGates;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nextErrorLogs =
        new(StringComparer.Ordinal);
    private readonly Dictionary<MetricId, IMetricProvider> _owners = [];

    private IReadOnlyList<MetricSubscription> _subscriptions = [];
    private CancellationTokenSource? _lifetimeCancellation;
    private CancellationTokenSource? _workerCancellation;
    private IReadOnlyList<Task> _workers = [];
    private bool _disposed;

    public MetricSamplingService(
        IEnumerable<IMetricProvider> providers,
        MetricValueCache cache,
        ILogger<MetricSamplingService> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _providers = providers.ToArray();
        _providerGates = _providers.ToDictionary(
            provider => provider,
            _ => new SemaphoreSlim(1, 1));
    }

    public event EventHandler<MetricValuesChangedEventArgs>? ValuesChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            if (_lifetimeCancellation is not null)
            {
                return;
            }

            await DiscoverProvidersAsync(cancellationToken);
            _lifetimeCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            StartWorkers();
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            if (_lifetimeCancellation is null)
            {
                return;
            }

            _lifetimeCancellation.Cancel();
            await StopWorkersAsync();
            _lifetimeCancellation.Dispose();
            _lifetimeCancellation = null;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task UpdateSubscriptionsAsync(
        IEnumerable<MetricSubscription> subscriptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        var normalized = subscriptions
            .Where(subscription => subscription.Interval > TimeSpan.Zero)
            .GroupBy(subscription => subscription.MetricId)
            .Select(group => group.OrderBy(subscription => subscription.Interval).First())
            .OrderBy(subscription => subscription.Interval)
            .ThenBy(subscription => subscription.MetricId.Value, StringComparer.Ordinal)
            .ToArray();

        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            _subscriptions = normalized;
            if (_lifetimeCancellation is not null)
            {
                await StopWorkersAsync();
                StartWorkers();
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task RescanProvidersAsync(CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            foreach (var provider in _providers)
            {
                try
                {
                    await provider.RescanAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogProviderFailure(provider, "重新扫描", exception);
                }
            }

            await DiscoverProvidersAsync(cancellationToken);
            if (_lifetimeCancellation is not null)
            {
                await StopWorkersAsync();
                StartWorkers();
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync(CancellationToken.None);
        _disposed = true;
        _stateGate.Dispose();
        foreach (var gate in _providerGates.Values)
        {
            gate.Dispose();
        }
    }

    private async Task DiscoverProvidersAsync(CancellationToken cancellationToken)
    {
        _owners.Clear();
        foreach (var provider in _providers)
        {
            try
            {
                var definitions = await provider.DiscoverAsync(cancellationToken);
                foreach (var definition in definitions)
                {
                    _owners.TryAdd(definition.Id, provider);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogProviderFailure(provider, "发现指标", exception);
            }
        }
    }

    private void StartWorkers()
    {
        if (_lifetimeCancellation is null || _lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        _workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellation.Token);
        _workers = _subscriptions
            .Where(subscription => _owners.ContainsKey(subscription.MetricId))
            .GroupBy(subscription => subscription.Interval)
            .Select(group => RunSamplingGroupAsync(
                group.Key,
                group.Select(subscription => subscription.MetricId).ToArray(),
                _workerCancellation.Token))
            .ToArray();
    }

    private async Task StopWorkersAsync()
    {
        if (_workerCancellation is null)
        {
            return;
        }

        _workerCancellation.Cancel();
        try
        {
            await Task.WhenAll(_workers);
        }
        catch (OperationCanceledException)
        {
            // 正常停止路径。
        }
        finally
        {
            _workerCancellation.Dispose();
            _workerCancellation = null;
            _workers = [];
        }
    }

    private async Task RunSamplingGroupAsync(
        TimeSpan interval,
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await SampleOnceAsync(metricIds, cancellationToken);
                await Task.Delay(interval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 服务停止或订阅变化时结束本采样组。
        }
    }

    private async Task SampleOnceAsync(
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken)
    {
        var requests = metricIds
            .GroupBy(metricId => _owners[metricId])
            .Select(group => ReadProviderAsync(
                group.Key,
                group.ToArray(),
                cancellationToken))
            .ToArray();

        await Task.WhenAll(requests);
    }

    private async Task ReadProviderAsync(
        IMetricProvider provider,
        IReadOnlyCollection<MetricId> metricIds,
        CancellationToken cancellationToken)
    {
        var providerGate = _providerGates[provider];
        await providerGate.WaitAsync(cancellationToken);
        try
        {
            var values = await provider.ReadAsync(metricIds, cancellationToken);
            var changed = new List<MetricValue>(values.Count);
            foreach (var value in values.Values)
            {
                if (_cache.Update(value))
                {
                    changed.Add(value);
                }
            }

            if (changed.Count > 0)
            {
                ValuesChanged?.Invoke(this, new MetricValuesChangedEventArgs(changed));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogProviderFailure(provider, "读取指标", exception);
        }
        finally
        {
            providerGate.Release();
        }
    }

    private void LogProviderFailure(
        IMetricProvider provider,
        string operation,
        Exception exception)
    {
        var now = DateTimeOffset.UtcNow;
        var next = _nextErrorLogs.GetOrAdd(provider.ProviderId, DateTimeOffset.MinValue);
        if (now < next)
        {
            return;
        }

        _nextErrorLogs[provider.ProviderId] = now.Add(ErrorLogInterval);
        _logger.LogWarning(
            exception,
            "指标 Provider {ProviderId} 在{Operation}时失败；其他 Provider 将继续运行",
            provider.ProviderId,
            operation);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
