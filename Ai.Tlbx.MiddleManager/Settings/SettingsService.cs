using Ai.Tlbx.MiddleManager.Settings;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Ai.Tlbx.MiddleManager.Settings
{
    public enum SettingsLoadStatus
    {
        Default,
        LoadedFromFile,
        ErrorFallbackToDefault
    }

    public sealed class SettingsService
    {
        private readonly string _settingsPath;
        private MiddleManagerSettings? _cached;
        private readonly object _lock = new();

        public SettingsLoadStatus LoadStatus { get; private set; } = SettingsLoadStatus.Default;
        public string? LoadError { get; private set; }
        public string SettingsPath => _settingsPath;
        public bool IsRunningAsService { get; }

        public SettingsService()
        {
            IsRunningAsService = DetectServiceMode();
            _settingsPath = GetSettingsPath(IsRunningAsService);
        }

        private static string GetSettingsPath(bool isService)
        {
            if (isService)
            {
                if (OperatingSystem.IsWindows())
                {
                    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    return Path.Combine(programData, "MiddleManager", "settings.json");
                }
                else
                {
                    return "/usr/local/etc/middlemanager/settings.json";
                }
            }

            var userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(userDir, ".middlemanager");
            return Path.Combine(configDir, "settings.json");
        }

        private static bool DetectServiceMode()
        {
            if (OperatingSystem.IsWindows())
            {
                return IsWindowsService();
            }
            else
            {
                return getuid() == 0;
            }
        }

        private static bool IsWindowsService()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return identity.IsSystem;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("libc", EntryPoint = "getuid")]
        private static extern uint getuid();

        public MiddleManagerSettings Load()
        {
            lock (_lock)
            {
                if (_cached is not null)
                {
                    return _cached;
                }

                if (!File.Exists(_settingsPath))
                {
                    _cached = new MiddleManagerSettings();
                    LoadStatus = SettingsLoadStatus.Default;
                    return _cached;
                }

                try
                {
                    var json = File.ReadAllText(_settingsPath);
                    _cached = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.MiddleManagerSettings)
                        ?? new MiddleManagerSettings();
                    LoadStatus = SettingsLoadStatus.LoadedFromFile;
                }
                catch (Exception ex)
                {
                    _cached = new MiddleManagerSettings();
                    LoadStatus = SettingsLoadStatus.ErrorFallbackToDefault;
                    LoadError = ex.Message;
                }

                return _cached;
            }
        }

        public void Save(MiddleManagerSettings settings)
        {
            lock (_lock)
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.MiddleManagerSettings);
                File.WriteAllText(_settingsPath, json);
                _cached = settings;
            }
        }

        public void InvalidateCache()
        {
            lock (_lock)
            {
                _cached = null;
            }
        }
    }
}
