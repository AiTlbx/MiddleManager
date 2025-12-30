using System.Collections.Concurrent;
using Ai.Tlbx.MiddleManager.Host.Ipc;
using Ai.Tlbx.MiddleManager.Host.Shells;

namespace Ai.Tlbx.MiddleManager.Host.Services;

public sealed class SessionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, TerminalSession> _sessions = new();
    private readonly ShellRegistry _shellRegistry = new();
    private bool _disposed;

    public event Action<string, ReadOnlyMemory<byte>>? OnOutput;
    public event Action<string>? OnStateChanged;

    public string? RunAsUser { get; set; }
    public string? RunAsUserSid { get; set; }
    public int? RunAsUid { get; set; }
    public int? RunAsGid { get; set; }

    public TerminalSession CreateSession(CreateSessionRequest request)
    {
        var shellConfig = _shellRegistry.GetConfigurationByName(request.ShellType)
            ?? _shellRegistry.GetConfigurationOrDefault(null);

        var workingDirectory = !string.IsNullOrEmpty(request.WorkingDirectory)
            ? request.WorkingDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var session = TerminalSession.Create(
            workingDirectory,
            request.Cols > 0 ? request.Cols : 80,
            request.Rows > 0 ? request.Rows : 24,
            shellConfig,
            request.RunAsUser ?? RunAsUser,
            request.RunAsUserSid ?? RunAsUserSid,
            request.RunAsUid ?? RunAsUid,
            request.RunAsGid ?? RunAsGid);

        session.OnOutput += (id, data) => OnOutput?.Invoke(id, data);
        session.OnStateChanged += id => OnStateChanged?.Invoke(id);

        _sessions[session.Id] = session;
        return session;
    }

    public TerminalSession? GetSession(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    public IReadOnlyList<TerminalSession> GetAllSessions()
    {
        return _sessions.Values.ToList();
    }

    public IReadOnlyList<SessionSnapshot> GetAllSnapshots()
    {
        return _sessions.Values.Select(s => s.ToSnapshot()).ToList();
    }

    public bool CloseSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Dispose();
            OnStateChanged?.Invoke(sessionId);
            return true;
        }
        return false;
    }

    public bool ResizeSession(string sessionId, int cols, int rows)
    {
        var session = GetSession(sessionId);
        return session?.Resize(cols, rows) ?? false;
    }

    public async Task SendInputAsync(string sessionId, ReadOnlyMemory<byte> data)
    {
        var session = GetSession(sessionId);
        if (session is not null)
        {
            await session.SendInputAsync(data).ConfigureAwait(false);
        }
    }

    public byte[]? GetBuffer(string sessionId)
    {
        var session = GetSession(sessionId);
        return session?.GetBuffer();
    }

    public void SetSessionName(string sessionId, string? name)
    {
        var session = GetSession(sessionId);
        session?.SetName(name);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        foreach (var session in _sessions.Values)
        {
            try { session.Dispose(); } catch { }
        }
        _sessions.Clear();
    }
}
