using System.ComponentModel;
using System.Management;
using System.Reflection;
using System.Windows.Forms;
using USBWatcher.Properties;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using USBWatcher.Core;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

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
            ToolStripItem stripItemOpen = new ToolStripMenuItem("&Open");
            ToolStripItem stripItemExit = new ToolStripMenuItem("E&xit");
            stripItemOpen.Click += TrayIcon_Click;
            stripItemExit.Click += stripItemExit_Click;
            contextMenuStrip.Items.AddRange(new ToolStripItem[] { stripItemOpen, stripItemExit });

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
                };

                var storedFriendlyName = Settings.GetDeviceName(usbdev.VID, usbdev.PID);
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
                string newFriendlyName = e.Label;
                string? portName = lsvDevices.SelectedItems[0].SubItems[1].Text;
                string? VID = lsvDevices.SelectedItems[0].SubItems[2].Text;
                string? PID = lsvDevices.SelectedItems[0].SubItems[3].Text;

                /* Make sure the user entered friendlyname doesn't contain (COMx) string 
                 * COMx is automatically appended by SetUSBDeviceFriendlyName
                 */
                if (Regex.IsMatch(newFriendlyName, @"\(COM[0-9]{1,3}\)$"))
                {
                    newFriendlyName = Regex.Replace(newFriendlyName, @"\(COM[0-9]{1,3}\)$", "").Trim();
                }

                if (usb_watcher.SetUSBDeviceFriendlyName(portName, e.Label))
                {
                    Settings.SaveDeviceName(VID, PID, newFriendlyName);
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
    }
}
