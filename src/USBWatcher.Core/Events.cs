namespace USBWatcher.Core
{
    public sealed record DeviceChangeNode(
        string DeviceID,
        string Description,
        bool Present,
        string? ParentDeviceID,
        bool Changed = true);

    public sealed record DeviceChangeGroup(
        string DeviceID,
        string Description,
        IReadOnlyList<DeviceChangeNode> Nodes);

    public class DeviceChangeEventArgs : EventArgs
    {
        public String Description { get; set; } = "None";
        public String DeviceID { get; set; } = "UnkownID";
        public Boolean Present { get; set; } = false;
        public IReadOnlyList<DeviceChangeGroup> Groups { get; set; } =
            Array.Empty<DeviceChangeGroup>();
        public DeviceChangeEventArgs() { }
    }
}
