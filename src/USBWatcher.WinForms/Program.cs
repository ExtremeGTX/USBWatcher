using Velopack;
using USBWatcher.Core;

namespace USBWatcher
{
    internal static class Program
    {
        private static readonly string MutexName = "Global\\USBWatcher_SingleInstance";
        private static readonly string ShowWindowEventName = "Global\\USBWatcher_ShowWindow";
        private static Mutex? _mutex;
        private static EventWaitHandle? _showWindowEvent;
        private static RegisteredWaitHandle? _showWindowRegistration;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            VelopackApp.Build()
                .SetAutoApplyOnStartup(false)
                .Run();

            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            _showWindowEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                ShowWindowEventName);
            if (!createdNew)
            {
                SingleInstanceActivation.AllowExistingInstanceToTakeForeground();
                _showWindowEvent.Set();
                _showWindowEvent.Dispose();
                _mutex.Dispose();
                return;
            }

            try
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();

                bool minimized = false;
                // Check if the app was started with --minimized argument
                if (args.Contains("--minimized"))
                {
                    minimized = true;
                }
                using var mainWindow = new Main(minimized);
                _ = mainWindow.Handle;
                _showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
                    _showWindowEvent,
                    (_, _) =>
                    {
                        if (!mainWindow.IsDisposed && mainWindow.IsHandleCreated)
                        {
                            mainWindow.BeginInvoke(new Action(mainWindow.ShowFromExternalLaunch));
                        }
                    },
                    null,
                    Timeout.Infinite,
                    executeOnlyOnce: false);

                Application.Run(mainWindow);
            }
            finally
            {
                // Release the mutex when the application exits
                _showWindowRegistration?.Unregister(null);
                _showWindowEvent?.Dispose();
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }
    }
}
