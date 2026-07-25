namespace TopMonitor.Infrastructure.Hardware;

public static class CpuTemperatureSelector
{
    public static CpuTemperatureSelection? Select(
        IReadOnlyCollection<CpuTemperatureCandidate> candidates)
    {
        var valid = candidates
            .Where(candidate => candidate.Value is { } value &&
                                double.IsFinite(value) &&
                                value is >= -20 and <= 125)
            .ToArray();

        return FindExact(valid, "CPU Package", "package")
            ?? FindExact(valid, "Package", "package")
            ?? FindExact(valid, "Core Max", "core-max")
            ?? FindAny(
                valid,
                ["Package", "Tdie", "Tctl/Tdie"],
                "package-equivalent")
            ?? valid
                .Where(candidate => candidate.Name.Contains(
                    "Core",
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(candidate => candidate.Value)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .Select(candidate => new CpuTemperatureSelection(candidate, "max-core"))
                .FirstOrDefault();
    }

    private static CpuTemperatureSelection? FindExact(
        IEnumerable<CpuTemperatureCandidate> candidates,
        string name,
        string reason) =>
        candidates
            .Where(candidate => string.Equals(
                candidate.Name,
                name,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select(candidate => new CpuTemperatureSelection(candidate, reason))
            .FirstOrDefault();

    private static CpuTemperatureSelection? FindAny(
        IEnumerable<CpuTemperatureCandidate> candidates,
        IReadOnlyCollection<string> names,
        string reason) =>
        candidates
            .Where(candidate => names.Any(name => candidate.Name.Contains(
                name,
                StringComparison.OrdinalIgnoreCase)))
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select(candidate => new CpuTemperatureSelection(candidate, reason))
            .FirstOrDefault();
}
