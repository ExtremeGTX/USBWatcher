﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace USBWatcher
{
    internal class UsbDevice
    {
        private const string REG_ROOT_KEY = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum";

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
                    string RegKey = DeviceRegKey;
                    Registry.SetValue(RegKey, "FriendlyName", value);
                    _friendlyName = value;
                }
            }
        }
        
        public string DeviceRegKey { get; private set; }
        public string VID { get; set; }
        public string PID { get; set; }
        public string PortName { get; set; }

        private string GetFriendlyName(string deviceId)
        {
            string RegKey = DeviceRegKey;
            string? value = (string?)Registry.GetValue(RegKey, "FriendlyName", "Unknown");
            if (value == null)
            {
                return "Unkown";
            }

            return value;
        }

        private string GetPortName(string deviceId)
        {
            string RegKey = $"{DeviceRegKey}\\Device Parameters";
            string? value = (string?)Registry.GetValue(RegKey, "PortName", "Unknown");
            if (value == null)
            {
                return "Unkown";
            }

            return value;
        }
        private string ParseDeviceID(string pattern, string deviceId)
        {
            Match m = Regex.Match(deviceId, pattern);
            return m.Groups[2].Value;
        }

        private string GetPID(string deviceId)
        {
            string pid_pattern = @"(PID_)([0-9a-fA-F]+)";
            return ParseDeviceID(pid_pattern, deviceId);
        }
        private string GetVID(string deviceId)
        {
            string vid_pattern = @"(VID_)([0-9a-fA-F]+)";
            return ParseDeviceID(vid_pattern, deviceId);
        }

        public UsbDevice(string deviceId)
        {
            string[] patterns = new string[] 
                                { 
                                    @"(USB\\VID_[0-9a-fA-F]+&PID_[0-9a-fA-F]+)",
                                    @"(FTDIBUS\\VID_[0-9a-fA-F]+\+PID_[0-9a-fA-F]+)",
                                };
            foreach (var pattern in patterns)
            {
                Match m = Regex.Match(deviceId, pattern);

                /* Basic validation */
                if (deviceId.Contains(m.Groups[0].Value))
                {
                    DeviceRegKey = $"{REG_ROOT_KEY}\\{deviceId}";
                    PortName = GetPortName(deviceId);
                    _friendlyName = GetFriendlyName(deviceId);
                    VID = GetVID(deviceId);
                    PID = GetPID(deviceId);
                    return;
                }
            }

            /* Failed to get device data, throw exception */
            throw new ArgumentException("Invalid DeviceID");
        }
    }
}
