using System.ComponentModel;
using System.Security.Principal;
using System.Text;
using System.Runtime.Versioning;
using TopMonitor.Infrastructure.Hardware;

namespace TopMonitor.Infrastructure.Fps;

public sealed class PerformanceLogUsersService(
    IElevatedProcessRunner elevatedProcessRunner)
{
    private const string PerformanceLogUsersSid = "S-1-5-32-559";

    [SupportedOSPlatform("windows")]
    public bool IsCurrentUserMember()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var target = new SecurityIdentifier(PerformanceLogUsersSid);
        return identity.Groups?.Any(group => group.Equals(target)) == true;
    }

    [SupportedOSPlatform("windows")]
    public async Task<bool> AddCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        if (IsCurrentUserMember())
        {
            return true;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var command = CreateSetupCommand(identity.Name);
        var encodedCommand = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(command));
        var powerShellPath = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

        try
        {
            var exitCode = await elevatedProcessRunner.RunAsync(
                powerShellPath,
                $"-NoProfile -NonInteractive -EncodedCommand {encodedCommand}",
                cancellationToken);
            return exitCode == 0;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return false;
        }
    }

    public static string CreateSetupCommand(string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        var escapedAccountName = accountName.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
        return
            $"Add-LocalGroupMember -SID ([System.Security.Principal.SecurityIdentifier]'{PerformanceLogUsersSid}') -Member '{escapedAccountName}'";
    }
}
