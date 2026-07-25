using TopMonitor.Infrastructure.Fps;

namespace TopMonitor.Application.Tests;

public sealed class PerformanceLogUsersServiceTests
{
    [Fact]
    public void Setup_command_uses_well_known_performance_log_users_sid()
    {
        var command = PerformanceLogUsersService.CreateSetupCommand(
            @"DESKTOP\User");

        Assert.Contains("S-1-5-32-559", command, StringComparison.Ordinal);
        Assert.Contains(@"DESKTOP\User", command, StringComparison.Ordinal);
    }

    [Fact]
    public void Setup_command_escapes_single_quotes_in_account_name()
    {
        var command = PerformanceLogUsersService.CreateSetupCommand(
            @"DESKTOP\User'Name");

        Assert.Contains(@"DESKTOP\User''Name", command, StringComparison.Ordinal);
    }
}
