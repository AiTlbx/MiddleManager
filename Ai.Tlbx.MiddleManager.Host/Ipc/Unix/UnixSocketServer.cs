#if !WINDOWS
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Ai.Tlbx.MiddleManager.Host.Ipc.Unix;

public sealed class UnixSocketServer : IIpcServer
{
    private readonly string _socketPath;
    private Socket? _listener;
    private bool _disposed;

    public static string DefaultSocketPath => GetDefaultSocketPath();

    public UnixSocketServer(string? socketPath = null)
    {
        _socketPath = socketPath ?? DefaultSocketPath;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(_socketPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(10);

        return Task.CompletedTask;
    }

    public async Task<IIpcTransport> AcceptAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is null)
        {
            throw new InvalidOperationException("Server not started");
        }

        var clientSocket = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
        return new UnixSocketTransport(clientSocket);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;

        _listener?.Dispose();

        try
        {
            if (File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }
        }
        catch { }

        return ValueTask.CompletedTask;
    }

    private static string GetDefaultSocketPath()
    {
        if (getuid() == 0)
        {
            return "/tmp/middlemanager-host.sock";
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".middlemanager", "host.sock");
    }

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint getuid();
}

internal sealed class UnixSocketTransport : IIpcTransport
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public bool IsConnected => _socket.Connected && !_disposed;

    public UnixSocketTransport(Socket socket)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: false);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<IpcFrame?> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
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
        if (_disposed)
        {
            return;
        }

        var buffer = SidecarProtocol.SerializeFrame(frame);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
            var read = await _stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
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

        try { await _stream.DisposeAsync().ConfigureAwait(false); }
        catch { }

        _socket.Dispose();
        _writeLock.Dispose();
    }
}
#endif
