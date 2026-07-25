using TopMonitor.Application.Fps;

namespace TopMonitor.Infrastructure.Fps;

public sealed class ForegroundFpsTracker(
    IForegroundProcessService foregroundProcessService,
    IPresentMonSessionFactory sessionFactory,
    TimeProvider timeProvider) : IAsyncDisposable
{
    private static readonly TimeSpan CandidateDebounce =
        TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ForegroundGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RestartBackoff = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _frameSync = new();
    private readonly HashSet<ProcessKey> _nonGameProcesses = [];
    private readonly Queue<ProcessKey> _nonGameOrder = [];
    private IPresentMonSession? _session;
    private CancellationTokenSource? _sessionCancellation;
    private Task? _readerTask;
    private FpsSlidingWindow? _window;
    private ProcessKey? _activeProcess;
    private ProcessKey? _candidateProcess;
    private DateTimeOffset _candidateSince;
    private DateTimeOffset _lastActiveForeground;
    private DateTimeOffset _probeDeadline;
    private DateTimeOffset _lastFrameReceived;
    private DateTimeOffset _restartAfter;
    private ProcessKey? _restartProcess;
    private double _latestFrameTime;
    private int _frameCount;
    private bool _disposed;

    public async Task<int?> GetCurrentFpsAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (cancellationToken.IsCancellationRequested)
            {
                await StopSessionAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }

            var now = timeProvider.GetUtcNow();
            var foreground = foregroundProcessService.GetForegroundProcess();
            ProcessKey? foregroundKey = foreground is null
                ? null
                : new ProcessKey(foreground.ProcessId, foreground.StartTime);

            if (_activeProcess is { } active)
            {
                if (foregroundKey == active)
                {
                    _lastActiveForeground = now;
                    ClearCandidate();
                    if (HasProbeTimedOut(now))
                    {
                        CacheNonGame(active);
                        await StopSessionAsync();
                        return null;
                    }

                    return GetFps(now);
                }

                TrackCandidate(foregroundKey, now);
                if (now - _lastActiveForeground < ForegroundGrace)
                {
                    return GetFps(now);
                }

                await StopSessionAsync();
            }

            if (foregroundKey is not { } target ||
                _nonGameProcesses.Contains(target))
            {
                if (foregroundKey is null)
                {
                    ClearCandidate();
                }

                return null;
            }

            if (!TrackCandidate(target, now) ||
                now - _candidateSince < CandidateDebounce)
            {
                return null;
            }

            if (_restartProcess == target && now < _restartAfter)
            {
                return null;
            }

            try
            {
                _session = await sessionFactory.StartAsync(
                    target.ProcessId,
                    cancellationToken);
            }
            catch
            {
                _restartProcess = target;
                _restartAfter = now + RestartBackoff;
                throw;
            }

            _activeProcess = target;
            _lastActiveForeground = now;
            _probeDeadline = now + ProbeTimeout;
            _sessionCancellation = new CancellationTokenSource();
            lock (_frameSync)
            {
                _window = new FpsSlidingWindow(TimeSpan.FromSeconds(1));
                _frameCount = 0;
                _latestFrameTime = 0;
                _lastFrameReceived = DateTimeOffset.MinValue;
            }

            _readerTask = ReadFramesAsync(
                _session,
                target,
                _sessionCancellation.Token);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (_disposed)
            {
                return;
            }

            await StopSessionAsync();
            _disposed = true;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task ReadFramesAsync(
        IPresentMonSession session,
        ProcessKey target,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in session
                               .ReadFramesAsync(cancellationToken)
                               .WithCancellation(cancellationToken))
            {
                if (frame.ProcessId != target.ProcessId)
                {
                    continue;
                }

                lock (_frameSync)
                {
                    _window?.Add(frame);
                    _frameCount++;
                    _latestFrameTime = frame.TimeSeconds;
                    _lastFrameReceived = timeProvider.GetUtcNow();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal session shutdown.
        }
    }

    private int? GetFps(DateTimeOffset now)
    {
        lock (_frameSync)
        {
            if (_window is null ||
                _frameCount < 2 ||
                now - _lastFrameReceived > TimeSpan.FromSeconds(1))
            {
                return null;
            }

            return _window.GetFps(_latestFrameTime);
        }
    }

    private bool HasProbeTimedOut(DateTimeOffset now)
    {
        lock (_frameSync)
        {
            return _frameCount == 0 && now >= _probeDeadline;
        }
    }

    private bool TrackCandidate(ProcessKey? candidate, DateTimeOffset now)
    {
        if (_candidateProcess == candidate)
        {
            return true;
        }

        _candidateProcess = candidate;
        _candidateSince = now;
        return false;
    }

    private void ClearCandidate()
    {
        _candidateProcess = null;
        _candidateSince = default;
    }

    private void CacheNonGame(ProcessKey process)
    {
        if (_nonGameProcesses.Add(process))
        {
            _nonGameOrder.Enqueue(process);
        }

        while (_nonGameOrder.Count > 128)
        {
            _nonGameProcesses.Remove(_nonGameOrder.Dequeue());
        }
    }

    private async Task StopSessionAsync()
    {
        var cancellation = _sessionCancellation;
        var session = _session;
        var reader = _readerTask;
        _sessionCancellation = null;
        _session = null;
        _readerTask = null;
        _activeProcess = null;

        cancellation?.Cancel();
        if (session is not null)
        {
            await session.DisposeAsync();
        }

        if (reader is not null)
        {
            try
            {
                await reader;
            }
            catch (OperationCanceledException)
            {
                // Normal session shutdown.
            }
        }

        cancellation?.Dispose();
        lock (_frameSync)
        {
            _window = null;
            _frameCount = 0;
        }
    }

    private readonly record struct ProcessKey(
        int ProcessId,
        DateTimeOffset StartTime);
}
