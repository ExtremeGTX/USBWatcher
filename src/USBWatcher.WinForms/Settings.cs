using System.Text.Json;

namespace USBWatcher
{
    internal class Settings
    {
        public Dictionary<string, string> FriendlyNames { get; set; } = new();

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "USBWatcher",
            "settings.json"
        );

        public static Settings Current { get; private set; } = new();

        public static void Load()
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                // Load settings if file exists
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<Settings>(json);
                    if (settings != null)
                    {
                        Current = settings;
                        return;
                    }
                }

                // If file doesn't exist or is invalid, create new settings
                Current = new Settings();
                Save();
            }
            catch
            {
                Current = new Settings();
            }
        }

        public static bool Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsPath, json);
                return true;
            }
            catch
            {
                // Handle or log error as needed
                return false;
            }
        }

        public static string? GetStoredDeviceName(string vid, string pid, string? sn)
        {
            string key = $"{vid}_{pid}";
            if (string.IsNullOrEmpty(sn) == false)
            {
                key += $"_{sn}";
            }
            return Current.FriendlyNames.GetValueOrDefault(key);
        }

        public static bool SaveDeviceName(string vid, string pid, string? sn, string friendlyName)
        {
            string key = $"{vid}_{pid}";
            if (string.IsNullOrEmpty(sn) == false)
            {
                key += $"_{sn}";
            }
            Current.FriendlyNames[key] = friendlyName;
            return Save();
        }

        public static string GetSettingsPath()
        {
            return SettingsPath;
        }
    }
}
