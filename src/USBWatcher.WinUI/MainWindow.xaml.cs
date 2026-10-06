using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Microsoft.UI.Dispatching;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using USBWatcher.Core;
using Windows.Graphics;

namespace USBWatcher.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly USBWatcherCore _watcher;
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private SettingsWindow? _settingsWindow;
    private bool _exitRequested;

    public ObservableCollection<DeviceRow> Devices { get; } = new();
    public ObservableCollection<EventNode> Events { get; } = new();

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();
        Title = "USBWatcher";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        SystemBackdrop = new MicaBackdrop();

        ConfigureWindow();
        ApplyTheme();
        _trayIcon = CreateTrayIcon();

        DeviceMonitoringScope scope = SettingsStore.Current.ListenToAllDevices
            ? DeviceMonitoringScope.AllDevices
            : DeviceMonitoringScope.ExternalUsbDevicesOnly;
        _watcher = new USBWatcherCore(DeviceWatcher_DeviceChangeEvent, scope);
        _watcher.DeviceListChanged += Watcher_DeviceListChanged;
        RefreshDevices();

        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        if (startMinimized)
        {
            Activated += HideAfterFirstActivation;
        }
    }

    private void ConfigureWindow()
    {
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;
        int requestedWidth = SettingsStore.Current.HasSavedWindowPlacement
            ? SettingsStore.Current.WindowWidth
            : 560;
        int requestedHeight = SettingsStore.Current.HasSavedWindowPlacement
            ? SettingsStore.Current.WindowHeight
            : 880;
        int width = Math.Clamp(requestedWidth, 480, Math.Max(480, workArea.Width - 24));
        int height = Math.Clamp(requestedHeight, 600, Math.Max(600, workArea.Height - 24));
        AppWindow.Resize(new SizeInt32(width, height));

        if (SettingsStore.Current.RememberWindowPosition &&
            SettingsStore.Current.HasSavedWindowPlacement &&
            SettingsStore.Current.WindowX != int.MinValue &&
            SettingsStore.Current.WindowY != int.MinValue)
        {
            AppWindow.Move(new PointInt32(SettingsStore.Current.WindowX, SettingsStore.Current.WindowY));
        }
        else
        {
            PositionNearSystemTray(displayArea, width, height);
        }
    }

    private void PositionNearSystemTray(DisplayArea displayArea, int width, int height)
    {
        const int margin = 12;
        RectInt32 workArea = displayArea.WorkArea;
        RectInt32 outerBounds = displayArea.OuterBounds;

        bool taskbarAtTop = workArea.Y > outerBounds.Y;
        bool taskbarAtLeft = workArea.X > outerBounds.X;

        int x = taskbarAtLeft
            ? workArea.X + margin
            : workArea.X + workArea.Width - width - margin;
        int y = taskbarAtTop
            ? workArea.Y + margin
            : workArea.Y + workArea.Height - height - margin;

        AppWindow.Move(new PointInt32(x, y));
    }

    private void HideAfterFirstActivation(object sender, WindowActivatedEventArgs args)
    {
        Activated -= HideAfterFirstActivation;
        AppWindow.Hide();
    }

    private System.Windows.Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show USBWatcher", null, (_, _) => DispatcherQueue.TryEnqueue(ShowWindow));
        menu.Items.Add("Settings", null, (_, _) => DispatcherQueue.TryEnqueue(OpenSettings));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => DispatcherQueue.TryEnqueue(ExitApplication));

        var icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "USBWatcher",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                DispatcherQueue.TryEnqueue(ShowWindow);
            }
        };
        return icon;
    }

    private void DeviceWatcher_DeviceChangeEvent(object? sender, DeviceChangeEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AddDeviceEvents(e);
        });
    }

    private void Watcher_DeviceListChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshDevices);

    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (UsbDeviceRecord device in _watcher.GetUsbDevicesList())
        {
            string identity = string.IsNullOrEmpty(device.SerialNumber) ? device.MI : device.SerialNumber;
            string alias = SettingsStore.GetAlias(device.VID, device.PID, identity) ?? string.Empty;
            Devices.Add(new DeviceRow
            {
                Name = device.DeviceName,
                Port = device.PortName,
                Alias = alias,
                SerialNumber = identity,
                Vid = device.VID,
                Pid = device.PID
            });
        }
    }

    private void AddDeviceEvents(DeviceChangeEventArgs args)
    {
        IReadOnlyList<DeviceChangeGroup> groups = args.Groups.Count > 0
            ? args.Groups
            : new[]
            {
                new DeviceChangeGroup(args.DeviceID, args.Description,
                    new[] { new DeviceChangeNode(args.DeviceID, args.Description, args.Present, null) })
            };

        foreach (DeviceChangeGroup group in groups)
        {
            bool inserted = group.Nodes.Any(node => node.Changed && node.Present);
            bool removed = group.Nodes.Any(node => node.Changed && !node.Present);
            string eventText = inserted && removed ? "Changed" : inserted ? "Connected" : "Disconnected";
            var statusColor = inserted && removed
                ? Colors.Goldenrod
                : inserted ? Colors.LimeGreen : Windows.UI.Color.FromArgb(255, 205, 50, 50);
            DateTime nowUtc = DateTime.UtcNow;
            EventNode? root = Events
                .Reverse()
                .FirstOrDefault(node =>
                    string.Equals(node.DeviceId, group.DeviceID, StringComparison.OrdinalIgnoreCase) &&
                    node.MergeKind == eventText &&
                    nowUtc - node.LastUpdatedUtc <= TimeSpan.FromSeconds(3));

            if (root is null)
            {
                root = new EventNode
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    Title = group.Description,
                    Details = eventText,
                    DeviceId = group.DeviceID,
                    ShowStatus = true,
                    StatusBrush = new SolidColorBrush(statusColor),
                    MergeKind = eventText,
                    LastUpdatedUtc = nowUtc
                };
                Events.Add(root);
            }
            else
            {
                root.Title = group.Description;
                root.Details = eventText;
                root.StatusBrush = new SolidColorBrush(statusColor);
                root.LastUpdatedUtc = nowUtc;
            }

            foreach (DeviceChangeNode change in group.Nodes)
            {
                if (string.Equals(change.DeviceID, group.DeviceID, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!root.DescendantNodes.TryGetValue(change.DeviceID, out EventNode? descendant))
                {
                    descendant = new EventNode
                    {
                        DeviceId = change.DeviceID,
                        StatusBrush = new SolidColorBrush(Colors.Transparent)
                    };
                    root.DescendantNodes[change.DeviceID] = descendant;
                }

                descendant.Title = change.Description;
                descendant.ParentDeviceId = change.ParentDeviceID;
            }

            // A node that gains descendants becomes an intermediate sub-root.
            // Keep it in the merge cache, but display only the final leaf devices.
            var descendantParentIds = root.DescendantNodes.Values
                .Where(node => !string.IsNullOrEmpty(node.ParentDeviceId))
                .Select(node => node.ParentDeviceId!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            EventNode[] visibleDescendants = root.DescendantNodes.Values
                .Where(node => !descendantParentIds.Contains(node.DeviceId))
                .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            root.Children.Clear();
            foreach (EventNode descendant in visibleDescendants)
            {
                root.Children.Add(descendant);
            }
        }

        int limit = Math.Clamp(SettingsStore.Current.MaxRecentEvents, 10, 1000);
        while (Events.Count > limit)
        {
            Events.RemoveAt(0);
        }

        if (SettingsStore.Current.AutoScrollRecentEvents && Events.Count > 0)
        {
            ScrollEventsToEnd();
        }
    }

    private void ScrollEventsToEnd()
    {
        // TreeView creates and expands descendant containers asynchronously. Two
        // low-priority passes ensure its final scroll extent includes every child.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            EventsTree.UpdateLayout();
            ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(EventsTree);
            if (scrollViewer is null)
            {
                return;
            }

            scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, disableAnimation: true);
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                EventsTree.UpdateLayout();
                scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, disableAnimation: true);
            });
        });
    }

    private async void DevicesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (DevicesList.SelectedItem is not DeviceRow device)
        {
            return;
        }

        var aliasBox = new TextBox
        {
            Text = device.Alias,
            Header = $"Alias for {device.Name}",
            PlaceholderText = "Friendly alias"
        };
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "Edit device alias",
            Content = aliasBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        string alias = RemovePortSuffix(aliasBox.Text).Trim();
        SettingsStore.SetAlias(device.Vid, device.Pid, device.SerialNumber, alias);
        try
        {
            string windowsName = string.IsNullOrWhiteSpace(alias) ? device.Name : alias;
            _watcher.SetUSBDeviceFriendlyName(device.Port, windowsName);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("The alias was saved, but Windows rejected the device registry rename.", ex.Message);
        }
        RefreshDevices();
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        await new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        }.ShowAsync();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.SettingsApplied += SettingsWindow_SettingsApplied;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Activate();
    }

    private void SettingsWindow_SettingsApplied(object? sender, EventArgs e)
    {
        _watcher.SetMonitoringScope(SettingsStore.Current.ListenToAllDevices
            ? DeviceMonitoringScope.AllDevices
            : DeviceMonitoringScope.ExternalUsbDevicesOnly);
        ApplyTheme();
        TrimEvents();
    }

    private void ApplyTheme()
    {
        Root.RequestedTheme = SettingsStore.Current.Theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void TrimEvents()
    {
        int limit = Math.Clamp(SettingsStore.Current.MaxRecentEvents, 10, 1000);
        while (Events.Count > limit)
        {
            Events.RemoveAt(0);
        }
    }

    private void ClearEvents_Click(object sender, RoutedEventArgs e) => Events.Clear();

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        SaveWindowPlacement();
        if (!_exitRequested && SettingsStore.Current.MinimizeToTray)
        {
            args.Cancel = true;
            AppWindow.Hide();
        }
    }

    private void SaveWindowPlacement()
    {
        if (!SettingsStore.Current.RememberWindowPosition)
        {
            return;
        }

        SettingsStore.Current.WindowX = AppWindow.Position.X;
        SettingsStore.Current.WindowY = AppWindow.Position.Y;
        SettingsStore.Current.WindowWidth = AppWindow.Size.Width;
        SettingsStore.Current.WindowHeight = AppWindow.Size.Height;
        SettingsStore.Current.HasSavedWindowPlacement = true;
        SettingsStore.Save();
    }

    private void ShowWindow()
    {
        Activate();
        AppWindow.Show();
        if (SettingsStore.Current.AutoScrollRecentEvents && Events.Count > 0)
        {
            ScrollEventsToEnd();
        }
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _watcher.DeviceListChanged -= Watcher_DeviceListChanged;
        _watcher.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _settingsWindow?.Close();
    }

    private static string RemovePortSuffix(string value) =>
        Regex.Replace(value ?? string.Empty, @"\s*\(COM\d{1,3}\)$", string.Empty,
            RegexOptions.IgnoreCase).Trim();

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            T? descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
