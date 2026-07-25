using System.Runtime.CompilerServices;
using TopMonitor.Application.Fps;
using TopMonitor.Infrastructure.Fps;

namespace TopMonitor.Application.Tests;

public sealed class ForegroundFpsTrackerTests
{
    [Fact]
    public async Task Stable_foreground_process_starts_one_session_after_debounce()
    {
        var clock = new ManualTimeProvider(
            DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var foreground = new FakeForegroundProcessService(
            new ForegroundProcessInfo(42, "game", DateTimeOffset.UtcNow));
        var sessions = new FakePresentMonSessionFactory();
        await using var tracker = new ForegroundFpsTracker(
            foreground,
            sessions,
            clock);

        Assert.Null(await tracker.GetCurrentFpsAsync(CancellationToken.None));
        clock.Advance(TimeSpan.FromMilliseconds(749));
        Assert.Null(await tracker.GetCurrentFpsAsync(CancellationToken.None));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);

        Assert.Equal([42], sessions.StartedProcessIds);
    }

    [Fact]
    public async Task Switching_process_disposes_previous_session_after_grace()
    {
        var clock = new ManualTimeProvider(
            DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var firstStart = DateTimeOffset.Parse("2026-07-25T23:00:00Z");
        var foreground = new FakeForegroundProcessService(
            new ForegroundProcessInfo(42, "game-a", firstStart));
        var sessions = new FakePresentMonSessionFactory();
        await using var tracker = new ForegroundFpsTracker(
            foreground,
            sessions,
            clock);

        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(750));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);

        foreground.Current = new ForegroundProcessInfo(
            84,
            "game-b",
            firstStart.AddMinutes(1));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        Assert.False(sessions.Sessions[0].IsDisposed);

        clock.Advance(TimeSpan.FromSeconds(5));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);

        Assert.True(sessions.Sessions[0].IsDisposed);
        Assert.Equal([42, 84], sessions.StartedProcessIds);
    }

    [Fact]
    public async Task Process_without_frames_is_retried_after_backoff()
    {
        var clock = new ManualTimeProvider(
            DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var firstStart = DateTimeOffset.Parse("2026-07-25T23:00:00Z");
        var foreground = new FakeForegroundProcessService(
            new ForegroundProcessInfo(42, "launcher", firstStart));
        var sessions = new FakePresentMonSessionFactory();
        await using var tracker = new ForegroundFpsTracker(
            foreground,
            sessions,
            clock);

        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(750));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(5));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        Assert.Equal([42], sessions.StartedProcessIds);

        clock.Advance(TimeSpan.FromSeconds(10));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);

        Assert.Equal([42, 42], sessions.StartedProcessIds);
    }

    [Fact]
    public async Task Cancellation_disposes_owned_session()
    {
        var clock = new ManualTimeProvider(
            DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var foreground = new FakeForegroundProcessService(
            new ForegroundProcessInfo(42, "game", DateTimeOffset.UtcNow));
        var sessions = new FakePresentMonSessionFactory();
        await using var tracker = new ForegroundFpsTracker(
            foreground,
            sessions,
            clock);
        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(750));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tracker.GetCurrentFpsAsync(cancellation.Token));

        Assert.True(sessions.Sessions[0].IsDisposed);
    }

    [Fact]
    public async Task Missing_foreground_process_never_starts_PresentMon()
    {
        var clock = new ManualTimeProvider(
            DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var sessions = new FakePresentMonSessionFactory();
        await using var tracker = new ForegroundFpsTracker(
            new FakeForegroundProcessService(null),
            sessions,
            clock);

        await tracker.GetCurrentFpsAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        await tracker.GetCurrentFpsAsync(CancellationToken.None);

        Assert.Empty(sessions.StartedProcessIds);
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class FakeForegroundProcessService(
        ForegroundProcessInfo? current) : IForegroundProcessService
    {
        public ForegroundProcessInfo? Current { get; set; } = current;

        public ForegroundProcessInfo? GetForegroundProcess() => Current;
    }

    private sealed class FakePresentMonSessionFactory : IPresentMonSessionFactory
    {
        public List<int> StartedProcessIds { get; } = [];

        public List<FakePresentMonSession> Sessions { get; } = [];

        public Task<IPresentMonSession> StartAsync(
            int processId,
            CancellationToken cancellationToken)
        {
            StartedProcessIds.Add(processId);
            var session = new FakePresentMonSession();
            Sessions.Add(session);
            return Task.FromResult<IPresentMonSession>(session);
        }
    }

    private sealed class FakePresentMonSession : IPresentMonSession
    {
        public bool IsDisposed { get; private set; }

        public async IAsyncEnumerable<PresentedFrame> ReadFramesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
