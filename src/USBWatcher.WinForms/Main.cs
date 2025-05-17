using Microsoft.Win32.TaskScheduler;
using System.Reflection;
using System.Text.RegularExpressions;
using USBWatcher.Core;

namespace USBWatcher
{
    public partial class Main : Form
    {
        private USBWatcherCore usb_watcher;
        private NotifyIcon? trayIcon;

        public Main()
        {
            InitializeComponent();
            InitializeTrayIcon();

            Settings.Load();
            usb_watcher = new USBWatcherCore(DeviceWatcher_DeviceChangeEvent);
            RefreshUSBPortsList();
        }

        private void InitializeTrayIcon()
        {
            ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
            ToolStripLabel ToolStripLabelSettings = new ToolStripLabel("&Settings") { Enabled = false };
            ToolStripMenuItem stripItemStartUp = new ToolStripMenuItem("&Start USBWatcher with Windows") { CheckOnClick = true };
            ToolStripSeparator separator = new ToolStripSeparator();
            ToolStripItem stripItemOpen = new ToolStripMenuItem("&Open");
            ToolStripItem stripItemExit = new ToolStripMenuItem("E&xit");

            stripItemOpen.Click += TrayIcon_Click;
            stripItemExit.Click += stripItemExit_Click;
            stripItemStartUp.Click += stripItemStartUp_Click;

            contextMenuStrip.Items.AddRange(new ToolStripItem[] { ToolStripLabelSettings, stripItemStartUp, separator, stripItemOpen, stripItemExit });

            stripItemStartUp.Checked = CheckStartupTask();

            trayIcon = new NotifyIcon()
            {
                Icon = this.Icon,
                Visible = true,
            };
            trayIcon.Click += TrayIcon_Click;
            trayIcon.ContextMenuStrip = contextMenuStrip;
        }

        private void stripItemExit_Click(object? sender, EventArgs e)
        {
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

        private void DeviceWatcher_DeviceChangeEvent(object? sender, DeviceChangeEventArgs e)
        {
            this.Invoke(delegate
            {
                /* Log device event */
                string[] DevInfo = new string[]
                            {
                                DateTime.Now.ToString("hh:mm:ss"),
                                e.Description,
                                e.Present ? "Inserted" : "Removed"
                            };

                ListViewItem lvi = new ListViewItem(DevInfo);
                lvi.BackColor = e.Present ? Color.LightGreen : Color.LightPink;

                lsvEvents.Items.Add(lvi);

                RefreshUSBPortsList();
            });
        }

        private void TrayIcon_Click(object? sender, EventArgs e)
        {
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

        void RefreshUSBPortsList()
        {
            lsvDevices.Items.Clear();
            IReadOnlyList<UsbDeviceRecord> UsbDevicesList = usb_watcher.GetUsbDevicesList();

            foreach (UsbDeviceRecord usbdev in UsbDevicesList)
            {
                string[] DevInfo = new string[]
                {
                    usbdev.FriendlyName,
                    usbdev.PortName,
                    usbdev.VID,
                    usbdev.PID,
                    usbdev.SerialNumber,
                };

                var storedFriendlyName = Settings.GetStoredDeviceName(usbdev.VID, usbdev.PID, usbdev.SerialNumber);
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
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            /* Close to systray */
            if (e.CloseReason == CloseReason.UserClosing)
            {
                trayIcon.Visible = true;
                this.Hide();
                e.Cancel = true;
            }
        }

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
        #region "Notes"
        /// Notes:
        /// Check: https://docs.microsoft.com/en-us/windows-hardware/drivers/install/system-defined-device-setup-classes-available-to-vendors
        /// SELECT * FROM Win32_PnPEntity WHERE ClassGuid="{4d36e978-e325-11ce-bfc1-08002be10318}"   for COM Ports
        /// Modify and save names straight in the register @ HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\USB\<VID_xxxx&PID_xxxx>\<instanceID> "FriendlyName"
        #endregion

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

        private void clearLogsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lsvEvents.Items.Clear();
        }

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
                td.Actions.Add(new ExecAction(exePath, null, Path.GetDirectoryName(exePath)));

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
        public bool CheckStartupTask()
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

    }
}
