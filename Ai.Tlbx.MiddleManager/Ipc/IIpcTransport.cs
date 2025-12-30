namespace Ai.Tlbx.MiddleManager.Ipc;

/// <summary>
/// Transport abstraction for IPC communication between web server and terminal host.
/// </summary>
public interface IIpcTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<IpcFrame?> ReadFrameAsync(CancellationToken cancellationToken = default);
    Task WriteFrameAsync(IpcFrame frame, CancellationToken cancellationToken = default);
}

/// <summary>
/// Server-side transport that accepts incoming connections.
/// </summary>
public interface IIpcServer : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task<IIpcTransport> AcceptAsync(CancellationToken cancellationToken = default);
}
