using TopMonitor.Infrastructure.Fps;

namespace TopMonitor.Application.Tests;

public sealed class PresentMonSessionFactoryTests
{
    [Fact]
    public void Factory_targets_numeric_pid_and_stdout_without_shell()
    {
        var options = PresentMonSessionFactory.CreateStartInfo(
            @"C:\TopMonitor\Dependencies\PresentMon.exe",
            4242);

        Assert.False(options.UseShellExecute);
        Assert.True(options.RedirectStandardOutput);
        Assert.True(options.RedirectStandardError);
        Assert.Equal(
            "--process_id 4242 --output_stdout --v1_metrics --terminate_on_proc_exit --session_name TopMonitorCapture",
            string.Join(" ", options.ArgumentList));
    }

    [Fact]
    public void Cleanup_terminates_only_the_owned_session_without_capture()
    {
        var options = PresentMonSessionFactory.CreateCleanupStartInfo(
            @"C:\TopMonitor\Dependencies\PresentMon.exe");

        Assert.False(options.UseShellExecute);
        Assert.True(options.RedirectStandardOutput);
        Assert.True(options.RedirectStandardError);
        Assert.Equal(
            "--terminate_existing_session --session_name TopMonitorCapture",
            string.Join(" ", options.ArgumentList));
    }
}
