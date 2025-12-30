namespace Ai.Tlbx.MiddleManager.Services;

public static class UpdateScriptGenerator
{
    private const string ServiceName = "MiddleManager";
    private const string LaunchdLabel = "com.aitlbx.middlemanager";
    private const string SystemdService = "middlemanager";

    public static string GenerateUpdateScript(string extractedDir, string currentBinaryPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return GenerateWindowsScript(extractedDir, currentBinaryPath);
        }

        return GenerateUnixScript(extractedDir, currentBinaryPath);
    }

    private static string GenerateWindowsScript(string extractedDir, string currentBinaryPath)
    {
        var installDir = Path.GetDirectoryName(currentBinaryPath) ?? currentBinaryPath;
        var newBinaryPath = Path.Combine(extractedDir, "mm.exe");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"mm-update-{Guid.NewGuid():N}.ps1");

        var script = $@"
# MiddleManager Update Script
$ErrorActionPreference = 'SilentlyContinue'

# Wait for main process to exit
Start-Sleep -Seconds 2

# Stop service if running
$service = Get-Service -Name '{ServiceName}' -ErrorAction SilentlyContinue
if ($service) {{
    Stop-Service -Name '{ServiceName}' -Force
    Start-Sleep -Seconds 2
}}

# Backup current binary
$currentBinary = '{currentBinaryPath}'
$backupPath = $currentBinary + '.bak'
if (Test-Path $currentBinary) {{
    Copy-Item $currentBinary $backupPath -Force
}}

# Copy new binary
$newBinary = '{newBinaryPath}'
Copy-Item $newBinary $currentBinary -Force

# Start service if it was running
if ($service) {{
    Start-Service -Name '{ServiceName}'
}} else {{
    # Start the binary directly
    Start-Process -FilePath $currentBinary -WindowStyle Hidden
}}

# Cleanup
Start-Sleep -Seconds 2
Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path '{extractedDir}' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
";

        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    private static string GenerateUnixScript(string extractedDir, string currentBinaryPath)
    {
        var newBinaryPath = Path.Combine(extractedDir, "mm");
        var newPtyHelperPath = Path.Combine(extractedDir, "pty_helper");
        var installDir = Path.GetDirectoryName(currentBinaryPath) ?? "/usr/local/bin";
        var ptyHelperDir = "/usr/local/lib/middlemanager";
        var scriptPath = Path.Combine(Path.GetTempPath(), $"mm-update-{Guid.NewGuid():N}.sh");

        var isMacOs = OperatingSystem.IsMacOS();
        var stopService = isMacOs
            ? $"launchctl unload /Library/LaunchDaemons/{LaunchdLabel}.plist 2>/dev/null || true"
            : $"systemctl stop {SystemdService} 2>/dev/null || true";
        var startService = isMacOs
            ? $"launchctl load /Library/LaunchDaemons/{LaunchdLabel}.plist 2>/dev/null || true"
            : $"systemctl start {SystemdService} 2>/dev/null || true";

        var script = $@"#!/bin/bash
# MiddleManager Update Script

# Wait for main process to exit
sleep 2

# Stop service if running
{stopService}

# Backup current binary
CURRENT_BINARY='{currentBinaryPath}'
BACKUP_PATH=""$CURRENT_BINARY.bak""
if [ -f ""$CURRENT_BINARY"" ]; then
    cp ""$CURRENT_BINARY"" ""$BACKUP_PATH""
fi

# Copy new binary
cp '{newBinaryPath}' ""$CURRENT_BINARY""
chmod +x ""$CURRENT_BINARY""

# Copy pty_helper if present (macOS)
if [ -f '{newPtyHelperPath}' ]; then
    mkdir -p '{ptyHelperDir}'
    cp '{newPtyHelperPath}' '{ptyHelperDir}/pty_helper'
    chmod +x '{ptyHelperDir}/pty_helper'
fi

# Start service
{startService}

# If service didn't start (not installed as service), start directly
if ! pgrep -f ""$CURRENT_BINARY"" > /dev/null; then
    nohup ""$CURRENT_BINARY"" > /dev/null 2>&1 &
fi

# Cleanup
sleep 2
rm -f ""$BACKUP_PATH""
rm -rf '{extractedDir}'
rm -f ""$0""
";

        File.WriteAllText(scriptPath, script);

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(scriptPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch
            {
            }
        }

        return scriptPath;
    }

    public static void ExecuteUpdateScript(string scriptPath)
    {
        if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            });
        }
        else
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
    }
}
