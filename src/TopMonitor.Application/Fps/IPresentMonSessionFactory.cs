namespace TopMonitor.Application.Fps;

public interface IPresentMonSessionFactory
{
    Task<IPresentMonSession> StartAsync(
        int processId,
        CancellationToken cancellationToken);
}
