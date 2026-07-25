using TopMonitor.Application.Fps;

namespace TopMonitor.Infrastructure.Fps;

public sealed class FpsSlidingWindow(TimeSpan duration)
{
    private readonly Queue<PresentedFrame> _frames = [];
    private readonly double _durationSeconds = duration.TotalSeconds;

    public void Add(PresentedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frames.Enqueue(frame);
        RemoveExpired(frame.TimeSeconds);
    }

    public int? GetFps(double nowSeconds)
    {
        RemoveExpired(nowSeconds);
        if (_frames.Count < 2)
        {
            return null;
        }

        var first = _frames.Peek();
        var last = _frames.Last();
        var elapsed = last.TimeSeconds - first.TimeSeconds;
        return elapsed <= 0
            ? null
            : (int)Math.Round(
                (_frames.Count - 1) / elapsed,
                MidpointRounding.AwayFromZero);
    }

    private void RemoveExpired(double nowSeconds)
    {
        var cutoff = nowSeconds - _durationSeconds;
        while (_frames.TryPeek(out var frame) && frame.TimeSeconds < cutoff)
        {
            _frames.Dequeue();
        }
    }
}
