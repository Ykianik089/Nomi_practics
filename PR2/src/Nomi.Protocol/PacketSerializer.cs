namespace Nomi.Protocol;

public static class PacketSerializer
{
    public static byte[] Serialize(PacketType type, ushort sequence, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(payload));

        uint checksum = Crc32.Compute(payload);
        var packet = new byte[PacketHeader.Size + payload.Length];

        var header = new PacketHeader(
            PacketHeader.CurrentVersion,
            type,
            sequence,
            (ushort)payload.Length,
            0,
            checksum);

        header.WriteTo(packet, checksum);
        payload.CopyTo(packet.AsSpan(PacketHeader.Size));
        return packet;
    }

    public static bool TryDeserialize(
        byte[] datagram,
        out PacketHeader header,
        out byte[] payload,
        out string? error)
    {
        header = default;
        payload = Array.Empty<byte>();
        error = null;

        if (datagram.Length < PacketHeader.Size)
        {
            error = "too short";
            return false;
        }

        header = PacketHeader.ReadFrom(datagram);

        if (header.Version != PacketHeader.CurrentVersion)
        {
            error = "bad version";
            return false;
        }

        int payloadSize = datagram.Length - PacketHeader.Size;
        if (header.PayloadSize != payloadSize)
        {
            error = "payload size mismatch";
            return false;
        }

        // Копируем в новый массив — безопасно для async.
        payload = datagram.AsSpan(PacketHeader.Size).ToArray();

        uint crc = Crc32.Compute(payload);
        if (crc != header.Checksum)
        {
            error = "bad crc";
            return false;
        }

        return true;
    }
}