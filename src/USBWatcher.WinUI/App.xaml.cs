using Microsoft.UI.Xaml;
using USBWatcher.Core;

namespace USBWatcher.WinUI;

public partial class App : Application
{
    private const string MutexName = "Global\\USBWatcher_WinUI_SingleInstance";
    private const string ShowWindowEventName = "Global\\USBWatcher_WinUI_ShowWindow";

    private MainWindow? _window;
    private Mutex? _mutex;
    private EventWaitHandle? _showWindowEvent;
    private RegisteredWaitHandle? _showWindowRegistration;

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
        _mutex = new Mutex(true, MutexName, out bool createdNew);
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
            Exit();
            return;
        }

        SettingsStore.Load();
        bool commandLineMinimized = Environment.GetCommandLineArgs()
            .Any(value => string.Equals(value, "--minimized", StringComparison.OrdinalIgnoreCase));

        _window = new MainWindow(commandLineMinimized || SettingsStore.Current.StartMinimized);
        _window.Closed += MainWindow_Closed;
        _showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showWindowEvent,
            (_, _) => _window?.DispatcherQueue.TryEnqueue(_window.ShowFromExternalLaunch),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        _window.Activate();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _showWindowRegistration?.Unregister(null);
        _showWindowEvent?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
