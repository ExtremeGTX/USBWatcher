﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace USBWatcher
{
    class DeviceChangeEventArgs : EventArgs
    {
        public String Description { get; set; } = "None";
        public String DeviceID { get; set; } = "UnkownID";
        public Boolean Present { get; set; } = false;
        public DeviceChangeEventArgs() { }
    }
    internal class DeviceWatcher
    {
        public event EventHandler<DeviceChangeEventArgs>? DeviceChangeEvent;

        public DeviceWatcher()
        {
            StartWatcher();
        }
        private void StartWatcher()
        {
            var query = new WqlEventQuery()
            {
                EventClassName = "__InstanceOperationEvent",
                WithinInterval = new TimeSpan(0, 0, 1),
                Condition = @"TargetInstance ISA 'Win32_PnPEntity'"
            };

            var scope = new ManagementScope("root\\CIMV2");
            using (var moWatcher = new ManagementEventWatcher(scope, query))
            {
                moWatcher.Options.Timeout = ManagementOptions.InfiniteTimeout;
                moWatcher.EventArrived += new EventArrivedEventHandler(DeviceChangedEvent);
                moWatcher.Start();
            }
        }
        private void DeviceChangedEvent(object sender, EventArrivedEventArgs e)
        {


            using (var moBase = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value)
            {
                string devicePNPId = moBase.Properties["PNPDeviceID"].Value?.ToString() ?? String.Empty;
                string deviceDescription = moBase.Properties["Caption"].Value?.ToString() ?? String.Empty;

                DeviceChangeEventArgs deviceChangeEventArgs = new DeviceChangeEventArgs
                {
                    DeviceID = devicePNPId,
                    Description = deviceDescription
                };
#if _0_ 
                if ((string)moBase["ClassGuid"] != GUID_DEVCLASS_USB)
                {
                    return;
                }
#endif         
                switch (e.NewEvent.ClassPath.ClassName)
                {
                    case "__InstanceDeletionEvent":
                        deviceChangeEventArgs.Present = false;
                        break;
                    case "__InstanceCreationEvent":
                        deviceChangeEventArgs.Present = true;
                        break;
                    case "__InstanceModificationEvent": /* TODO: Check how to trigger and test this case! */
                    default:
                        Console.WriteLine(e.NewEvent.ClassPath.ClassName);
                        break;
                }
                DeviceChangeEvent?.Invoke(this, deviceChangeEventArgs);
            }
        }
    }
}
