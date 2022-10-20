using System.ComponentModel;
using System.Management;
using System.Reflection;
using System.Windows.Forms;

using USBWatcher.Properties;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace USBWatcher
{
    public partial class Main : Form
    {
        const string GUID_DEVCLASS_USB = @"{4d36e978-e325-11ce-bfc1-08002be10318}";
        const string WMI_QUERY = $"SELECT * FROM Win32_PnPEntity WHERE ClassGuid='{GUID_DEVCLASS_USB}'";
        List<UsbDevice> UsbDevicesList = new List<UsbDevice>();

        private NotifyIcon? trayIcon;
        public Main()
        {
            InitializeComponent();
            InitializeTrayIcon();

            GetUSBPorts();
            DeviceWatcher deviceWatcher = new DeviceWatcher();
            deviceWatcher.DeviceChangeEvent += DeviceWatcher_DeviceChangeEvent;
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
                lsvEvents.Items.Add(lvi);

                /* Refresh current device list */
                GetUSBPorts();
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
        void GetUSBPorts()
        {
            UsbDevicesList.Clear();
            lsvDevices.Items.Clear();
            using (var searcher = new ManagementObjectSearcher(WMI_QUERY))
            {
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
                for (int i = 0; i < ports.Count; i++)
                {
                    string? DevID = ports[i]["DeviceID"].ToString();
                    if (DevID == null)
                        continue;

                    UsbDevice usbdev = new UsbDevice(DevID);
                    UsbDevicesList.Add(usbdev);
                    string?[] DevInfo = new string?[]
                    {
                        usbdev.FriendlyName,
                        usbdev.PortName,
                        usbdev.VID,
                        usbdev.PID,
                    };
                    ListViewItem lvi = new ListViewItem(DevInfo);
                    lsvDevices.Items.Add(lvi);
                }
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
                UsbDevicesList[e.Item].FriendlyName = e.Label;
                refresh_trayiconText();
            }
        }

        private void refresh_trayiconText()
        {
            if (trayIcon is null)
            {
                return;
            }

            string myText = this.Text;
            foreach (UsbDevice usbdev in UsbDevicesList)
            {
                myText += Environment.NewLine + usbdev.PortName + ": " + usbdev.FriendlyName ;
            }

            /// https://stackoverflow.com/questions/579665/how-can-i-show-a-systray-tooltip-longer-than-63-chars
            Type t = typeof(NotifyIcon);
            BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;

            t.GetField("text", hidden).SetValue(trayIcon, myText);
            if ((bool)t.GetField("added", hidden).GetValue(trayIcon))
                t.GetMethod("UpdateIcon", hidden).Invoke(trayIcon, new object[] { true });


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
            MessageBox.Show("USB Watcher by Mohamed ElShahawi\nhttps://github.com/ExtremeGTX/USBWatcher");
        }

        private void clearLogsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lsvEvents.Items.Clear();
        }
    }

}