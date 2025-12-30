namespace Ai.Tlbx.MiddleManager.Pty;

public static class PtyConnectionFactory
{
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
        if (OperatingSystem.IsWindows())
        {
            return WindowsPtyConnection.Start(app, args, workingDirectory, cols, rows, environment, runAsUserSid);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return UnixPtyConnection.Start(app, args, workingDirectory, cols, rows, environment, runAsUser);
        }

        throw new PlatformNotSupportedException("PTY is only supported on Windows, Linux, and macOS");
    }
}
