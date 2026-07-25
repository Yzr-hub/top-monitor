namespace TopMonitor.Application.Fps;

public interface IPresentMonSession : IAsyncDisposable
{
    IAsyncEnumerable<PresentedFrame> ReadFramesAsync(
        CancellationToken cancellationToken);
}
