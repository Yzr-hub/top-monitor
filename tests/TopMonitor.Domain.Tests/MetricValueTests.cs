using TopMonitor.Domain.Metrics;

namespace TopMonitor.Domain.Tests;

public sealed class MetricValueTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_number_is_normalized_to_invalid(double invalidNumber)
    {
        var value = MetricValue.Create(
            MetricIds.CpuTemperaturePackage,
            invalidNumber,
            DateTimeOffset.UtcNow);

        Assert.Null(value.Value);
        Assert.Equal(MetricStatus.Invalid, value.Status);
        Assert.NotNull(value.ErrorMessage);
    }
}
