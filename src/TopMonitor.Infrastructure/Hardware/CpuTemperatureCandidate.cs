namespace TopMonitor.Infrastructure.Hardware;

public sealed record CpuTemperatureCandidate(
    string Id,
    string Name,
    double? Value);

public sealed record CpuTemperatureSelection(
    CpuTemperatureCandidate Candidate,
    string Reason);
