namespace Ai.Tlbx.MiddleManager.Ipc;

public static class IpcTransportFactory
{
    public static IIpcTransport CreateClient()
    {
#if WINDOWS
        return new Windows.NamedPipeTransport();
#else
        return new Unix.UnixSocketTransport();
#endif
    }

    public static IIpcServer CreateServer()
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
        return $@"\\.\pipe\{Windows.NamedPipeTransport.DefaultPipeName}";
#else
        return Unix.UnixSocketTransport.DefaultSocketPath;
#endif
    }
}
