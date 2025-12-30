# CLAUDE.md

This file provides guidance to Claude Code when working with this repository.

## What This Is

MiddleManager is a web-based terminal multiplexer. Single executable (~15MB), native AOT compiled, runs on macOS/Windows/Linux. Serves terminal sessions via browser at `http://localhost:2000`.

**Executable name:** `mm` (mm.exe on Windows)
**Default port:** 2000
**Settings location:** `~/.middlemanager/settings.json`

## Build Commands

```bash
# Build
dotnet build Ai.Tlbx.MiddleManager/Ai.Tlbx.MiddleManager.csproj

# Test
dotnet test Ai.Tlbx.MiddleManager.Tests/Ai.Tlbx.MiddleManager.Tests.csproj

# AOT publish (platform-specific)
./Ai.Tlbx.MiddleManager/build-aot-macos.sh # macOS
Ai.Tlbx.MiddleManager/build-aot.cmd        # Windows
./Ai.Tlbx.MiddleManager/build-aot-linux.sh # Linux

# Output: Ai.Tlbx.MiddleManager/publish/mm[.exe]
```

## Architecture

```
Program.cs                    Entry point, API endpoints, WebSocket handlers
├── Services/
│   ├── SessionManager        Manages all terminal sessions
│   ├── TerminalSession       Individual session (wraps PTY + I/O)
│   ├── MuxConnectionManager  WebSocket multiplexing (multiple sessions over one socket)
│   ├── MuxProtocol           Binary protocol: [1-byte type][2-byte sessionId][2-byte length][payload]
│   ├── UpdateService         GitHub release check, download, background timer
│   └── UpdateScriptGenerator Platform-specific update scripts
├── Pty/
│   ├── IPtyConnection        Interface for PTY implementations
│   ├── PtyConnectionFactory  Platform selector
│   ├── WindowsPtyConnection  ConPTY via CsWin32 (Windows only)
│   └── UnixPtyConnection     forkpty() via P/Invoke (Linux + macOS)
├── Shells/
│   ├── IShellConfiguration   Shell config interface
│   ├── ShellRegistry         Available shells per platform
│   └── *ShellConfiguration   Pwsh, PowerShell, Cmd, Bash, Zsh
├── Settings/
│   ├── MiddleManagerSettings POCO for settings.json
│   └── SettingsService       Load/save with JSON source gen
└── wwwroot/
    ├── index.html            Single page app
    ├── js/terminal.js        xterm.js integration, mux client, sidebar
    └── css/app.css           Styles
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

1. Bump `<Version>` in `Ai.Tlbx.MiddleManager/Ai.Tlbx.MiddleManager.csproj`
2. Update `CHANGELOG.md`
3. Commit, push, tag: `git tag v1.x.x && git push origin v1.x.x`
4. GitHub Actions builds all platforms and creates release

**GitHub Actions workflow** (`.github/workflows/release.yml`):
- Triggers on `v*` tags
- Matrix build: `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`
- Uploads raw binaries as artifacts (not pre-zipped)
- Release job packages artifacts: `.zip` for Windows, `.tar.gz` for Unix
- macOS includes `pty_helper` native binary (built with clang)

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
