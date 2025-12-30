namespace Ai.Tlbx.MiddleManager.Host.Ipc;

public interface IIpcTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<IpcFrame?> ReadFrameAsync(CancellationToken cancellationToken = default);
    Task WriteFrameAsync(IpcFrame frame, CancellationToken cancellationToken = default);
}

public interface IIpcServer : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task<IIpcTransport> AcceptAsync(CancellationToken cancellationToken = default);
}
