using System.Buffers.Binary;
using System.Text;

namespace Nomi.Net;

/// <summary>CONNECT: имя пользователя.</summary>
public readonly record struct ConnectPayload(string UserName)
{
    public byte[] ToBytes()
    {
        var name = Encoding.UTF8.GetBytes(UserName);
        if (name.Length > 32) name = name[..32];

        var buffer = new byte[1 + name.Length];
        buffer[0] = (byte)name.Length;
        name.CopyTo(buffer, 1);
        return buffer;
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out ConnectPayload value)
    {
        value = default;
        if (payload.Length < 1 || payload.Length < 1 + payload[0]) return false;

        value = new ConnectPayload(Encoding.UTF8.GetString(payload.Slice(1, payload[0])));
        return true;
    }
}

/// <summary>LOCATION_UPDATE: GPS-координаты (lat, lng, alt).</summary>
public readonly record struct LocationPayload(float Lat, float Lng, float Alt)
{
    public const int Size = 12;

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(0), Lat);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(4), Lng);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(8), Alt);
        return buffer;
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out LocationPayload value)
    {
        value = default;
        if (payload.Length != Size) return false;

        value = new LocationPayload(
            BinaryPrimitives.ReadSingleLittleEndian(payload),
            BinaryPrimitives.ReadSingleLittleEndian(payload[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(payload[8..]));
        return true;
    }
}

/// <summary>REACTION: эмодзи + id получателя.</summary>
public readonly record struct ReactionPayload(byte EmojiId, uint TargetUid)
{
    public const int Size = 5;

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        buffer[0] = EmojiId;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(1), TargetUid);
        return buffer;
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out ReactionPayload value)
    {
        value = default;
        if (payload.Length != Size) return false;

        value = new ReactionPayload(
            payload[0],
            BinaryPrimitives.ReadUInt32LittleEndian(payload[1..]));
        return true;
    }
}

/// <summary>CONNECT_ACK / STATE_UPDATE: состояние пользователя.</summary>
public readonly record struct StatePayload(
    ushort UserId, float Lat, float Lng, float Alt, byte Status, ushort AckSequence)
{
    public const int Size = 20;

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0), UserId);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(2), Lat);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(6), Lng);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(10), Alt);
        buffer[14] = Status;
        buffer[15] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(16), AckSequence);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(18), 0);
        return buffer;
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out StatePayload value)
    {
        value = default;
        if (payload.Length != Size) return false;

        value = new StatePayload(
            BinaryPrimitives.ReadUInt16LittleEndian(payload),
            BinaryPrimitives.ReadSingleLittleEndian(payload[2..]),
            BinaryPrimitives.ReadSingleLittleEndian(payload[6..]),
            BinaryPrimitives.ReadSingleLittleEndian(payload[10..]),
            payload[14],
            BinaryPrimitives.ReadUInt16LittleEndian(payload[16..]));
        return true;
    }
}

/// <summary>REACTION_RESULT: результат доставки реакции.</summary>
public readonly record struct ReactionResultPayload(
    ushort UserId, byte EmojiId, byte Delivered, ushort AckSequence)
{
    public const int Size = 7;

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0), UserId);
        buffer[2] = EmojiId;
        buffer[3] = Delivered;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), AckSequence);
        buffer[6] = 0;
        return buffer;
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out ReactionResultPayload value)
    {
        value = default;
        if (payload.Length != Size) return false;

        value = new ReactionResultPayload(
            BinaryPrimitives.ReadUInt16LittleEndian(payload),
            payload[2],
            payload[3],
            BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]));
        return true;
    }
}

/// <summary>ERROR: текст причины.</summary>
public readonly record struct ErrorPayload(string Message)
{
    public byte[] ToBytes() => Encoding.UTF8.GetBytes(Message);

    public static ErrorPayload Read(ReadOnlySpan<byte> payload) =>
        new(Encoding.UTF8.GetString(payload));
}