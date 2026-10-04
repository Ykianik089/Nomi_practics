using System.Buffers.Binary;

namespace Nomi.Protocol;

public static class PingPongCodec
{
    public const int PingPayloadSize = 12;
    public const int PongPayloadSize = 20;

    public static byte[] EncodePing(long clientSendTimestampMs, uint nonce)
    {
        var buffer = new byte[PingPayloadSize];
        BinaryPrimitives.WriteInt64BigEndian(buffer, clientSendTimestampMs);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(8), nonce);
        return buffer;
    }

    public static bool TryDecodePing(
        ReadOnlySpan<byte> payload,
        out long clientSendTimestampMs,
        out uint nonce)
    {
        clientSendTimestampMs = 0;
        nonce = 0;

        if (payload.Length != PingPayloadSize)
            return false;

        clientSendTimestampMs = BinaryPrimitives.ReadInt64BigEndian(payload);
        nonce = BinaryPrimitives.ReadUInt32BigEndian(payload[8..]);
        return true;
    }

    public static byte[] EncodePong(
        long clientSendTimestampMs,
        long serverReceiveTimestampMs,
        uint nonce)
    {
        var buffer = new byte[PongPayloadSize];
        BinaryPrimitives.WriteInt64BigEndian(buffer, clientSendTimestampMs);
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(8), serverReceiveTimestampMs);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(16), nonce);
        return buffer;
    }

    public static bool TryDecodePong(
        ReadOnlySpan<byte> payload,
        out long clientSendTimestampMs,
        out long serverReceiveTimestampMs,
        out uint nonce)
    {
        clientSendTimestampMs = 0;
        serverReceiveTimestampMs = 0;
        nonce = 0;

        if (payload.Length != PongPayloadSize)
            return false;

        clientSendTimestampMs = BinaryPrimitives.ReadInt64BigEndian(payload);
        serverReceiveTimestampMs = BinaryPrimitives.ReadInt64BigEndian(payload[8..]);
        nonce = BinaryPrimitives.ReadUInt32BigEndian(payload[16..]);
        return true;
    }
}