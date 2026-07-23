namespace TopMonitor.Application.Displays;

public sealed record DisplayInfo(
    string Id,
    string Name,
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary);
