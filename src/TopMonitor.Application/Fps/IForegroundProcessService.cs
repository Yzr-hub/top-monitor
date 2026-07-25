namespace TopMonitor.Application.Fps;

public interface IForegroundProcessService
{
    ForegroundProcessInfo? GetForegroundProcess();
}
