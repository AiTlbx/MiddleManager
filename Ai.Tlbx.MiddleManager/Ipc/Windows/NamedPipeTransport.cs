#if WINDOWS
using System.IO.Pipes;

namespace Ai.Tlbx.MiddleManager.Ipc.Windows;

public sealed class NamedPipeTransport : IIpcTransport
{
    private readonly NamedPipeClientStream _pipe;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public const string DefaultPipeName = "middlemanager-host";

    public bool IsConnected => _pipe.IsConnected && !_disposed;

    public NamedPipeTransport(string pipeName = DefaultPipeName)
    {
        _pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IpcFrame?> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_pipe.IsConnected)
        {
            return null;
        }

        var header = new byte[IpcFrame.HeaderSize];
        var headerRead = await ReadExactAsync(header, cancellationToken).ConfigureAwait(false);
        if (!headerRead)
        {
            return null;
        }

        if (!SidecarProtocol.TryParseHeader(header, out var type, out var sessionId, out var payloadLength))
        {
            return null;
        }

        ReadOnlyMemory<byte> payload = ReadOnlyMemory<byte>.Empty;
        if (payloadLength > 0)
        {
            var payloadBuffer = new byte[payloadLength];
            var payloadRead = await ReadExactAsync(payloadBuffer, cancellationToken).ConfigureAwait(false);
            if (!payloadRead)
            {
                return null;
            }
            payload = payloadBuffer;
        }

        return new IpcFrame(type, sessionId, payload);
    }

    public async Task WriteFrameAsync(IpcFrame frame, CancellationToken cancellationToken = default)
    {
        if (_disposed || !_pipe.IsConnected)
        {
            return;
        }

        var buffer = SidecarProtocol.SerializeFrame(frame);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _pipe.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await _pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<bool> ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await _pipe.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }
            offset += read;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try { await _pipe.DisposeAsync().ConfigureAwait(false); }
        catch { }

        _writeLock.Dispose();
    }
}

public sealed class NamedPipeServer : IIpcServer
{
    private readonly string _pipeName;

    public NamedPipeServer(string pipeName = NamedPipeTransport.DefaultPipeName)
    {
        _pipeName = pipeName;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<IIpcTransport> AcceptAsync(CancellationToken cancellationToken = default)
    {
        var pipe = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        return new NamedPipeServerTransport(pipe);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

internal sealed class NamedPipeServerTransport : IIpcTransport
{
    private readonly NamedPipeServerStream _pipe;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public bool IsConnected => _pipe.IsConnected && !_disposed;

    public NamedPipeServerTransport(NamedPipeServerStream pipe)
    {
        _pipe = pipe;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<IpcFrame?> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_pipe.IsConnected)
        {
            return null;
        }

        var header = new byte[IpcFrame.HeaderSize];
        var headerRead = await ReadExactAsync(header, cancellationToken).ConfigureAwait(false);
        if (!headerRead)
        {
            return null;
        }

        if (!SidecarProtocol.TryParseHeader(header, out var type, out var sessionId, out var payloadLength))
        {
            return null;
        }

        ReadOnlyMemory<byte> payload = ReadOnlyMemory<byte>.Empty;
        if (payloadLength > 0)
        {
            var payloadBuffer = new byte[payloadLength];
            var payloadRead = await ReadExactAsync(payloadBuffer, cancellationToken).ConfigureAwait(false);
            if (!payloadRead)
            {
                return null;
            }
            payload = payloadBuffer;
        }

        return new IpcFrame(type, sessionId, payload);
    }

    public async Task WriteFrameAsync(IpcFrame frame, CancellationToken cancellationToken = default)
    {
        if (_disposed || !_pipe.IsConnected)
        {
            return;
        }

        var buffer = SidecarProtocol.SerializeFrame(frame);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _pipe.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await _pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<bool> ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await _pipe.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }
            offset += read;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try { await _pipe.DisposeAsync().ConfigureAwait(false); }
        catch { }

        _writeLock.Dispose();
    }
}
#endif
