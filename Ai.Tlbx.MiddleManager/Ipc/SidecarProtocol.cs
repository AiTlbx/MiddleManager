using System.Buffers;
using System.Text;

namespace Ai.Tlbx.MiddleManager.Ipc;

/// <summary>
/// Binary protocol for IPC communication between web server and terminal host.
/// Frame format: [1 byte type][8 byte sessionId][2 byte length][payload]
/// </summary>
public static class SidecarProtocol
{
    public const int ProtocolVersion = 1;

    public static byte[] SerializeFrame(IpcFrame frame)
    {
        var payloadLength = (ushort)Math.Min(frame.Payload.Length, IpcFrame.MaxPayloadSize);
        var buffer = new byte[IpcFrame.HeaderSize + payloadLength];

        buffer[0] = (byte)frame.Type;
        WriteSessionId(buffer.AsSpan(1, 8), frame.SessionId);
        BitConverter.TryWriteBytes(buffer.AsSpan(9, 2), payloadLength);
        frame.Payload.Span.Slice(0, payloadLength).CopyTo(buffer.AsSpan(IpcFrame.HeaderSize));

        return buffer;
    }

    public static bool TryParseHeader(
        ReadOnlySpan<byte> header,
        out IpcMessageType type,
        out string sessionId,
        out ushort payloadLength)
    {
        type = default;
        sessionId = string.Empty;
        payloadLength = 0;

        if (header.Length < IpcFrame.HeaderSize)
        {
            return false;
        }

        type = (IpcMessageType)header[0];
        sessionId = Encoding.ASCII.GetString(header.Slice(1, 8)).TrimEnd('\0');
        payloadLength = BitConverter.ToUInt16(header.Slice(9, 2));
        return true;
    }

    private static void WriteSessionId(Span<byte> dest, string sessionId)
    {
        dest.Clear();
        var len = Math.Min(8, sessionId?.Length ?? 0);
        for (var i = 0; i < len; i++)
        {
            dest[i] = (byte)sessionId![i];
        }
    }

    // Payload builders for specific message types

    public static byte[] CreateResizePayload(int cols, int rows)
    {
        var payload = new byte[4];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 2), (ushort)cols);
        BitConverter.TryWriteBytes(payload.AsSpan(2, 2), (ushort)rows);
        return payload;
    }

    public static (int cols, int rows) ParseResizePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
        {
            return (80, 24);
        }
        return (BitConverter.ToUInt16(payload.Slice(0, 2)), BitConverter.ToUInt16(payload.Slice(2, 2)));
    }

    public static byte[] CreateHandshakePayload(string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret ?? string.Empty);
        var payload = new byte[4 + secretBytes.Length];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), ProtocolVersion);
        secretBytes.CopyTo(payload.AsSpan(4));
        return payload;
    }

    public static (int version, string secret) ParseHandshakePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
        {
            return (0, string.Empty);
        }
        var version = BitConverter.ToInt32(payload.Slice(0, 4));
        var secret = payload.Length > 4 ? Encoding.UTF8.GetString(payload.Slice(4)) : string.Empty;
        return (version, secret);
    }

    public static byte[] CreateCreateSessionPayload(IpcCreateSessionRequest request)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        writer.Write(request.ShellType ?? string.Empty);
        writer.Write(request.WorkingDirectory ?? string.Empty);
        writer.Write((ushort)request.Cols);
        writer.Write((ushort)request.Rows);
        writer.Write(request.RunAsUser ?? string.Empty);
        writer.Write(request.RunAsUserSid ?? string.Empty);
        writer.Write(request.RunAsUid ?? -1);
        writer.Write(request.RunAsGid ?? -1);

        return ms.ToArray();
    }

    public static IpcCreateSessionRequest ParseCreateSessionPayload(ReadOnlySpan<byte> payload)
    {
        using var ms = new MemoryStream(payload.ToArray());
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        return new IpcCreateSessionRequest
        {
            ShellType = reader.ReadString(),
            WorkingDirectory = reader.ReadString(),
            Cols = reader.ReadUInt16(),
            Rows = reader.ReadUInt16(),
            RunAsUser = NullIfEmpty(reader.ReadString()),
            RunAsUserSid = NullIfEmpty(reader.ReadString()),
            RunAsUid = NullIfNegative(reader.ReadInt32()),
            RunAsGid = NullIfNegative(reader.ReadInt32()),
        };
    }

    public static byte[] CreateSessionCreatedPayload(SessionSnapshot snapshot)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        WriteSessionSnapshot(writer, snapshot);
        return ms.ToArray();
    }

    public static SessionSnapshot ParseSessionCreatedPayload(ReadOnlySpan<byte> payload)
    {
        using var ms = new MemoryStream(payload.ToArray());
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        return ReadSessionSnapshot(reader);
    }

    public static byte[] CreateSessionListPayload(IReadOnlyList<SessionSnapshot> sessions)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        writer.Write((ushort)sessions.Count);
        foreach (var session in sessions)
        {
            WriteSessionSnapshot(writer, session);
        }

        return ms.ToArray();
    }

    public static List<SessionSnapshot> ParseSessionListPayload(ReadOnlySpan<byte> payload)
    {
        using var ms = new MemoryStream(payload.ToArray());
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        var count = reader.ReadUInt16();
        var sessions = new List<SessionSnapshot>(count);
        for (var i = 0; i < count; i++)
        {
            sessions.Add(ReadSessionSnapshot(reader));
        }

        return sessions;
    }

    public static byte[] CreateStateChangePayload(SessionSnapshot snapshot)
    {
        return CreateSessionCreatedPayload(snapshot);
    }

    public static SessionSnapshot ParseStateChangePayload(ReadOnlySpan<byte> payload)
    {
        return ParseSessionCreatedPayload(payload);
    }

    public static byte[] CreateErrorPayload(string message)
    {
        return Encoding.UTF8.GetBytes(message ?? string.Empty);
    }

    public static string ParseErrorPayload(ReadOnlySpan<byte> payload)
    {
        return Encoding.UTF8.GetString(payload);
    }

    private static void WriteSessionSnapshot(BinaryWriter writer, SessionSnapshot snapshot)
    {
        writer.Write(snapshot.Id ?? string.Empty);
        writer.Write(snapshot.Name ?? string.Empty);
        writer.Write(snapshot.ShellType ?? string.Empty);
        writer.Write(snapshot.IsRunning);
        writer.Write(snapshot.ExitCode ?? -1);
        writer.Write((ushort)snapshot.Cols);
        writer.Write((ushort)snapshot.Rows);
        writer.Write(snapshot.CurrentWorkingDirectory ?? string.Empty);
        writer.Write(snapshot.CreatedAt.ToBinary());
        writer.Write(snapshot.Pid);
    }

    private static SessionSnapshot ReadSessionSnapshot(BinaryReader reader)
    {
        return new SessionSnapshot
        {
            Id = reader.ReadString(),
            Name = NullIfEmpty(reader.ReadString()),
            ShellType = reader.ReadString(),
            IsRunning = reader.ReadBoolean(),
            ExitCode = NullIfNegative(reader.ReadInt32()),
            Cols = reader.ReadUInt16(),
            Rows = reader.ReadUInt16(),
            CurrentWorkingDirectory = NullIfEmpty(reader.ReadString()),
            CreatedAt = DateTime.FromBinary(reader.ReadInt64()),
            Pid = reader.ReadInt32(),
        };
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
    private static int? NullIfNegative(int v) => v < 0 ? null : v;
}

public sealed class IpcCreateSessionRequest
{
    public string? ShellType { get; set; }
    public string? WorkingDirectory { get; set; }
    public int Cols { get; set; } = 80;
    public int Rows { get; set; } = 24;
    public string? RunAsUser { get; set; }
    public string? RunAsUserSid { get; set; }
    public int? RunAsUid { get; set; }
    public int? RunAsGid { get; set; }
}

public sealed class SessionSnapshot
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public required string ShellType { get; set; }
    public bool IsRunning { get; set; }
    public int? ExitCode { get; set; }
    public int Cols { get; set; }
    public int Rows { get; set; }
    public string? CurrentWorkingDirectory { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Pid { get; set; }
}
