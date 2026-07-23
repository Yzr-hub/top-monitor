namespace TopMonitor.Application.Configuration;

public interface IStartupService
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}
