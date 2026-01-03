namespace Ai.Tlbx.MidTerm.Common.Ipc;

/// <summary>
/// Platform-specific IPC endpoint resolution.
/// Windows: Named pipes
/// Unix: Unix domain sockets
/// </summary>
public static class IpcEndpoint
{
    /// <summary>
    /// Get the IPC endpoint name/path for a session.
    /// </summary>
    public static string GetSessionEndpoint(string sessionId)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"mt-con-{sessionId}";
        }
        else
        {
            return $"/tmp/mt-con-{sessionId}.sock";
        }
    }

    /// <summary>
    /// Check if the endpoint is a Unix socket (path) vs named pipe (name only).
    /// </summary>
    public static bool IsUnixSocket => !OperatingSystem.IsWindows();
}
