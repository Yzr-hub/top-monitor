namespace TopMonitor.Application.Fps;

public sealed record PresentedFrame(
    int ProcessId,
    double TimeSeconds,
    string PresentMode);
