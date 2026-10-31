using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace USBWatcher.Core;

public static class ElevationHelper
{
    private const int ErrorCancelled = 1223;

    public static bool EnsureRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            return true;
        }

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };
        foreach (string argument in Environment.GetCommandLineArgs().Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // The user declined the UAC prompt, so leave the app closed.
        }

        return false;
    }
}
