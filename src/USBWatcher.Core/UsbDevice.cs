using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace USBWatcher.Core
{
    internal class UsbDevice
    {
        private const string REG_ROOT_KEY = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum";
        private const uint CR_SUCCESS = 0x00000000;
        private const uint CR_BUFFER_SMALL = 0x0000001A;

        private static readonly DEVPROPKEY DEVPKEY_Device_DeviceDesc = new(
            new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);

        private static readonly DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc = new(
            new Guid("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"), 4);

        private string _friendlyName;
        public string FriendlyName
        {
            get
            {
                return _friendlyName;
            }
            set
            {
                if (value != null)
                {
                    Registry.SetValue(DeviceRegKey, "FriendlyName", value);
                    _friendlyName = value;
                }
            }
        }

        public string DeviceRegKey { get; private set; }
        public string VID { get; set; }
        public string PID { get; set; }
        public string PortName { get; set; }
        public string SerialNumber { get; set; }
        public string Manufacturer { get; set; }
        public string MI { get; set; }
        public string DeviceName { get; private set; }

        private string GetFriendlyName(string deviceId)
        {
            string? value = (string?)Registry.GetValue(DeviceRegKey, "FriendlyName", "Unknown");
            return value ?? "Unknown";
        }

        private string GetPortName(string deviceId)
        {
            string regKey = $"{DeviceRegKey}\\Device Parameters";
            string? value = (string?)Registry.GetValue(regKey, "PortName", "Unknown");
            return value ?? "Unknown";
        }

        private string GetManufacturer(string deviceId)
        {
            string? value = (string?)Registry.GetValue(DeviceRegKey, "Mfg", "");
            if (value == null)
            {
                return "";
            }

            return value[(value.LastIndexOf(';') + 1)..];
        }

        private string ParseDeviceID(string pattern, string deviceId)
        {
            Match match = Regex.Match(deviceId, pattern);
            return match.Groups[2].Value;
        }

        private string GetPID(string deviceId)
        {
            return ParseDeviceID(@"(PID_)([0-9a-fA-F]+)", deviceId);
        }

        private string GetVID(string deviceId)
        {
            return ParseDeviceID(@"(VID_)([0-9a-fA-F]+)", deviceId);
        }

        private string GetFTDISerialNumber(string deviceId)
        {
            string? serialNumber = deviceId[(deviceId.LastIndexOf('+') + 1)..]?.Split("\\", StringSplitOptions.None)[0];
            return serialNumber ?? "";
        }

        private string GetMI(string deviceId)
        {
            return ParseDeviceID(@"(MI_)(\d{2})", deviceId);
        }

        private string GetDeviceName(string deviceId)
        {
            string description = GetDeviceProperty(deviceId, DEVPKEY_Device_BusReportedDeviceDesc)
                ?? GetDeviceProperty(deviceId, DEVPKEY_Device_DeviceDesc)
                ?? Registry.GetValue(DeviceRegKey, "DeviceDesc", null)?.ToString()
                ?? GetFriendlyName(deviceId);

            // Registry DeviceDesc values can be indirect resource strings such as
            // "@usbser.inf,%devicedesc%;USB Serial Device".
            int resourceSeparator = description.LastIndexOf(';');
            if (description.StartsWith('@') && resourceSeparator >= 0)
            {
                description = description[(resourceSeparator + 1)..];
            }

            return Regex.Replace(description, @"\s*\(COM\d{1,3}\)$", string.Empty,
                RegexOptions.IgnoreCase).Trim();
        }

        private static string? GetDeviceProperty(string deviceId, DEVPROPKEY propertyKey)
        {
            if (CM_Locate_DevNodeW(out uint devInst, deviceId, 0) != CR_SUCCESS)
            {
                return null;
            }

            uint bufferSize = 0;
            uint result = CM_Get_DevNode_PropertyW(
                devInst,
                ref propertyKey,
                out _,
                IntPtr.Zero,
                ref bufferSize,
                0);
            if (result != CR_BUFFER_SMALL || bufferSize < sizeof(char))
            {
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                result = CM_Get_DevNode_PropertyW(
                    devInst,
                    ref propertyKey,
                    out _,
                    buffer,
                    ref bufferSize,
                    0);
                return result == CR_SUCCESS
                    ? Marshal.PtrToStringUni(buffer)?.TrimEnd('\0')
                    : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public UsbDevice(string deviceId)
        {
            string[] patterns =
            {
                @"(USB\\VID_[0-9a-fA-F]+&PID_[0-9a-fA-F]+)",
                @"(FTDIBUS\\VID_[0-9a-fA-F]+\+PID_[0-9a-fA-F]+)",
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(deviceId, pattern);

                if (deviceId.Contains(match.Groups[0].Value))
                {
                    DeviceRegKey = $"{REG_ROOT_KEY}\\{deviceId}";
                    PortName = GetPortName(deviceId);
                    _friendlyName = GetFriendlyName(deviceId);
                    DeviceName = GetDeviceName(deviceId);
                    VID = GetVID(deviceId);
                    PID = GetPID(deviceId);
                    MI = GetMI(deviceId);
                    Manufacturer = GetManufacturer(deviceId);
                    SerialNumber = deviceId.Contains("FTDI")
                        ? GetFTDISerialNumber(deviceId)
                        : "";
                    return;
                }
            }

            throw new ArgumentException("Invalid DeviceID");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public DEVPROPKEY(Guid formatId, uint propertyId)
            {
                FormatId = formatId;
                PropertyId = propertyId;
            }

            public Guid FormatId;
            public uint PropertyId;
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Locate_DevNodeW(
            out uint devInst,
            string deviceId,
            uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_DevNode_PropertyW(
            uint devInst,
            ref DEVPROPKEY propertyKey,
            out uint propertyType,
            IntPtr propertyBuffer,
            ref uint propertyBufferSize,
            uint flags);
    }
}
