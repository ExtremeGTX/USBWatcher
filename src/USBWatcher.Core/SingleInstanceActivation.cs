using System.Diagnostics;
using System.Runtime.InteropServices;

namespace USBWatcher.Core;

public static class SingleInstanceActivation
{
    public static void AllowExistingInstanceToTakeForeground()
    {
        using Process current = Process.GetCurrentProcess();
        foreach (Process process in Process.GetProcessesByName(current.ProcessName))
        {
            using (process)
            {
                if (process.Id != current.Id)
                {
                    _ = AllowSetForegroundWindow((uint)process.Id);
                    return;
                }
            }
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
