using System.Management;

namespace USBWatcher.Core
{
    public record UsbDeviceRecord (
        string FriendlyName,
        string DeviceRegKey,
        string VID,
        string PID,
        string PortName,
        string? SerialNumber,
        string? Manufacturer
    );

    public class USBWatcherCore
    {
        const string GUID_DEVCLASS_USB = @"{4d36e978-e325-11ce-bfc1-08002be10318}";
        const string WMI_QUERY = $"SELECT * FROM Win32_PnPEntity WHERE ClassGuid='{GUID_DEVCLASS_USB}'";

        List<UsbDevice> UsbDevicesList = new List<UsbDevice>();

        public event EventHandler<DeviceChangeEventArgs>? DeviceChangeEvent;
        public USBWatcherCore(EventHandler<DeviceChangeEventArgs>? eventHandler)
        {
            DeviceWatcher deviceWatcher = new DeviceWatcher();
            deviceWatcher.DeviceChangeEvent += DeviceWatcher_DeviceChangeEvent;
            deviceWatcher.DeviceChangeEvent += eventHandler;
            QueryUSBSerialPorts();
        }

        private void QueryUSBSerialPorts()
        {
            UsbDevicesList.Clear();
            using (var searcher = new ManagementObjectSearcher(WMI_QUERY))
            {
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
                for (int i = 0; i < ports.Count; i++)
                {
                    string? DevID = ports[i]["DeviceID"].ToString();
                    string? strManufacturer = ports[i]["Manufacturer"].ToString();
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
                }
            }
        }
        private void DeviceWatcher_DeviceChangeEvent(object? sender, DeviceChangeEventArgs e)
        {
            /* Refresh current device list */
            QueryUSBSerialPorts();
        }

        public IReadOnlyList<UsbDeviceRecord> GetUsbDevicesList()
        {
            return UsbDevicesList.Select(d => new UsbDeviceRecord(
                d.FriendlyName,
                d.DeviceRegKey,
                d.VID,
                d.PID,
                d.PortName,
                d.SerialNumber,
                d.Manufacturer
            )).ToList().AsReadOnly();
        }

        public bool SetUSBDeviceFriendlyName(string portName, string newName)
        {
            /* Find the device portName and change its Friendly name */
            foreach (UsbDevice usbdev in UsbDevicesList)
            {
                if (usbdev.PortName == portName)
                {
                    usbdev.FriendlyName = newName + String.Format(" ({0})", portName);
                    return true;
                }
            }
            return false;
        }
        public string GetUSBDeviceFriendlyName(string portName)
        {
            /* Find the device portName and change its Friendly name */
            foreach (UsbDevice usbdev in UsbDevicesList)
            {
                if (usbdev.PortName == portName)
                {
                    return usbdev.FriendlyName;
                }
            }
            throw new Exception($"Device with port name {portName} not found\nPlease Open \"Device Manager\" and reinstall the device.");
        }
    }
}
