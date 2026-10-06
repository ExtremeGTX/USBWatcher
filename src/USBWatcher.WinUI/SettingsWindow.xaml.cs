using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace USBWatcher.WinUI;

public sealed partial class SettingsWindow : Window
{
    private bool _loading = true;

    public event EventHandler? SettingsApplied;

    public SettingsWindow()
    {
        InitializeComponent();
        Title = "USBWatcher Settings";
        SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        int width = Math.Min(780, displayArea.WorkArea.Width - 24);
        int height = Math.Min(820, displayArea.WorkArea.Height - 24);
        AppWindow.Resize(new SizeInt32(width, height));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }

        Root.ActualThemeChanged += Root_ActualThemeChanged;
        LoadValues();
        _loading = false;
    }

    private void LoadValues()
    {
        AppSettings settings = SettingsStore.Current;
        StartWithWindowsToggle.IsOn = settings.StartWithWindows;
        StartMinimizedToggle.IsOn = settings.StartMinimized;
        MinimizeToTrayToggle.IsOn = settings.MinimizeToTray;
        ShowConnectedToggle.IsOn = settings.ShowOnlyConnectedDevices;
        AutoScrollToggle.IsOn = settings.AutoScrollRecentEvents;
        ExpandLastEventOnlyToggle.IsOn = settings.ExpandOnlyLastDeviceEvent;
        RememberPositionToggle.IsOn = settings.RememberWindowPosition;
        MonitoringScopeBox.SelectedIndex = settings.ListenToAllDevices ? 1 : 0;
        ThemeBox.SelectedIndex = settings.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
        MaxEventsBox.Value = Math.Clamp(settings.MaxRecentEvents, 10, 1000);
        ApplyTheme(settings.Theme);
    }

    private void SettingChanged(object sender, RoutedEventArgs e) => SaveValues();

    private void MaxEventsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SaveValues();

    private void SaveValues()
    {
        if (_loading)
        {
            return;
        }

        AppSettings settings = SettingsStore.Current;
        settings.StartWithWindows = StartWithWindowsToggle.IsOn;
        settings.StartMinimized = StartMinimizedToggle.IsOn;
        settings.MinimizeToTray = MinimizeToTrayToggle.IsOn;
        settings.ShowOnlyConnectedDevices = ShowConnectedToggle.IsOn;
        settings.AutoScrollRecentEvents = AutoScrollToggle.IsOn;
        settings.ExpandOnlyLastDeviceEvent = ExpandLastEventOnlyToggle.IsOn;
        settings.RememberWindowPosition = RememberPositionToggle.IsOn;
        settings.ListenToAllDevices = MonitoringScopeBox.SelectedIndex == 1;
        settings.Theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
        settings.MaxRecentEvents = double.IsNaN(MaxEventsBox.Value)
            ? 100
            : Math.Clamp((int)MaxEventsBox.Value, 10, 1000);

        ApplyTheme(settings.Theme);
        SettingsStore.Save();
        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyTheme(string theme)
    {
        Root.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        bool isDark = theme == "Dark" ||
            (theme == "System" && Application.Current.RequestedTheme == ApplicationTheme.Dark);
        UpdateTitleBarColors(isDark);
    }

    private void Root_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateTitleBarColors(sender.ActualTheme == ElementTheme.Dark);
    }

    private void UpdateTitleBarColors(bool isDark)
    {
        Windows.UI.Color background = isDark
            ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
            : Windows.UI.Color.FromArgb(255, 243, 243, 243);
        Windows.UI.Color foreground = isDark
            ? Microsoft.UI.Colors.White
            : Microsoft.UI.Colors.Black;
        Windows.UI.Color inactiveForeground = isDark
            ? Windows.UI.Color.FromArgb(255, 160, 160, 160)
            : Windows.UI.Color.FromArgb(255, 96, 96, 96);
        Windows.UI.Color hoverBackground = isDark
            ? Windows.UI.Color.FromArgb(255, 51, 51, 51)
            : Windows.UI.Color.FromArgb(255, 229, 229, 229);
        Windows.UI.Color pressedBackground = isDark
            ? Windows.UI.Color.FromArgb(255, 64, 64, 64)
            : Windows.UI.Color.FromArgb(255, 215, 215, 215);

        AppWindow.TitleBar.BackgroundColor = background;
        AppWindow.TitleBar.ForegroundColor = foreground;
        AppWindow.TitleBar.InactiveBackgroundColor = background;
        AppWindow.TitleBar.InactiveForegroundColor = inactiveForeground;
        AppWindow.TitleBar.ButtonBackgroundColor = background;
        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = hoverBackground;
        AppWindow.TitleBar.ButtonHoverForegroundColor = foreground;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = pressedBackground;
        AppWindow.TitleBar.ButtonPressedForegroundColor = foreground;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = background;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = inactiveForeground;
    }
}
