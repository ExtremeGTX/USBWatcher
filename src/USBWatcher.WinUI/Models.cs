using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace USBWatcher.WinUI;

public sealed class DeviceRow
{
    public string Name { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Vid { get; set; } = string.Empty;
    public string Pid { get; set; } = string.Empty;
    public string Identity => string.IsNullOrWhiteSpace(SerialNumber) ? string.Empty : SerialNumber;
}

public sealed class EventNode : INotifyPropertyChanged
{
    private string _time = string.Empty;
    private string _title = string.Empty;
    private string _details = string.Empty;
    private Brush _statusBrush = new SolidColorBrush(Colors.Transparent);

    public string Time
    {
        get => _time;
        set => SetProperty(ref _time, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public string DeviceId { get; set; } = string.Empty;
    public bool ShowStatus { get; set; }
    public Brush StatusBrush
    {
        get => _statusBrush;
        set => SetProperty(ref _statusBrush, value);
    }

    public ObservableCollection<EventNode> Children { get; } = new();
    internal Dictionary<string, EventNode> DescendantNodes { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    internal string? ParentDeviceId { get; set; }
    internal string MergeKind { get; set; } = string.Empty;
    internal DateTime LastUpdatedUtc { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
