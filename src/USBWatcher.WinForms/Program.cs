namespace USBWatcher
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
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
    }
}