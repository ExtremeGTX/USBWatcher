namespace USBWatcher
{
    internal static class Program
    {
        private static readonly string MutexName = "Global\\USBWatcher_SingleInstance";
        private static Mutex? _mutex;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show("USBWatcher is already running.", "USBWatcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                Application.Run(new Main(minimized));
            }
            finally
            {
                // Release the mutex when the application exits
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }
    }
}