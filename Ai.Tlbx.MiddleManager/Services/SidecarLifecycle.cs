using System.Diagnostics;
using Ai.Tlbx.MiddleManager.Ipc;
using Ai.Tlbx.MiddleManager.Settings;

namespace Ai.Tlbx.MiddleManager.Services;

public sealed class SidecarLifecycle : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly SidecarClient _client;
    private Process? _sidecarProcess;
    private bool _disposed;

    public SidecarClient Client => _client;
    public bool IsConnected => _client.IsConnected;

    public SidecarLifecycle(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _client = new SidecarClient();
    }

    public async Task<bool> StartAndConnectAsync(CancellationToken cancellationToken = default)
    {
        // Try to connect to existing mm-host
        if (await _client.ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            Console.WriteLine("Connected to existing mm-host");
            return true;
        }

        // In service mode, we expect mm-host to already be running
        if (_settingsService.IsRunningAsService)
        {
            Console.WriteLine("Warning: mm-host not running. Cannot auto-spawn in service mode.");
            return false;
        }

        // Auto-spawn mm-host in user mode
        if (!await SpawnSidecarAsync(cancellationToken).ConfigureAwait(false))
        {
            Console.WriteLine("Failed to spawn mm-host");
            return false;
        }

        // Wait for sidecar to start and connect
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            if (await _client.ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                Console.WriteLine("Connected to mm-host");
                return true;
            }
        }

        Console.WriteLine("Failed to connect to mm-host after spawn");
        return false;
    }

    private async Task<bool> SpawnSidecarAsync(CancellationToken cancellationToken)
    {
        var hostPath = GetSidecarPath();
        if (string.IsNullOrEmpty(hostPath) || !File.Exists(hostPath))
        {
            Console.WriteLine($"mm-host not found at expected path: {hostPath}");
            return false;
        }

        var settings = _settingsService.Load();

        var psi = new ProcessStartInfo
        {
            FileName = hostPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        // Pass runAs settings via environment
        if (!string.IsNullOrEmpty(settings.RunAsUser))
        {
            psi.Environment["MM_RUN_AS_USER"] = settings.RunAsUser;
        }
        if (!string.IsNullOrEmpty(settings.RunAsUserSid))
        {
            psi.Environment["MM_RUN_AS_USER_SID"] = settings.RunAsUserSid;
        }
        if (settings.RunAsUid.HasValue)
        {
            psi.Environment["MM_RUN_AS_UID"] = settings.RunAsUid.Value.ToString();
        }
        if (settings.RunAsGid.HasValue)
        {
            psi.Environment["MM_RUN_AS_GID"] = settings.RunAsGid.Value.ToString();
        }

        try
        {
            _sidecarProcess = Process.Start(psi);
            return _sidecarProcess is not null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start mm-host: {ex.Message}");
            return false;
        }
    }

    private static string GetSidecarPath()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExe))
        {
            return string.Empty;
        }

        var dir = Path.GetDirectoryName(currentExe);
        if (string.IsNullOrEmpty(dir))
        {
            return string.Empty;
        }

        var hostName = OperatingSystem.IsWindows() ? "mm-host.exe" : "mm-host";
        return Path.Combine(dir, hostName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        await _client.DisposeAsync().ConfigureAwait(false);

        // Don't kill the sidecar - it should keep running for session persistence
        // The sidecar will clean up when the system shuts down or when explicitly stopped
    }
}
