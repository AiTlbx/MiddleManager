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
dotnet test Ai.Tlbx.MiddleManager.Tests/Ai.Tlbx.MiddleManager.Tests.csproj

# AOT publish (platform-specific)
Ai.Tlbx.MiddleManager/build-aot.cmd        # Windows
./Ai.Tlbx.MiddleManager/build-aot-linux.sh # Linux
./Ai.Tlbx.MiddleManager/build-aot-macos.sh # macOS
```

## Architecture (v2.2+ Supervisor Model)

```
┌─────────────────────────────────────────────────────────────┐
│  Single Service (MiddleManager)                             │
│  Entry point: mm-host --service                             │
├─────────────────────────────────────────────────────────────┤
│  mm-host.exe (PTY Host + Supervisor)                        │
│  ├─ SidecarServer (IPC listener)                            │
│  ├─ SessionManager (owns sessions, survives web restarts)   │
│  ├─ WebServerSupervisor (spawns/monitors mm.exe)            │
│  ├─ Heartbeat sender (Ping every 5s)                        │
│  └─ TerminalSession (wraps PTY, buffers output)             │
└─────────────────────────────────────────────────────────────┘
           │ Named Pipe (Win) / Unix Socket (Unix)
           │ ← Heartbeat (Ping/Pong) →
           ▼
┌─────────────────────────────────────────────────────────────┐
│  mm.exe (Web Server) - spawned as child process             │
│  ├─ REST API, WebSocket handlers, Static files              │
│  ├─ SidecarClient (connects to mm-host via IPC)             │
│  ├─ Heartbeat responder (Pong on Ping)                      │
│  └─ Auto-reconnect (exponential backoff 100ms → 5s)         │
└─────────────────────────────────────────────────────────────┘
           │
    Shell Processes (pwsh, bash, zsh)
```

**Key Benefits:**
- Terminal sessions persist across web server restarts
- Single service entry point (mm-host spawns and supervises mm.exe)
- Auto-restart on crash (exponential backoff 1s → 30s)
- Heartbeat monitoring detects frozen processes
- Auto-reconnect if connection drops

### Command-Line Flags

**mm-host.exe:**
- `--service` — Service mode: spawn and supervise mm.exe
- (no flags) — Standalone mode: just IPC server (for debugging)

**mm.exe:**
- `--spawned` — Spawned by mm-host, use auto-reconnect
- `--port <n>` — Listen on port (default: 2000)
- `--bind <addr>` — Bind address (default: localhost)

### Heartbeat & Recovery

- mm-host sends Ping every 5s, expects Pong within 8s
- mm.exe auto-reconnects on disconnect (exponential backoff 100ms → 5s)
- mm-host auto-restarts mm.exe on crash (exponential backoff 1s → 30s)

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
├── Program.cs                      Entry point, --service flag
├── Services/
│   ├── SidecarServer               IPC listener + heartbeat
│   ├── WebServerSupervisor         Spawns/monitors mm.exe
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

## Features (Already Implemented)

- Cross-platform PTY (Windows ConPTY, Unix forkpty)
- Multiple shells (Pwsh, PowerShell, Cmd, Bash, Zsh)
- WebSocket multiplexing, session rename, resize, OSC-7 directory tracking
- Auto-update from GitHub releases
- Install scripts with service registration (launchd, systemd, Windows Service)
- Embedded static files (AOT compatible), JSON source generators

## Code Style

- **Braces:** Allman (opening brace on new line)
- **Indent:** 4 spaces
- **Private fields:** `_camelCase`
- **Async methods:** `Async` suffix
- **Access modifiers:** Always explicit
- **Namespaces:** File-scoped (`namespace Foo;`)
- **Null checks:** `is null` / `is not null`
- **Comments:** Minimal, only for complex logic

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

Use `release.ps1` to automate version bumping, commit, tag, and push:

```powershell
.\release.ps1 -Bump patch -Message "Fix installer issue"
.\release.ps1 -Bump minor -Message "Add new feature"
.\release.ps1 -Bump major -Message "Breaking change"
```

The script:
1. Bumps version in both csproj files, version.json, and Host/Program.cs
2. Commits all changes with message `v{version}: {Message}`
3. Creates annotated tag
4. Pushes to main and pushes tag
5. GitHub Actions builds and creates release

**GitHub Actions workflow** (`.github/workflows/release.yml`):
- Triggers on `v*` tags
- Matrix build: `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`
- Builds both `mm` and `mm-host` for each platform
- Packages both binaries together per platform

## Update System

**Version manifest** (`version.json`): Contains `web`, `pty`, `protocol` versions.

**Update types:**
- **Web-only** (sessions preserved): Only `web` version changed
- **Full** (sessions lost): `pty` or `protocol` version changed

## Install System

**Scripts:** `install.ps1` (Windows), `install.sh` (macOS/Linux)

**Modes:**
- **System service**: `C:\Program Files\MiddleManager` or `/usr/local/bin`, runs `mm-host --service`
- **User install**: `%LOCALAPPDATA%\MiddleManager` or `~/.local/bin`, user runs `mm` manually

**Key behaviors:**
- Installer kills running `mm-host`/`mm` processes before copying (avoids locked files)
- Service runs as SYSTEM/root but spawns terminals as the installing user
- Settings stored in `%ProgramData%\MiddleManager` (service) or `~/.middlemanager` (user)
