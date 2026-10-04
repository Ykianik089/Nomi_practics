using System.Buffers.Binary;

namespace Nomi.Protocol;

public readonly record struct PacketHeader(
    byte Version,
    PacketType Type,
    ushort Sequence,
    ushort PayloadSize,
    ushort Reserved,
    uint Checksum)
{
    public const int Size = 12;
    public const byte CurrentVersion = 1;

    public void WriteTo(Span<byte> buffer, uint checksum)
    {
        buffer[0] = Version;
        buffer[1] = (byte)Type;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[2..], Sequence);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[4..], PayloadSize);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[6..], Reserved);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[8..], checksum);
    }

    public static PacketHeader ReadFrom(ReadOnlySpan<byte> buffer)
    {
        return new PacketHeader(
            buffer[0],
            (PacketType)buffer[1],
            BinaryPrimitives.ReadUInt16BigEndian(buffer[2..]),
            BinaryPrimitives.ReadUInt16BigEndian(buffer[4..]),
            BinaryPrimitives.ReadUInt16BigEndian(buffer[6..]),
            BinaryPrimitives.ReadUInt32BigEndian(buffer[8..]));
    }
}