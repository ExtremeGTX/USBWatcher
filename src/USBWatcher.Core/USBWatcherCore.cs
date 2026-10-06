using System.Management;
using System.Runtime.InteropServices;

namespace USBWatcher.Core
{
    public record UsbDeviceRecord (
        string FriendlyName,
        string DeviceRegKey,
        string VID,
        string PID,
        string PortName,
        string SerialNumber,
        string Manufacturer,
        string MI,
        string DeviceName
    );

    public sealed class USBWatcherCore : IDisposable
    {
        const string GUID_DEVCLASS_PORTS = @"{4d36e978-e325-11ce-bfc1-08002be10318}";
        const string WMI_QUERY = $"SELECT * FROM Win32_PnPEntity WHERE ClassGuid='{GUID_DEVCLASS_PORTS}'";

        readonly List<UsbDevice> UsbDevicesList = new List<UsbDevice>();
        readonly DeviceWatcher deviceWatcher;
        readonly object devicesLock = new();
        int deviceRefreshRequested;
        int deviceRefreshWorkerRunning;
        volatile bool disposed;

        public event EventHandler? DeviceListChanged;

        public USBWatcherCore(
            EventHandler<DeviceChangeEventArgs>? eventHandler,
            DeviceMonitoringScope monitoringScope = DeviceMonitoringScope.ExternalUsbDevicesOnly)
        {
            deviceWatcher = new DeviceWatcher(monitoringScope);
            deviceWatcher.DeviceChangeEvent += DeviceWatcher_DeviceChangeEvent;
            deviceWatcher.DeviceChangeEvent += eventHandler;
            QueryUSBSerialPorts();
        }

        private void QueryUSBSerialPorts()
        {
            var refreshedDevices = new List<UsbDevice>();
            using (var searcher = new ManagementObjectSearcher(WMI_QUERY))
            {
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
                for (int i = 0; i < ports.Count; i++)
                {
                    string? DevID = ports[i]["DeviceID"]?.ToString();
                    if (DevID == null)
                        continue;

                    refreshedDevices.Add(new UsbDevice(DevID));
                }
            }

            lock (devicesLock)
            {
                UsbDevicesList.Clear();
                UsbDevicesList.AddRange(refreshedDevices);
            }
        }
        private void DeviceWatcher_DeviceChangeEvent(object? sender, DeviceChangeEventArgs e)
        {
            RequestDeviceListRefresh();
        }

        private void RequestDeviceListRefresh()
        {
            if (disposed)
            {
                return;
            }

            Interlocked.Exchange(ref deviceRefreshRequested, 1);
            if (Interlocked.CompareExchange(ref deviceRefreshWorkerRunning, 1, 0) == 0)
            {
                _ = Task.Run(ProcessDeviceListRefreshesAsync);
            }
        }

        private async Task ProcessDeviceListRefreshesAsync()
        {
            try
            {
                while (!disposed && Interlocked.Exchange(ref deviceRefreshRequested, 0) == 1)
                {
                    // Let the port function finish registering, without delaying the
                    // device event or blocking readers of the current snapshot.
                    await Task.Delay(75).ConfigureAwait(false);
                    if (disposed)
                    {
                        break;
                    }

                    try
                    {
                        QueryUSBSerialPorts();
                        if (!disposed)
                        {
                            DeviceListChanged?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    catch (ManagementException)
                    {
                        // A later PnP notification will request another refresh.
                    }
                    catch (COMException)
                    {
                        // WMI can be temporarily unavailable during enumeration.
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref deviceRefreshWorkerRunning, 0);
                if (!disposed && Volatile.Read(ref deviceRefreshRequested) != 0)
                {
                    RequestDeviceListRefresh();
                }
            }
        }

        public IReadOnlyList<UsbDeviceRecord> GetUsbDevicesList()
        {
            lock (devicesLock)
            {
                return UsbDevicesList.Select(d => new UsbDeviceRecord(
                    d.FriendlyName,
                    d.DeviceRegKey,
                    d.VID,
                    d.PID,
                    d.PortName,
                    d.SerialNumber,
                    d.Manufacturer,
                    d.MI,
                    d.DeviceName
                )).ToList().AsReadOnly();
            }
        }

        public void SetMonitoringScope(DeviceMonitoringScope monitoringScope)
        {
            deviceWatcher.SetMonitoringScope(monitoringScope);
        }

        public bool SetUSBDeviceFriendlyName(string portName, string newName)
        {
            /* Find the device portName and change its Friendly name */
            lock (devicesLock)
            {
                foreach (UsbDevice usbdev in UsbDevicesList)
                {
                    if (usbdev.PortName == portName)
                    {
                        usbdev.FriendlyName = newName + String.Format(" ({0})", portName);
                        return true;
                    }
                }
            }
            return false;
        }
        public string GetUSBDeviceFriendlyName(string portName)
        {
            /* Find the device portName and change its Friendly name */
            lock (devicesLock)
            {
                foreach (UsbDevice usbdev in UsbDevicesList)
                {
                    if (usbdev.PortName == portName)
                    {
                        return usbdev.FriendlyName;
                    }
                }
            }
            throw new Exception($"Device with port name {portName} not found\nPlease Open \"Device Manager\" and reinstall the device.");
        }

        public void Dispose()
        {
            disposed = true;
            deviceWatcher.DeviceChangeEvent -= DeviceWatcher_DeviceChangeEvent;
            deviceWatcher.Dispose();
            DeviceListChanged = null;
        }
    }
}
