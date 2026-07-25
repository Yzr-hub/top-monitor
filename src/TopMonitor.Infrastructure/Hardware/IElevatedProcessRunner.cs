namespace TopMonitor.Infrastructure.Hardware;

public interface IElevatedProcessRunner
{
    Task<int> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken);
}
