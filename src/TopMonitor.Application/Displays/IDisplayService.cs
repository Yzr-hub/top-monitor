namespace TopMonitor.Application.Displays;

public interface IDisplayService
{
    IReadOnlyList<DisplayInfo> GetDisplays();
}
