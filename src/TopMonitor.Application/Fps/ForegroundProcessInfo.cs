namespace TopMonitor.Application.Fps;

public sealed record ForegroundProcessInfo(
    int ProcessId,
    string ProcessName,
    DateTimeOffset StartTime);
