using System.Text.Json;
using Microsoft.Win32.TaskScheduler;

namespace USBWatcher.WinUI;

public sealed class AppSettings
{
    public Dictionary<string, string> FriendlyNames { get; set; } = new();
    public bool ListenToAllDevices { get; set; }
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowOnlyConnectedDevices { get; set; }
    public bool AutoScrollRecentEvents { get; set; } = true;
    public bool ExpandOnlyLastDeviceEvent { get; set; }
    public bool RememberWindowPosition { get; set; } = true;
    public string Theme { get; set; } = "System";
    public int MaxRecentEvents { get; set; } = 100;
    public int WindowX { get; set; } = int.MinValue;
    public int WindowY { get; set; } = int.MinValue;
    public int WindowWidth { get; set; } = 560;
    public int WindowHeight { get; set; } = 880;
    public bool HasSavedWindowPlacement { get; set; }
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
}

public static class SettingsStore
{
    private const string StartupTaskName = "USBWatcher.WinUI_AutoStart";
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "USBWatcher",
        "settings.winui.json");
    private static readonly string LegacySettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "USBWatcher",
        "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            string sourcePath = File.Exists(SettingsPath) ? SettingsPath : LegacySettingsPath;
            if (File.Exists(sourcePath))
            {
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(sourcePath)) ?? new();
            }
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public static bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            UpdateStartupRegistration();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? GetAlias(string vid, string pid, string? serialOrInterface)
    {
        return Current.FriendlyNames.GetValueOrDefault(GetDeviceKey(vid, pid, serialOrInterface));
    }

    public static void SetAlias(string vid, string pid, string? serialOrInterface, string alias)
    {
        string key = GetDeviceKey(vid, pid, serialOrInterface);
        if (string.IsNullOrWhiteSpace(alias))
        {
            Current.FriendlyNames.Remove(key);
        }
        else
        {
            Current.FriendlyNames[key] = alias.Trim();
        }

        Save();
    }

    private static string GetDeviceKey(string vid, string pid, string? serialOrInterface)
    {
        string key = $"{vid}_{pid}";
        return string.IsNullOrWhiteSpace(serialOrInterface) ? key : $"{key}_{serialOrInterface}";
    }

    private static void UpdateStartupRegistration()
    {
        using var taskService = new TaskService();
        if (!Current.StartWithWindows)
        {
            if (taskService.GetTask(StartupTaskName) is not null)
            {
                taskService.RootFolder.DeleteTask(StartupTaskName, exceptionOnNotExists: false);
            }
            return;
        }

        string executable = Environment.ProcessPath ?? string.Empty;
        string arguments = Current.StartMinimized ? "--minimized" : string.Empty;
        TaskDefinition definition = taskService.NewTask();
        definition.RegistrationInfo.Description = "Starts USBWatcher WinUI at user logon";
        definition.Principal.RunLevel = TaskRunLevel.Highest;
        definition.Principal.LogonType = TaskLogonType.InteractiveToken;
        definition.Triggers.Add(new LogonTrigger());
        definition.Actions.Add(new ExecAction(executable, arguments, Path.GetDirectoryName(executable)));
        taskService.RootFolder.RegisterTaskDefinition(
            StartupTaskName,
            definition,
            TaskCreation.CreateOrUpdate,
            null,
            null,
            TaskLogonType.InteractiveToken,
            null);
    }
}
