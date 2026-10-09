using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace USBWatcher.WinUI;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(initializationParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
