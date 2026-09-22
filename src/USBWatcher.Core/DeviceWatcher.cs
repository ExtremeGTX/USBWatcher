using System.Collections.Concurrent;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace USBWatcher.Core
{
    public enum DeviceMonitoringScope
    {
        ExternalUsbDevicesOnly,
        AllDevices
    }

    internal sealed class DeviceWatcher : IDisposable
    {
        private const uint CR_SUCCESS = 0;
        private const uint CM_NOTIFY_FILTER_FLAG_ALL_INTERFACE_CLASSES = 1;
        private const int CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE = 0;
        private const int CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL = 0;
        private const int CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL = 1;

        private readonly object syncRoot = new();
        private readonly ConcurrentDictionary<string, bool> pendingInterfaceChanges =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> deviceNameCache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly CmNotificationCallback notificationCallback;
        private readonly Timer debounceTimer;

        private Dictionary<string, DeviceSnapshot> deviceTree =
            new(StringComparer.OrdinalIgnoreCase);
        private DeviceMonitoringScope monitoringScope;
        private IntPtr notificationHandle;
        private ManagementEventWatcher? fallbackWatcher;
        private bool nativeWatcherActive;
        private volatile bool disposed;

        internal event EventHandler<DeviceChangeEventArgs>? DeviceChangeEvent;

        internal DeviceWatcher(DeviceMonitoringScope monitoringScope)
        {
            notificationCallback = DeviceNotificationReceived;
            debounceTimer = new Timer(DispatchPendingEvents, null, Timeout.Infinite, Timeout.Infinite);
            SetMonitoringScope(monitoringScope);
        }

        internal void SetMonitoringScope(DeviceMonitoringScope newMonitoringScope)
        {
            lock (syncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                StopWatcher();
                monitoringScope = newMonitoringScope;

                try
                {
                    StartNativeWatcher();
                }
                catch (EntryPointNotFoundException)
                {
                    StartWmiFallback();
                }
                catch (DllNotFoundException)
                {
                    StartWmiFallback();
                }
                catch (InvalidOperationException)
                {
                    StartWmiFallback();
                }
            }
        }

        private void StartNativeWatcher()
        {
            // Always listen to every interface class. USB-only is an ancestry
            // filter applied after notification, so changes to USB descendants
            // (for example only a COM function) are not missed.
            var filter = new CmNotifyFilter
            {
                Size = (uint)Marshal.SizeOf<CmNotifyFilter>(),
                Flags = CM_NOTIFY_FILTER_FLAG_ALL_INTERFACE_CLASSES,
                FilterType = CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE,
                ClassGuid = Guid.Empty
            };

            uint result = CM_Register_Notification(
                ref filter,
                IntPtr.Zero,
                notificationCallback,
                out notificationHandle);

            if (result != CR_SUCCESS)
            {
                notificationHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    $"Unable to register for device notifications (CONFIGRET 0x{result:X8}).");
            }

            nativeWatcherActive = true;
            deviceTree = CaptureDeviceTree();
        }

        private void StartWmiFallback()
        {
            string condition = "TargetInstance ISA 'Win32_PnPEntity'";
            if (monitoringScope == DeviceMonitoringScope.ExternalUsbDevicesOnly)
            {
                condition += " AND TargetInstance.PNPDeviceID LIKE 'USB\\%'";
            }

            var query = new WqlEventQuery
            {
                EventClassName = "__InstanceOperationEvent",
                WithinInterval = TimeSpan.FromSeconds(1),
                Condition = condition
            };

            fallbackWatcher = new ManagementEventWatcher(new ManagementScope("root\\CIMV2"), query);
            fallbackWatcher.Options.Timeout = ManagementOptions.InfiniteTimeout;
            fallbackWatcher.EventArrived += WmiDeviceChanged;
            fallbackWatcher.Start();
        }

        private uint DeviceNotificationReceived(
            IntPtr notification,
            IntPtr context,
            int action,
            IntPtr eventData,
            uint eventDataSize)
        {
            if (disposed || (action != CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL &&
                             action != CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL))
            {
                return CR_SUCCESS;
            }

            string devicePath = eventDataSize > 24
                ? Marshal.PtrToStringUni(IntPtr.Add(eventData, 24)) ?? string.Empty
                : string.Empty;

            if (!string.IsNullOrEmpty(devicePath))
            {
                pendingInterfaceChanges[devicePath] =
                    action == CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL;
            }

            ScheduleDispatch();
            return CR_SUCCESS;
        }

        private void WmiDeviceChanged(object sender, EventArrivedEventArgs e)
        {
            string eventClass = e.NewEvent.ClassPath.ClassName;
            if (eventClass != "__InstanceCreationEvent" && eventClass != "__InstanceDeletionEvent")
            {
                return;
            }

            using var device = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string deviceId = device["PNPDeviceID"]?.ToString() ?? string.Empty;
            string description = device["Caption"]?.ToString() ?? GetDeviceName(deviceId);
            bool present = eventClass == "__InstanceCreationEvent";
            var node = new DeviceChangeNode(deviceId, description, present, null);
            var group = new DeviceChangeGroup(deviceId, description, new[] { node });

            DeviceChangeEvent?.Invoke(this, new DeviceChangeEventArgs
            {
                DeviceID = deviceId,
                Description = description,
                Present = present,
                Groups = new[] { group }
            });
        }

        private void ScheduleDispatch()
        {
            if (disposed)
            {
                return;
            }

            try
            {
                // One physical connection produces several interface events.
                // Wait briefly so they can be shown as one complete hierarchy.
                debounceTimer.Change(TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // Shutdown raced the callback.
            }
        }

        private void DispatchPendingEvents(object? state)
        {
            if (disposed || !nativeWatcherActive)
            {
                return;
            }

            List<DeviceChangeGroup> groups;
            lock (syncRoot)
            {
                if (disposed || !nativeWatcherActive)
                {
                    return;
                }

                Dictionary<string, DeviceSnapshot> previousTree = deviceTree;
                Dictionary<string, DeviceSnapshot> currentTree = CaptureDeviceTree();
                Dictionary<string, bool> changedNodes = FindChangedNodes(previousTree, currentTree);

                foreach (var entry in pendingInterfaceChanges.ToArray())
                {
                    if (pendingInterfaceChanges.TryRemove(entry.Key, out bool interfacePresent))
                    {
                        string instanceId = GetDeviceInstanceId(entry.Key);
                        if (!string.IsNullOrEmpty(instanceId))
                        {
                            changedNodes[instanceId] = interfacePresent;
                        }
                    }
                }

                groups = BuildChangeGroups(changedNodes, previousTree, currentTree);
                deviceTree = currentTree;
            }

            if (groups.Count == 0)
            {
                return;
            }

            DeviceChangeGroup firstGroup = groups[0];
            bool present = firstGroup.Nodes.Any(node => node.Present);
            DeviceChangeEvent?.Invoke(this, new DeviceChangeEventArgs
            {
                DeviceID = firstGroup.DeviceID,
                Description = firstGroup.Description,
                Present = present,
                Groups = groups
            });
        }

        private static Dictionary<string, bool> FindChangedNodes(
            IReadOnlyDictionary<string, DeviceSnapshot> previousTree,
            IReadOnlyDictionary<string, DeviceSnapshot> currentTree)
        {
            var changes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (string deviceId in currentTree.Keys)
            {
                if (!previousTree.ContainsKey(deviceId))
                {
                    changes[deviceId] = true;
                }
            }

            foreach (string deviceId in previousTree.Keys)
            {
                if (!currentTree.ContainsKey(deviceId))
                {
                    changes[deviceId] = false;
                }
            }

            return changes;
        }

        private List<DeviceChangeGroup> BuildChangeGroups(
            IReadOnlyDictionary<string, bool> changedNodes,
            IReadOnlyDictionary<string, DeviceSnapshot> previousTree,
            IReadOnlyDictionary<string, DeviceSnapshot> currentTree)
        {
            var groupedChanges = new Dictionary<string, Dictionary<string, bool>>(
                StringComparer.OrdinalIgnoreCase);

            foreach ((string deviceId, bool present) in changedNodes)
            {
                if (present)
                {
                    deviceNameCache.TryRemove(deviceId, out _);
                }

                IReadOnlyDictionary<string, DeviceSnapshot> sourceTree = present
                    ? currentTree
                    : previousTree;

                if (!sourceTree.ContainsKey(deviceId))
                {
                    continue;
                }

                if (IsInHiddenDeviceBranch(deviceId, sourceTree))
                {
                    continue;
                }

                string? usbRoot = FindUsbDeviceRoot(deviceId, sourceTree);
                if (monitoringScope == DeviceMonitoringScope.ExternalUsbDevicesOnly &&
                    (usbRoot == null || !IsExternalUsbDevice(usbRoot)))
                {
                    continue;
                }

                string groupId = usbRoot
                    ?? sourceTree[deviceId].ParentDeviceId
                    ?? deviceId;

                if (!groupedChanges.TryGetValue(groupId, out Dictionary<string, bool>? group))
                {
                    group = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    groupedChanges[groupId] = group;
                }

                group[deviceId] = present;
            }

            var result = new List<DeviceChangeGroup>();
            foreach ((string groupId, Dictionary<string, bool> changes) in groupedChanges)
            {
                bool rootPresent = currentTree.ContainsKey(groupId);
                bool rootChanged = changes.TryGetValue(groupId, out bool changedRootPresent) &&
                    (changedRootPresent
                        ? !previousTree.ContainsKey(groupId) && rootPresent
                        : previousTree.ContainsKey(groupId) && !rootPresent);
                IReadOnlyDictionary<string, DeviceSnapshot> sourceTree = rootPresent
                    ? currentTree
                    : previousTree;

                if (!rootChanged)
                {
                    // Keep both sides of a mixed batch (for example one child
                    // removed while another is added under the same parent).
                    var combinedTree = new Dictionary<string, DeviceSnapshot>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach ((string deviceId, DeviceSnapshot snapshot) in previousTree)
                    {
                        combinedTree[deviceId] = snapshot;
                    }
                    foreach ((string deviceId, DeviceSnapshot snapshot) in currentTree)
                    {
                        combinedTree[deviceId] = snapshot;
                    }
                    sourceTree = combinedTree;
                }

                IEnumerable<KeyValuePair<string, bool>> nodesToShow = changes;
                if (rootChanged)
                {
                    // A physical parent arrived or left: display its entire cached
                    // subtree, including descendants without their own interface.
                    nodesToShow = GetSubtree(
                            groupId,
                            sourceTree,
                            stopAtSecondaryBuses:
                                monitoringScope == DeviceMonitoringScope.ExternalUsbDevicesOnly)
                        .Select(node => new KeyValuePair<string, bool>(node.DeviceId, rootPresent));
                }

                List<DeviceChangeNode> nodes = nodesToShow
                    .Where(change => (change.Value ? currentTree : previousTree).ContainsKey(change.Key))
                    .Select(change =>
                    {
                        IReadOnlyDictionary<string, DeviceSnapshot> changeTree = change.Value
                            ? currentTree
                            : previousTree;
                        DeviceSnapshot snapshot = changeTree[change.Key];
                        return new DeviceChangeNode(
                            snapshot.DeviceId,
                            GetDeviceName(snapshot.DeviceId),
                            change.Value,
                            snapshot.ParentDeviceId);
                    })
                    .ToList();

                if (!rootChanged)
                {
                    var includedIds = nodes
                        .Select(node => node.DeviceID)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (string changedId in changes.Keys)
                    {
                        string? ancestorId = sourceTree.TryGetValue(changedId, out DeviceSnapshot? changed)
                            ? changed.ParentDeviceId
                            : null;

                        while (ancestorId != null &&
                               !string.Equals(ancestorId, groupId, StringComparison.OrdinalIgnoreCase) &&
                               sourceTree.TryGetValue(ancestorId, out DeviceSnapshot? ancestor))
                        {
                            if (includedIds.Add(ancestorId))
                            {
                                nodes.Add(new DeviceChangeNode(
                                    ancestor.DeviceId,
                                    GetDeviceName(ancestor.DeviceId),
                                    true,
                                    ancestor.ParentDeviceId,
                                    Changed: false));
                            }

                            ancestorId = ancestor.ParentDeviceId;
                        }
                    }
                }

                nodes = nodes
                    // Never render an ancestor or a neighboring branch beneath
                    // this event's root, even if several PnP changes are batched.
                    .Where(node => IsDescendantOrSelf(node.DeviceID, groupId, sourceTree))
                    .OrderBy(node => GetDepth(node.DeviceID, sourceTree))
                    .ThenBy(node => node.Description, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (nodes.Count == 0)
                {
                    continue;
                }

                result.Add(new DeviceChangeGroup(groupId, GetDeviceName(groupId), nodes));
            }

            return result;
        }

        private static IEnumerable<DeviceSnapshot> GetSubtree(
            string rootId,
            IReadOnlyDictionary<string, DeviceSnapshot> tree,
            bool stopAtSecondaryBuses)
        {
            var childrenByParent = tree.Values
                .Where(node => node.ParentDeviceId != null)
                .GroupBy(node => node.ParentDeviceId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(rootId);

            while (pending.Count > 0)
            {
                string currentId = pending.Pop();
                if (!tree.TryGetValue(currentId, out DeviceSnapshot? current))
                {
                    continue;
                }

                yield return current;
                if (childrenByParent.TryGetValue(currentId, out List<DeviceSnapshot>? children))
                {
                    foreach (DeviceSnapshot child in children)
                    {
                        if (IsHiddenDeviceNode(child.DeviceId) ||
                            (stopAtSecondaryBuses && IsSecondaryBusDevice(child.DeviceId)))
                        {
                            continue;
                        }

                        pending.Push(child.DeviceId);
                    }
                }
            }
        }

        private static int GetDepth(
            string deviceId,
            IReadOnlyDictionary<string, DeviceSnapshot> tree)
        {
            int depth = 0;
            string? currentId = deviceId;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (currentId != null && visited.Add(currentId) &&
                   tree.TryGetValue(currentId, out DeviceSnapshot? current))
            {
                depth++;
                currentId = current.ParentDeviceId;
            }

            return depth;
        }

        private static bool IsDescendantOrSelf(
            string deviceId,
            string rootId,
            IReadOnlyDictionary<string, DeviceSnapshot> tree)
        {
            string? currentId = deviceId;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (currentId != null && visited.Add(currentId))
            {
                if (string.Equals(currentId, rootId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                currentId = tree.TryGetValue(currentId, out DeviceSnapshot? current)
                    ? current.ParentDeviceId
                    : null;
            }

            return false;
        }

        private static string? FindUsbDeviceRoot(
            string deviceId,
            IReadOnlyDictionary<string, DeviceSnapshot> tree)
        {
            string? currentId = deviceId;
            string? nearestUsbNode = null;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (currentId != null && visited.Add(currentId) &&
                   tree.TryGetValue(currentId, out DeviceSnapshot? current))
            {
                if (IsSecondaryBusDevice(current.DeviceId))
                {
                    // Bluetooth devices are peripherals of a secondary wireless
                    // bus, not USB functions of the Bluetooth radio itself.
                    return null;
                }

                if (current.DeviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
                {
                    nearestUsbNode ??= current.DeviceId;
                    string hardwarePart = current.DeviceId.Split('\\')[1];
                    if (hardwarePart.StartsWith("VID_", StringComparison.OrdinalIgnoreCase) &&
                        !hardwarePart.Contains("&MI_", StringComparison.OrdinalIgnoreCase) &&
                        !hardwarePart.Contains("&LAMPARRAY", StringComparison.OrdinalIgnoreCase))
                    {
                        return current.DeviceId;
                    }
                }

                currentId = current.ParentDeviceId;
            }

            return nearestUsbNode;
        }

        private static bool IsSecondaryBusDevice(string deviceId)
        {
            string enumerator = deviceId.Split('\\')[0];
            return enumerator.StartsWith("BTH", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInHiddenDeviceBranch(
            string deviceId,
            IReadOnlyDictionary<string, DeviceSnapshot> tree)
        {
            string? currentId = deviceId;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (currentId != null && visited.Add(currentId) &&
                   tree.TryGetValue(currentId, out DeviceSnapshot? current))
            {
                if (IsHiddenDeviceNode(current.DeviceId))
                {
                    return true;
                }

                currentId = current.ParentDeviceId;
            }

            return false;
        }

        private static bool IsHiddenDeviceNode(string deviceId)
        {
            return IsBluetoothProfileDevice(deviceId) ||
                   IsOperatingSystemProjection(deviceId);
        }

        private static bool IsOperatingSystemProjection(string deviceId)
        {
            string[] parts = deviceId.Split('\\');
            string enumerator = parts[0];
            string hardwareId = parts.Length > 1 ? parts[1] : string.Empty;

            // These are Windows-generated presentation/storage layers. The
            // underlying USB or USBSTOR function remains visible.
            return (enumerator.Equals("ROOT", StringComparison.OrdinalIgnoreCase) &&
                    hardwareId.Equals("VOLMGR", StringComparison.OrdinalIgnoreCase)) ||
                   (enumerator.Equals("STORAGE", StringComparison.OrdinalIgnoreCase) &&
                    hardwareId.Equals("Volume", StringComparison.OrdinalIgnoreCase)) ||
                   (enumerator.Equals("SWD", StringComparison.OrdinalIgnoreCase) &&
                    hardwareId.Equals("WPDBUSENUM", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBluetoothProfileDevice(string deviceId)
        {
            string[] parts = deviceId.Split('\\');
            string enumerator = parts[0];
            string hardwareId = parts.Length > 1 ? parts[1] : string.Empty;

            if (enumerator.Equals("BTHLEDEVICE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (enumerator.Equals("BTHENUM", StringComparison.OrdinalIgnoreCase) &&
                hardwareId.StartsWith("{", StringComparison.Ordinal))
            {
                return true;
            }

            return enumerator.Equals("BTHHFENUM", StringComparison.OrdinalIgnoreCase) ||
                   enumerator.Equals("BTHA2DP", StringComparison.OrdinalIgnoreCase) ||
                   enumerator.Equals("BTHAVRCP", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExternalUsbDevice(string physicalUsbDeviceId)
        {
            const int cmDevCapRemovable = 0x00000004;
            const string enumRoot = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum";
            string registryPath = $"{enumRoot}\\{physicalUsbDeviceId}";

            try
            {
                object? value = Registry.GetValue(registryPath, "Capabilities", null);
                int capabilities = value switch
                {
                    int intValue => intValue,
                    long longValue => unchecked((int)longValue),
                    _ => 0
                };

                return (capabilities & cmDevCapRemovable) != 0;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        private Dictionary<string, DeviceSnapshot> CaptureDeviceTree()
        {
            var result = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
            if (CM_Locate_DevNodeW(out uint rootDevInst, null, 0) != CR_SUCCESS ||
                CM_Get_Child(out uint firstChild, rootDevInst, 0) != CR_SUCCESS)
            {
                return result;
            }

            CaptureDeviceAndSiblings(firstChild, null, result);
            return result;
        }

        private static void CaptureDeviceAndSiblings(
            uint firstDevInst,
            string? parentDeviceId,
            IDictionary<string, DeviceSnapshot> result)
        {
            uint currentDevInst = firstDevInst;
            while (true)
            {
                string? deviceId = GetDeviceId(currentDevInst);
                if (!string.IsNullOrEmpty(deviceId))
                {
                    result[deviceId] = new DeviceSnapshot(deviceId, parentDeviceId);
                    if (CM_Get_Child(out uint childDevInst, currentDevInst, 0) == CR_SUCCESS)
                    {
                        CaptureDeviceAndSiblings(childDevInst, deviceId, result);
                    }
                }

                if (CM_Get_Sibling(out uint siblingDevInst, currentDevInst, 0) != CR_SUCCESS)
                {
                    break;
                }

                currentDevInst = siblingDevInst;
            }
        }

        private static string? GetDeviceId(uint devInst)
        {
            if (CM_Get_Device_ID_Size(out uint idLength, devInst, 0) != CR_SUCCESS)
            {
                return null;
            }

            var buffer = new StringBuilder((int)idLength + 1);
            return CM_Get_Device_IDW(devInst, buffer, idLength + 1, 0) == CR_SUCCESS
                ? buffer.ToString()
                : null;
        }

        private string GetDeviceName(string instanceId)
        {
            if (deviceNameCache.TryGetValue(instanceId, out string? cachedName))
            {
                return cachedName;
            }

            string? name = GetRegistryDeviceName(instanceId) ?? GetWmiDeviceName(instanceId);
            if (string.IsNullOrWhiteSpace(name))
            {
                Match hardwareId = Regex.Match(
                    instanceId,
                    @"(?:VID_[0-9A-F]{4}&PID_[0-9A-F]{4}|VEN_[0-9A-F]{4}&DEV_[0-9A-F]{4})",
                    RegexOptions.IgnoreCase);
                name = hardwareId.Success
                    ? $"Device {hardwareId.Value.ToUpperInvariant()}"
                    : instanceId;
            }

            deviceNameCache[instanceId] = name;
            return name;
        }

        private static string? GetWmiDeviceName(string instanceId)
        {
            string escapedInstanceId = instanceId
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal);

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    $"SELECT Caption, Name FROM Win32_PnPEntity WHERE DeviceID='{escapedInstanceId}'");
                using ManagementObjectCollection devices = searcher.Get();
                foreach (ManagementBaseObject device in devices)
                {
                    string? name = device["Caption"]?.ToString() ?? device["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
            catch (ManagementException)
            {
            }
            catch (COMException)
            {
            }

            return null;
        }

        private static string? GetRegistryDeviceName(string instanceId)
        {
            const string enumRoot = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum";
            string registryPath = $"{enumRoot}\\{instanceId}";

            string? name;
            try
            {
                name = Registry.GetValue(registryPath, "FriendlyName", null)?.ToString()
                    ?? Registry.GetValue(registryPath, "DeviceDesc", null)?.ToString();
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            int displayTextSeparator = name.LastIndexOf(';');
            return displayTextSeparator >= 0
                ? name[(displayTextSeparator + 1)..]
                : name;
        }

        private static string GetDeviceInstanceId(string devicePath)
        {
            if (!devicePath.StartsWith(@"\\?\", StringComparison.Ordinal) &&
                !devicePath.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                return devicePath;
            }

            string instanceId = devicePath[4..];
            int classGuidSeparator = instanceId.LastIndexOf("#{", StringComparison.Ordinal);
            if (classGuidSeparator >= 0)
            {
                instanceId = instanceId[..classGuidSeparator];
            }

            return instanceId.Replace('#', '\\');
        }

        private void StopWatcher()
        {
            nativeWatcherActive = false;
            debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (notificationHandle != IntPtr.Zero)
            {
                CM_Unregister_Notification(notificationHandle);
                notificationHandle = IntPtr.Zero;
            }

            if (fallbackWatcher != null)
            {
                fallbackWatcher.EventArrived -= WmiDeviceChanged;
                fallbackWatcher.Stop();
                fallbackWatcher.Dispose();
                fallbackWatcher = null;
            }

            pendingInterfaceChanges.Clear();
            deviceTree.Clear();
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                StopWatcher();
                debounceTimer.Dispose();
                DeviceChangeEvent = null;
            }
        }

        private sealed record DeviceSnapshot(string DeviceId, string? ParentDeviceId);

        [StructLayout(LayoutKind.Explicit, Size = 416)]
        private struct CmNotifyFilter
        {
            [FieldOffset(0)] public uint Size;
            [FieldOffset(4)] public uint Flags;
            [FieldOffset(8)] public int FilterType;
            [FieldOffset(12)] public uint Reserved;
            [FieldOffset(16)] public Guid ClassGuid;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint CmNotificationCallback(
            IntPtr notification,
            IntPtr context,
            int action,
            IntPtr eventData,
            uint eventDataSize);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        private static extern uint CM_Register_Notification(
            ref CmNotifyFilter filter,
            IntPtr context,
            CmNotificationCallback callback,
            out IntPtr notification);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Unregister_Notification(IntPtr notification);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Locate_DevNodeW(
            out uint devInst,
            string? deviceId,
            uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Child(out uint childDevInst, uint devInst, uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Sibling(out uint siblingDevInst, uint devInst, uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Device_ID_Size(out uint length, uint devInst, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_Device_IDW(
            uint devInst,
            StringBuilder buffer,
            uint bufferLength,
            uint flags);
    }
}
