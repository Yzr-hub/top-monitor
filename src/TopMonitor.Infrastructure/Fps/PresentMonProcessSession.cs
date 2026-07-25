using System.Diagnostics;
using System.Runtime.CompilerServices;
using TopMonitor.Application.Fps;

namespace TopMonitor.Infrastructure.Fps;

public sealed class PresentMonProcessSession(Process process) : IPresentMonSession
{
    private readonly CancellationTokenSource _readCancellation = new();
    private bool _disposed;

    public async IAsyncEnumerable<PresentedFrame> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _readCancellation.Token);
        var parser = new PresentMonCsvParser();
        var headerFound = false;

        while (!linkedCancellation.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync(
                linkedCancellation.Token);
            if (line is null)
            {
                yield break;
            }

            if (!headerFound)
            {
                headerFound = parser.TryReadHeader(line);
                continue;
            }

            if (parser.TryReadFrame(line, out var frame))
            {
                yield return frame;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _readCancellation.Cancel();
        try
        {
            if (!process.HasExited)
            {
                using var timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(2));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(CancellationToken.None);
                    }
                }
            }
        }
        catch (InvalidOperationException)
        {
            // The owned process exited between state checks.
        }
        finally
        {
            process.Dispose();
            _readCancellation.Dispose();
        }
    }
}
