namespace USBWatcher.Core
{
    public class DeviceChangeEventArgs : EventArgs
    {
        public String Description { get; set; } = "None";
        public String DeviceID { get; set; } = "UnkownID";
        public Boolean Present { get; set; } = false;
        public DeviceChangeEventArgs() { }
    }
}
