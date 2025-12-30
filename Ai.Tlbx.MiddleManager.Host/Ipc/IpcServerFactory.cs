namespace Ai.Tlbx.MiddleManager.Host.Ipc;

public static class IpcServerFactory
{
    public static IIpcServer Create()
    {
#if WINDOWS
        return new Windows.NamedPipeServer();
#else
        return new Unix.UnixSocketServer();
#endif
    }

    public static string GetEndpointDescription()
    {
#if WINDOWS
        return $@"\\.\pipe\{Windows.NamedPipeServer.DefaultPipeName}";
#else
        return Unix.UnixSocketServer.DefaultSocketPath;
#endif
    }
}
