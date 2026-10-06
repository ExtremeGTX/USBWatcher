using Microsoft.UI.Xaml;

namespace USBWatcher.WinUI;

public partial class App : Application
{
    private MainWindow? _window;
    private Mutex? _mutex;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine(args.Exception);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mutex = new Mutex(true, "Global\\USBWatcher_WinUI_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Exit();
            return;
        }

        SettingsStore.Load();
        bool commandLineMinimized = Environment.GetCommandLineArgs()
            .Any(value => string.Equals(value, "--minimized", StringComparison.OrdinalIgnoreCase));

        _window = new MainWindow(commandLineMinimized || SettingsStore.Current.StartMinimized);
        _window.Activate();
    }
}
