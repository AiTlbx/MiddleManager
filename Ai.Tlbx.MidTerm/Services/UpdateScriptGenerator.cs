namespace Ai.Tlbx.MidTerm.Services;

public static class UpdateScriptGenerator
{
    private const string ServiceName = "MidTerm";
    private const string LaunchdLabel = "com.aitlbx.midterm";
    private const string SystemdService = "midterm";

    public static string GenerateUpdateScript(string extractedDir, string currentBinaryPath, UpdateType updateType = UpdateType.Full)
    {
        if (OperatingSystem.IsWindows())
        {
            return GenerateWindowsScript(extractedDir, currentBinaryPath, updateType);
        }

        return GenerateUnixScript(extractedDir, currentBinaryPath, updateType);
    }

    private static string GenerateWindowsScript(string extractedDir, string currentBinaryPath, UpdateType updateType)
    {
        var installDir = Path.GetDirectoryName(currentBinaryPath) ?? currentBinaryPath;
        var newWebBinaryPath = Path.Combine(extractedDir, "mt.exe");
        var newConHostBinaryPath = Path.Combine(extractedDir, "mthost.exe");
        var newVersionJsonPath = Path.Combine(extractedDir, "version.json");
        var currentConHostBinaryPath = Path.Combine(installDir, "mthost.exe");
        var currentVersionJsonPath = Path.Combine(installDir, "version.json");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"mt-update-{Guid.NewGuid():N}.ps1");

        var isWebOnly = updateType == UpdateType.WebOnly;
        var killConHost = isWebOnly ? "" : "Get-Process -Name 'mthost' -ErrorAction SilentlyContinue | Stop-Process -Force";
        var backupConHost = isWebOnly ? "" : $@"
if (Test-Path $conHostBinary) {{
    Copy-Item $conHostBinary ($conHostBinary + '.bak') -Force
}}";
        var copyConHost = isWebOnly ? "" : $@"
if (Test-Path $newConHostBinary) {{
    Copy-Item $newConHostBinary $conHostBinary -Force
}}";
        var cleanupConHost = isWebOnly ? "" : "Remove-Item ($conHostBinary + '.bak') -Force -ErrorAction SilentlyContinue";
        var updateTypeComment = isWebOnly ? "# Web-only update - mthost sessions preserved" : "# Full update - all sessions will be restarted";

        var script = $@"
# MidTerm Update Script
{updateTypeComment}
$ErrorActionPreference = 'SilentlyContinue'

# Wait for main process to exit
Start-Sleep -Seconds 2

# Stop service
$service = Get-Service -Name '{ServiceName}' -ErrorAction SilentlyContinue
if ($service) {{
    Stop-Service -Name '{ServiceName}' -Force
    Start-Sleep -Seconds 2
}}

# Kill mt.exe process
Get-Process -Name 'mt' -ErrorAction SilentlyContinue | Stop-Process -Force
{killConHost}
Start-Sleep -Seconds 1

# Backup current binaries
$webBinary = '{currentBinaryPath}'
$conHostBinary = '{currentConHostBinaryPath}'

if (Test-Path $webBinary) {{
    Copy-Item $webBinary ($webBinary + '.bak') -Force
}}
{backupConHost}

# Copy new binaries
$newWebBinary = '{newWebBinaryPath}'
$newConHostBinary = '{newConHostBinaryPath}'
$newVersionJson = '{newVersionJsonPath}'
$currentVersionJson = '{currentVersionJsonPath}'

Copy-Item $newWebBinary $webBinary -Force
{copyConHost}
if (Test-Path $newVersionJson) {{
    Copy-Item $newVersionJson $currentVersionJson -Force
}}

# Start service
if ($service) {{
    Start-Service -Name '{ServiceName}'
}} else {{
    # Start the binary directly
    Start-Process -FilePath $webBinary -WindowStyle Hidden
}}

# Cleanup
Start-Sleep -Seconds 2
Remove-Item ($webBinary + '.bak') -Force -ErrorAction SilentlyContinue
{cleanupConHost}
Remove-Item -Path '{extractedDir}' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
";

        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    private static string GenerateUnixScript(string extractedDir, string currentBinaryPath, UpdateType updateType)
    {
        var installDir = Path.GetDirectoryName(currentBinaryPath) ?? "/usr/local/bin";
        var newWebBinaryPath = Path.Combine(extractedDir, "mt");
        var newConHostBinaryPath = Path.Combine(extractedDir, "mthost");
        var newVersionJsonPath = Path.Combine(extractedDir, "version.json");
        var currentConHostBinaryPath = Path.Combine(installDir, "mthost");
        var currentVersionJsonPath = Path.Combine(installDir, "version.json");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"mt-update-{Guid.NewGuid():N}.sh");

        var isMacOs = OperatingSystem.IsMacOS();
        var isWebOnly = updateType == UpdateType.WebOnly;

        var stopService = isMacOs
            ? $"launchctl unload /Library/LaunchDaemons/{LaunchdLabel}.plist 2>/dev/null || true"
            : $"systemctl stop {SystemdService} 2>/dev/null || true";
        var startService = isMacOs
            ? $"launchctl load /Library/LaunchDaemons/{LaunchdLabel}.plist 2>/dev/null || true"
            : $"systemctl start {SystemdService} 2>/dev/null || true";

        var killConHost = isWebOnly ? "" : "pkill -f 'mthost' 2>/dev/null || true";
        var backupConHost = isWebOnly ? "" : @"
if [ -f ""$CONHOST_BINARY"" ]; then
    cp ""$CONHOST_BINARY"" ""$CONHOST_BINARY.bak""
fi";
        var copyConHost = isWebOnly ? "" : @"
if [ -f ""$NEW_CONHOST_BINARY"" ]; then
    cp ""$NEW_CONHOST_BINARY"" ""$CONHOST_BINARY""
    chmod +x ""$CONHOST_BINARY""
fi";
        var cleanupConHost = isWebOnly ? "" : @"rm -f ""$CONHOST_BINARY.bak""";
        var updateTypeComment = isWebOnly ? "# Web-only update - mthost sessions preserved" : "# Full update - all sessions will be restarted";

        var script = $@"#!/bin/bash
# MidTerm Update Script
{updateTypeComment}

# Wait for main process to exit
sleep 2

# Stop service
{stopService}

# Kill mt process
pkill -f '/mt$' 2>/dev/null || true
{killConHost}
sleep 1

# Backup current binaries
WEB_BINARY='{currentBinaryPath}'
CONHOST_BINARY='{currentConHostBinaryPath}'

if [ -f ""$WEB_BINARY"" ]; then
    cp ""$WEB_BINARY"" ""$WEB_BINARY.bak""
fi
{backupConHost}

# Copy new binaries
NEW_WEB_BINARY='{newWebBinaryPath}'
NEW_CONHOST_BINARY='{newConHostBinaryPath}'
NEW_VERSION_JSON='{newVersionJsonPath}'
CURRENT_VERSION_JSON='{currentVersionJsonPath}'

cp ""$NEW_WEB_BINARY"" ""$WEB_BINARY""
chmod +x ""$WEB_BINARY""
{copyConHost}

if [ -f ""$NEW_VERSION_JSON"" ]; then
    cp ""$NEW_VERSION_JSON"" ""$CURRENT_VERSION_JSON""
fi

# Start service
{startService}

# If services didn't start (not installed as service), start directly
if ! pgrep -f ""$WEB_BINARY"" > /dev/null; then
    nohup ""$WEB_BINARY"" > /dev/null 2>&1 &
fi

# Cleanup
sleep 2
rm -f ""$WEB_BINARY.bak""
{cleanupConHost}
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
