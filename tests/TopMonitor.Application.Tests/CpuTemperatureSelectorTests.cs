using TopMonitor.Infrastructure.Hardware;

namespace TopMonitor.Application.Tests;

public sealed class CpuTemperatureSelectorTests
{
    [Fact]
    public void Package_with_valid_value_has_highest_priority()
    {
        var candidates = new[]
        {
            new CpuTemperatureCandidate("core-max", "Core Max", 72),
            new CpuTemperatureCandidate("package", "CPU Package", 68)
        };

        var selected = CpuTemperatureSelector.Select(candidates);

        Assert.Equal("package", selected?.Candidate.Id);
        Assert.Equal("package", selected?.Reason);
    }

    [Fact]
    public void Core_max_is_used_when_package_value_is_missing()
    {
        var candidates = new[]
        {
            new CpuTemperatureCandidate("package", "CPU Package", null),
            new CpuTemperatureCandidate("core-max", "Core Max", 74)
        };

        Assert.Equal("core-max", CpuTemperatureSelector.Select(candidates)?.Candidate.Id);
    }

    [Fact]
    public void Maximum_valid_core_is_last_resort()
    {
        var candidates = new[]
        {
            new CpuTemperatureCandidate("core-0", "CPU Core #1", 61),
            new CpuTemperatureCandidate("core-1", "CPU Core #2", 66)
        };

        Assert.Equal("core-1", CpuTemperatureSelector.Select(candidates)?.Candidate.Id);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-21)]
    [InlineData(126)]
    public void Invalid_values_are_rejected(double value)
    {
        var candidates = new[]
        {
            new CpuTemperatureCandidate("package", "CPU Package", value)
        };

        Assert.Null(CpuTemperatureSelector.Select(candidates));
    }
}
