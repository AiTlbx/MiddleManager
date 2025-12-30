using System.Diagnostics.CodeAnalysis;

namespace Ai.Tlbx.MiddleManager.Host.Pty;

public static class PtyConnectionFactory
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static IPtyConnection Create(
        string app,
        string[] args,
        string workingDirectory,
        int cols,
        int rows,
        IDictionary<string, string>? environment = null,
        string? runAsUser = null,
        string? runAsUserSid = null,
        int? runAsUid = null,
        int? runAsGid = null)
    {
#if WINDOWS
        return WindowsPtyConnection.Start(app, args, workingDirectory, cols, rows, environment, runAsUserSid);
#else
        return UnixPtyConnection.Start(app, args, workingDirectory, cols, rows, environment, runAsUser);
#endif
    }
}
