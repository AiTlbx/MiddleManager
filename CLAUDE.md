# CLAUDE.md

This file provides guidance to Claude Code when working with this repository.

## What This Is

MiddleManager is a web-based terminal multiplexer. Native AOT compiled, runs on macOS/Windows/Linux. Serves terminal sessions via browser at `http://localhost:2000`.

**Executables (v2.0+):**
- `mm` / `mm.exe` — Web server (UI, REST API, WebSockets)
- `mm-host` / `mm-host.exe` — PTY host (terminal sessions, persists across web restarts)

**Default port:** 2000
**Settings location:** `~/.middlemanager/settings.json`

## Build Commands

```bash
# Build both projects
dotnet build Ai.Tlbx.MiddleManager/Ai.Tlbx.MiddleManager.csproj
dotnet build Ai.Tlbx.MiddleManager.Host/Ai.Tlbx.MiddleManager.Host.csproj

# Test
dotnet test Ai.Tlbx.MiddleManager.Aot.Tests/Ai.Tlbx.MiddleManager.Aot.Tests.csproj

# AOT publish (platform-specific)
Ai.Tlbx.MiddleManager.Aot/build-aot.cmd        # Windows
./Ai.Tlbx.MiddleManager.Aot/build-aot-linux.sh # Linux
./Ai.Tlbx.MiddleManager.Aot/build-aot-macos.sh # macOS

# Output: Ai.Tlbx.MiddleManager.Aot/publish/mm[.exe]
```

## Architecture (v2.0+ Sidecar)

```
┌─────────────────────────────────────────────────────────────┐
│  mm.exe (Web Server) - Ai.Tlbx.MiddleManager                │
│  ├─ REST API, WebSocket handlers, Static files              │
│  ├─ Settings UI                                             │
│  └─ SidecarClient (connects to mm-host via IPC)             │
└─────────────────────────────────────────────────────────────┘
           │ Named Pipe (Win) / Unix Socket (Unix)
           ▼
┌─────────────────────────────────────────────────────────────┐
│  mm-host.exe (PTY Sidecar) - Ai.Tlbx.MiddleManager.Host     │
│  ├─ SidecarServer (accepts IPC connections)                 │
│  ├─ SessionManager (owns sessions, survives web restarts)   │
│  ├─ TerminalSession (wraps PTY, buffers output)             │
│  └─ IPtyConnection (Windows ConPTY / Unix forkpty)          │
└─────────────────────────────────────────────────────────────┘
           │
    Shell Processes (pwsh, bash, zsh)
```

**Key Benefit:** Terminal sessions persist across web server restarts. Most updates only restart mm.exe.

### Project Structure

```
Ai.Tlbx.MiddleManager/              Web Server (mm.exe)
├── Program.cs                      Entry point, API, WebSocket handlers
├── Services/
│   ├── SidecarClient               IPC client to mm-host
│   ├── SidecarLifecycle            Spawn/connect to mm-host
│   ├── SidecarSessionManager       Proxy to mm-host sessions
│   ├── SidecarMuxConnectionManager WebSocket mux for sidecar mode
│   ├── SessionManager              Direct mode (fallback, no sidecar)
│   ├── UpdateService               GitHub release check, version comparison
│   └── UpdateScriptGenerator       Platform-specific update scripts
├── Ipc/                            IPC infrastructure
│   ├── IIpcTransport               Transport interface
│   ├── IpcFrame, IpcMessageType    Binary protocol
│   ├── SidecarProtocol             Payload serialization
│   └── Windows/, Unix/             Platform transports
└── wwwroot/                        Static files (embedded)

Ai.Tlbx.MiddleManager.Host/         PTY Host (mm-host.exe)
├── Program.cs                      Entry point, CLI parsing
├── Services/
│   ├── SidecarServer               IPC listener
│   ├── SessionManager              Session lifecycle
│   └── TerminalSession             PTY wrapper + output buffer
├── Pty/                            PTY implementations
│   ├── WindowsPtyConnection        ConPTY
│   └── UnixPtyConnection           forkpty()
├── Shells/                         Shell configurations
└── Ipc/                            IPC (copy of main project)
```

## API Endpoints

```
GET  /api/sessions           List all sessions
POST /api/sessions           Create session {shellType, cols, rows, workingDirectory}
DELETE /api/sessions/{id}    Close session
POST /api/sessions/{id}/resize   Resize {cols, rows}
PUT  /api/sessions/{id}/name     Rename {name}
GET  /api/shells             Available shells for platform
GET  /api/settings           Current settings
PUT  /api/settings           Update settings
GET  /api/version            Server version
GET  /api/update/check       Check for updates {available, currentVersion, latestVersion}
POST /api/update/apply       Download update and restart
```

## WebSocket Endpoints

- `/ws/mux` — Multiplexed terminal I/O (binary protocol)
- `/ws/state` — Session state changes (JSON, for sidebar sync)

## What's Already Done (Don't Re-implement)

- Cross-platform PTY support (Windows ConPTY, Linux/macOS forkpty)
- Shell configurations for Pwsh, PowerShell, Cmd, Bash, Zsh
- WebSocket multiplexing protocol
- Session rename with server-side storage and cross-browser sync
- Active/passive viewer indicator (LastActiveViewerId)
- OSC-7 working directory tracking
- Terminal resize
- Settings persistence
- Embedded static file serving (AOT compatible)
- ASCII art welcome banner with version/port/platform info
- Auto-update from GitHub releases (background check + UI notification)
- Install scripts with system service registration (launchd, systemd, Windows Service)

## Code Style

- **Braces:** Allman (opening brace on new line)
- **Indent:** 4 spaces
- **Private fields:** `_camelCase`
- **Async methods:** `Async` suffix
- **Access modifiers:** Always explicit
- **Namespaces:** File-scoped (`namespace Foo;`)
- **Null checks:** `is null` / `is not null`
- **Comments:** Minimal, only for complex logic

## AOT Gotchas

- **JSON:** Must use source generators (AppJsonContext, SettingsJsonContext)
- **Reflection:** Avoid, or annotate with `[DynamicallyAccessedMembers]`
- **CsWin32:** Only included when building for Windows RID (conditional in csproj)
- **Static files:** Embedded as resources, served via EmbeddedWebRootFileProvider

## Platform-Specific

| Platform | PTY | Shells |
|----------|-----|--------|
| macOS | forkpty() libSystem | Zsh, Bash |
| Windows | ConPTY (Windows.Win32) | Pwsh, PowerShell, Cmd |
| Linux | forkpty() libc | Bash, Zsh |

Default shell: Zsh (macOS), Pwsh (Windows), Bash (Linux)

## Important Rules

- Never `dotnet run` without user permission
- Never `Task.Run` unless explicitly asked for threading
- Aim for 0 build warnings
- Use interfaces + DI, not static classes
- Platform checks: `OperatingSystem.IsWindows()`, `.IsLinux()`, `.IsMacOS()`

## Release Process

1. Bump `<Version>` in BOTH projects:
   - `Ai.Tlbx.MiddleManager/Ai.Tlbx.MiddleManager.csproj`
   - `Ai.Tlbx.MiddleManager.Host/Ai.Tlbx.MiddleManager.Host.csproj`
2. Update `version.json` in repo root (web, pty, protocol versions)
3. Update `CHANGELOG.md`
4. Commit, push, tag: `git tag v2.x.x && git push origin v2.x.x`
5. GitHub Actions builds both projects and creates release

**GitHub Actions workflow** (`.github/workflows/release.yml`):
- Triggers on `v*` tags
- Matrix build: `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`
- Builds both `mm` and `mm-host` for each platform
- Packages both binaries together per platform

## Update System & Version Comparison

### Version Manifest (`version.json`)

```json
{
  "web": "2.0.0",      // mm.exe version
  "pty": "2.0.0",      // mm-host.exe version
  "protocol": 1,       // IPC protocol version (must match exactly)
  "minCompatiblePty": "2.0.0"  // Minimum mm-host version web server can work with
}
```

### Determining Update Type

**Web-Only Update** (sessions preserved):
- `release.pty == installed.pty` (PTY host version unchanged)
- Only mm.exe is replaced
- Sessions continue running in mm-host
- User message: "Quick update - your terminals will stay alive"

**Full Update** (sessions lost):
- `release.pty != installed.pty` (PTY host version changed)
- Both binaries replaced
- mm-host restarts, killing all sessions
- User message: "Full update - please save your work, terminals will restart"

**Protocol Mismatch** (requires full update):
- `release.protocol != installed.protocol`
- IPC protocol changed, both must update together
- Always a full update

### Version Comparison Logic (in UpdateService)

```csharp
public UpdateType DetermineUpdateType(VersionManifest installed, VersionManifest release)
{
    // Protocol change = always full update
    if (release.Protocol != installed.Protocol)
        return UpdateType.Full;

    // PTY version change = full update (host restarts)
    if (release.Pty != installed.Pty)
        return UpdateType.Full;

    // Only web version changed = web-only update (sessions preserved)
    if (release.Web != installed.Web)
        return UpdateType.WebOnly;

    return UpdateType.None;
}
```

### Update Scenarios

| Scenario | Web Version | PTY Version | Protocol | Update Type | Sessions |
|----------|-------------|-------------|----------|-------------|----------|
| Bug fix in UI | 2.0.0 → 2.0.1 | 2.0.0 | 1 | Web-Only | Preserved |
| New web feature | 2.0.0 → 2.1.0 | 2.0.0 | 1 | Web-Only | Preserved |
| PTY bug fix | 2.0.0 | 2.0.0 → 2.0.1 | 1 | Full | Lost |
| New PTY feature | 2.0.0 → 2.1.0 | 2.0.0 → 2.1.0 | 1 | Full | Lost |
| Protocol change | 2.0.0 → 3.0.0 | 2.0.0 → 3.0.0 | 1 → 2 | Full | Lost |

### UI Messaging

**Web-Only Update Toast:**
```
Update Available: v2.0.1
Quick update - your terminals will stay alive!
[Update Now]
```

**Full Update Toast:**
```
Update Available: v2.1.0
⚠️ This update requires restarting the terminal host.
Please save your work - all terminal sessions will close.
[Update Now] [Later]
```

## Install System

**Install scripts:**
- `install.ps1` — Windows (PowerShell)
- `install.sh` — macOS/Linux (Bash)

**Install modes:**
| Mode | Location | Settings Path |
|------|----------|---------------|
| System service | `C:\Program Files\MiddleManager` (Win) / `/usr/local/bin` (Unix) | `%ProgramData%\MiddleManager` (Win) / `/usr/local/etc/middlemanager` (Unix) |
| User install | `%LOCALAPPDATA%\MiddleManager` (Win) / `~/.local/bin` (Unix) | `~/.middlemanager` |

**Service registration:**
- Windows: `sc.exe create` Windows Service, runs as LocalSystem
- macOS: launchd plist in `/Library/LaunchDaemons`
- Linux: systemd unit in `/etc/systemd/system`

**User de-elevation:** When running as service (root/LocalSystem), terminals spawn as the installing user via:
- Windows: `CreateProcessAsUser` with `WTSQueryUserToken`
- Unix: `sudo -u` wrapper

**Settings migration on update:**
1. Installer renames `settings.json` → `settings.json.old`
2. Installer writes minimal bootstrap settings (runAs* fields only)
3. App on startup detects `.old`, migrates user preferences (theme, fontSize, shell, etc.)
4. App deletes `.old` after successful migration

**Important installer gotchas:**
- Must stop service BEFORE copying binary (file locked by running process)
- Capture user identity BEFORE elevation (for runAs* settings)
- Windows installer re-downloads script for elevated process (can't pass complex state)

## Embedded Resources

Static files in `wwwroot/` are embedded as resources via:
```xml
<EmbeddedResource Include="wwwroot\**\*" LinkBase="wwwroot" />
```

Served by `EmbeddedWebRootFileProvider` with namespace prefix `Ai.Tlbx.MiddleManager.wwwroot.*`

**Gotcha:** The namespace must match the project folder name exactly, not assembly name or any `.Aot` suffix.

## Windows Service Hosting

Requires `Microsoft.Extensions.Hosting.WindowsServices` package (Windows only, conditional in csproj).

```csharp
#if WINDOWS
    builder.Host.UseWindowsService();
#endif
```

The `WINDOWS` define is set conditionally when `RuntimeIdentifier.StartsWith('win')`.
