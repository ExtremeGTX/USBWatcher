using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using USBWatcher.Core;

namespace USBWatcher
{
    public partial class Main : Form
    {
        private USBWatcherCore usb_watcher;
        private NotifyIcon? trayIcon;
        private bool MinimizeOnStart = false;
        private ToolStripMenuItem? externalUsbDevicesOnlyMenuItem;
        private ToolStripMenuItem? allDevicesMenuItem;

        public Main(bool minimized)
        {
            InitializeComponent();
            InitializeTrayIcon();

            Settings.Load();
            InitializeDeviceMonitoringMenu();
            DeviceMonitoringScope monitoringScope = Settings.Current.ListenToAllDevices
                ? DeviceMonitoringScope.AllDevices
                : DeviceMonitoringScope.ExternalUsbDevicesOnly;
            usb_watcher = new USBWatcherCore(DeviceWatcher_DeviceChangeEvent, monitoringScope);
            RefreshUSBPortsList();

            if (minimized)
            {
                MinimizeOnStart = true;
            }
        }

        private void DeviceWatcher_DeviceChangeEvent(object? sender, DeviceChangeEventArgs e)
        {
            this.Invoke(delegate
            {
                AddDeviceEvents(e);

                RefreshUSBPortsList();
            });
        }

        private void AddDeviceEvents(DeviceChangeEventArgs e)
        {
            IReadOnlyList<DeviceChangeGroup> groups = e.Groups.Count > 0
                ? e.Groups
                : new[]
                {
                    new DeviceChangeGroup(
                        e.DeviceID,
                        e.Description,
                        new[] { new DeviceChangeNode(e.DeviceID, e.Description, e.Present, null) })
                };

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            foreach (DeviceChangeGroup group in groups)
            {
                bool hasInsertions = group.Nodes.Any(node => node.Changed && node.Present);
                bool hasRemovals = group.Nodes.Any(node => node.Changed && !node.Present);
                var groupNode = new TreeNode($"{timestamp}  {group.Description}")
                {
                    Name = group.DeviceID,
                    ToolTipText = group.DeviceID,
                    BackColor = hasInsertions && hasRemovals
                        ? Color.LightYellow
                        : hasInsertions ? Color.LightGreen : Color.LightPink
                };

                var descendantParentIds = group.Nodes
                    .Where(node => !string.IsNullOrEmpty(node.ParentDeviceID))
                    .Select(node => node.ParentDeviceID!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (DeviceChangeNode change in group.Nodes)
                {
                    if (string.Equals(change.DeviceID, group.DeviceID, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // The physical event device is the single displayed root. Omit any
                    // intermediate descendant roots and promote their leaf devices to it.
                    if (descendantParentIds.Contains(change.DeviceID))
                    {
                        continue;
                    }

                    var changeNode = new TreeNode(change.Description)
                    {
                        Name = change.DeviceID,
                        ToolTipText = change.DeviceID,
                        BackColor = change.Changed
                            ? change.Present ? Color.LightGreen : Color.LightPink
                            : SystemColors.Window
                    };
                    groupNode.Nodes.Add(changeNode);
                }

                tvwEvents.Nodes.Add(groupNode);
                groupNode.ExpandAll();
                tvwEvents.SelectedNode = groupNode;
                groupNode.EnsureVisible();
            }
        }

        void RefreshUSBPortsList()
        {
            lsvDevices.Items.Clear();
            IReadOnlyList<UsbDeviceRecord> UsbDevicesList = usb_watcher.GetUsbDevicesList();

            foreach (UsbDeviceRecord usbdev in UsbDevicesList)
            {
                string subID = string.IsNullOrEmpty(usbdev.SerialNumber) ? usbdev.MI : usbdev.SerialNumber;
                string[] DevInfo = new string[]
                {
                    usbdev.FriendlyName,
                    usbdev.PortName,
                    usbdev.VID,
                    usbdev.PID,
                    subID
                };

                var storedFriendlyName = Settings.GetStoredDeviceName(usbdev.VID, usbdev.PID, subID);
                if (!string.IsNullOrEmpty(storedFriendlyName))
                {
                    usb_watcher.SetUSBDeviceFriendlyName(usbdev.PortName, storedFriendlyName);
                    DevInfo[0] = storedFriendlyName;
                }

                ListViewItem lvi = new ListViewItem(DevInfo);
                lsvDevices.Items.Add(lvi);
            }

            refresh_trayiconText();
        }

        #region "Form events"
        private void Main_FormShown(object sender, EventArgs e)
        {
            if (MinimizeOnStart)
            {
                this.Hide();
            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            /* Close to systray */
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = true;
                }
                this.Hide();
                e.Cancel = true;
            }
        }

        private void Main_FormClosed(object? sender, FormClosedEventArgs e)
        {
            usb_watcher.Dispose();
            trayIcon?.Dispose();
        }
        #endregion

        #region "TrayIcon"
        private void InitializeTrayIcon()
        {
            ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
            ToolStripLabel ToolStripLabelSettings = new ToolStripLabel("&Settings") { Enabled = false };
            ToolStripMenuItem stripItemStartUp = new ToolStripMenuItem("&Start USBWatcher with Windows") { CheckOnClick = true };
            ToolStripSeparator separator = new ToolStripSeparator();
            ToolStripItem stripItemOpen = new ToolStripMenuItem("&Show/Hide");
            ToolStripItem stripItemExit = new ToolStripMenuItem("E&xit");

            stripItemOpen.Click += TrayIcon_Click;
            stripItemExit.Click += stripItemExit_Click;
            stripItemStartUp.Click += stripItemStartUp_Click;

            contextMenuStrip.Items.AddRange(new ToolStripItem[] { ToolStripLabelSettings, stripItemStartUp, separator, stripItemOpen, stripItemExit });

            stripItemStartUp.Checked = CheckStartupTaskStatus();

            trayIcon = new NotifyIcon()
            {
                Icon = this.Icon,
                Visible = true,
            };
            trayIcon.Click += TrayIcon_Click;
            trayIcon.ContextMenuStrip = contextMenuStrip;
        }

        private void refresh_trayiconText()
        {
            if (trayIcon is null)
            {
                return;
            }

            string myText = this.Text;
            //foreach (UsbDevice usbdev in UsbDevicesList)
            //{
            //    myText += Environment.NewLine + usbdev.PortName + ": " + usbdev.FriendlyName ;
            //}

            /// https://stackoverflow.com/questions/579665/how-can-i-show-a-systray-tooltip-longer-than-63-chars
            //Type t = typeof(NotifyIcon);
            //BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;

            //t.GetField("text", hidden).SetValue(trayIcon, myText);
            //if ((bool)t.GetField("added", hidden).GetValue(trayIcon))
            //    t.GetMethod("UpdateIcon", hidden).Invoke(trayIcon, new object[] { true });
        }

        private void TrayIcon_Click(object? sender, EventArgs e)
        {
            /* Act only on left click */
            if (e is MouseEventArgs mouseEvent && mouseEvent.Button != MouseButtons.Left)
            {
                return;
            }

            /* show/hide main window from systray icon */
            switch (this.Visible)
            {
                case true:
                    this.Hide();
                    break;
                case false:
                    this.SetDesktopLocation(MousePosition.X - this.Width / 2, MousePosition.Y - this.Height - 20);
                    this.Show();
                    this.Activate();
                    break;
            }
        }

        private void stripItemExit_Click(object? sender, EventArgs e)
        {
            usb_watcher.Dispose();
            trayIcon?.Dispose();
            this.Dispose();
            Application.Exit();
        }

        private void stripItemStartUp_Click(object? sender, EventArgs e)
        {
            ToolStripMenuItem stripItemStartUp = (sender as ToolStripMenuItem)!;
            if (stripItemStartUp.Checked)
            {
                CreateStartupTask();
            }
            else
            {
                RemoveStartupTask();
            }
        }
        #endregion

        #region "Windows Startup task"
        public void CreateStartupTask()
        {
            string taskName = "USBWatcher_AutoStart";
            string exePath = Application.ExecutablePath;

            using (TaskService ts = new TaskService())
            {
                // Create a new task definition and assign properties
                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "Starts USBWatcher with Admin rights at user logon";
                td.Principal.RunLevel = TaskRunLevel.Highest; // Run with highest privileges (admin)
                td.Principal.LogonType = TaskLogonType.InteractiveToken; // Only when user is logged on

                // Create a trigger that starts at logon of any user
                td.Triggers.Add(new LogonTrigger());

                // Create an action that runs your executable
                td.Actions.Add(new ExecAction(exePath, "--minimized", Path.GetDirectoryName(exePath)));

                // Register the task in the root folder
                ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, null, null, TaskLogonType.InteractiveToken, null);
            }
        }

        public void RemoveStartupTask()
        {
            string taskName = "USBWatcher_AutoStart";

            using (TaskService ts = new TaskService())
            {
                // Check if the task exists
                var task = ts.GetTask(taskName);
                if (task != null)
                {
                    ts.RootFolder.DeleteTask(taskName);
                    Console.WriteLine("Scheduled task removed successfully.");
                }
                else
                {
                    Console.WriteLine("Scheduled task not found.");
                }
            }
        }
        public bool CheckStartupTaskStatus()
        {
            string taskName = "USBWatcher_AutoStart";

            using (TaskService ts = new TaskService())
            {
                // Check if the task exists
                var task = ts.GetTask(taskName);
                if (task != null)
                {
                    return true;
                }
                return false;
            }
        }
        #endregion

        #region "ToolStrip menu"
        private void InitializeDeviceMonitoringMenu()
        {
            var openSettingsFileMenuItem = new ToolStripMenuItem("Open settings file");
            openSettingsFileMenuItem.Click += settingsToolStripMenuItem_Click;

            externalUsbDevicesOnlyMenuItem = new ToolStripMenuItem("External USB devices only") { CheckOnClick = true };
            allDevicesMenuItem = new ToolStripMenuItem("All devices") { CheckOnClick = true };

            externalUsbDevicesOnlyMenuItem.Click += (_, _) => SetDeviceMonitoringScope(DeviceMonitoringScope.ExternalUsbDevicesOnly);
            allDevicesMenuItem.Click += (_, _) => SetDeviceMonitoringScope(DeviceMonitoringScope.AllDevices);

            var listenForMenuItem = new ToolStripMenuItem("Listen for");
            listenForMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                externalUsbDevicesOnlyMenuItem,
                allDevicesMenuItem
            });

            settingsToolStripMenuItem.DropDownItems.Add(openSettingsFileMenuItem);
            settingsToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            settingsToolStripMenuItem.DropDownItems.Add(listenForMenuItem);
            UpdateDeviceMonitoringMenuChecks();
        }

        private void SetDeviceMonitoringScope(DeviceMonitoringScope monitoringScope)
        {
            usb_watcher.SetMonitoringScope(monitoringScope);
            Settings.Current.ListenToAllDevices = monitoringScope == DeviceMonitoringScope.AllDevices;
            Settings.Save();
            UpdateDeviceMonitoringMenuChecks();
        }

        private void UpdateDeviceMonitoringMenuChecks()
        {
            if (externalUsbDevicesOnlyMenuItem != null)
            {
                externalUsbDevicesOnlyMenuItem.Checked = !Settings.Current.ListenToAllDevices;
            }

            if (allDevicesMenuItem != null)
            {
                allDevicesMenuItem.Checked = Settings.Current.ListenToAllDevices;
            }
        }

        private void settingsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            /* Open settings file in notepad */
            string settingsPath = Settings.GetSettingsPath();
            if (File.Exists(settingsPath))
            {
                Process.Start(new ProcessStartInfo("notepad.exe", settingsPath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show($"Settings file not found: {settingsPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void clearLogsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tvwEvents.Nodes.Clear();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionString = version != null ?
                $"v{version.Major}.{version.Minor}.{version.Build}" : "";

            MessageBox.Show(
                $"USB Watcher {versionString}\nby Mohamed ElShahawi\nhttps://github.com/ExtremeGTX/USBWatcher",
                "About USB Watcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        #endregion

        #region "Devices list"
        private void lsvDevices_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.Label == null || e.Label == "")
            {
                e.CancelEdit = true;
            }
            else
            {
                if (lsvDevices.SelectedItems.Count != 1)
                {
                    return;
                }

                string newFriendlyName = e.Label;
                string? portName = lsvDevices.SelectedItems[0].SubItems[1].Text;
                string? VID = lsvDevices.SelectedItems[0].SubItems[2].Text;
                string? PID = lsvDevices.SelectedItems[0].SubItems[3].Text;
                string? SN = lsvDevices.SelectedItems[0].SubItems[4].Text;

                /* Make sure the user entered friendlyname doesn't contain (COMx) string
                 * COMx is automatically appended by SetUSBDeviceFriendlyName
                 */
                if (Regex.IsMatch(newFriendlyName, @"\(COM[0-9]{1,3}\)$"))
                {
                    newFriendlyName = Regex.Replace(newFriendlyName, @"\(COM[0-9]{1,3}\)$", "").Trim();
                }

                if (usb_watcher.SetUSBDeviceFriendlyName(portName, e.Label))
                {
                    Settings.SaveDeviceName(VID, PID, SN, newFriendlyName);
                }

                //refresh_trayiconText();
            }
        }

        private void lsvDevices_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.F2 && lsvDevices.SelectedItems.Count > 0)
            {
                /* Make sure the user can't edit COMx part in friendlyname
                 * COMx is automatically appended by USBWatcher SetUSBDeviceFriendlyName
                 */
                if (Regex.IsMatch(lsvDevices.SelectedItems[0].Text, @"\(COM[0-9]{1,3}\)$"))
                {
                    lsvDevices.SelectedItems[0].Text = Regex.Replace(lsvDevices.SelectedItems[0].Text, @"\(COM[0-9]{1,3}\)$", "").Trim();
                }

                lsvDevices.SelectedItems[0].BeginEdit();
            }
        }

        // Define static locals for tracking sort state
        int lsvDevices_sortColumn = -1;
        SortOrder lsvDevices_sortOrder = SortOrder.None;
        private void lsvDevices_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Only allow sorting for columns 0 and 1 (FriendlyName and PortName)
            if (e.Column > 1)
                return;

            // Determine new sort order
            if (lsvDevices_sortColumn == e.Column)
            {
                // Same column as last sort; toggle the sort order
                lsvDevices_sortOrder = lsvDevices_sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                // New column; sort ascending by default
                lsvDevices_sortColumn = e.Column;
                lsvDevices_sortOrder = SortOrder.Ascending;
            }

            // Perform the sort
            lsvDevices.BeginUpdate();
            try
            {
                var items = lsvDevices.Items.Cast<ListViewItem>().ToList();
                items.Sort((x, y) =>
                {
                    string textX = lsvDevices_sortColumn == 0 ? x.Text : x.SubItems[lsvDevices_sortColumn].Text;
                    string textY = lsvDevices_sortColumn == 0 ? y.Text : y.SubItems[lsvDevices_sortColumn].Text;

                    int result;
                    if (lsvDevices_sortColumn == 1) // PortName column - sort COM ports numerically
                    {
                        var matchX = Regex.Match(textX, @"COM(\d+)");
                        var matchY = Regex.Match(textY, @"COM(\d+)");

                        if (matchX.Success && matchY.Success)
                        {
                            int numX = int.Parse(matchX.Groups[1].Value);
                            int numY = int.Parse(matchY.Groups[1].Value);
                            result = numX.CompareTo(numY);
                        }
                        else
                        {
                            result = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    else // FriendlyName column - simple string comparison
                    {
                        result = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
                    }

                    return lsvDevices_sortOrder == SortOrder.Ascending ? result : -result;
                });

                lsvDevices.Items.Clear();
                lsvDevices.Items.AddRange(items.ToArray());
            }
            finally
            {
                lsvDevices.EndUpdate();
            }
        }
        #endregion

        #region "Notes"
        /// Notes:
        /// Check: https://docs.microsoft.com/en-us/windows-hardware/drivers/install/system-defined-device-setup-classes-available-to-vendors
        /// SELECT * FROM Win32_PnPEntity WHERE ClassGuid="{4d36e978-e325-11ce-bfc1-08002be10318}"   for COM Ports
        /// Modify and save names straight in the register @ HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\USB\<VID_xxxx&PID_xxxx>\<instanceID> "FriendlyName"
        #endregion

    }
}
