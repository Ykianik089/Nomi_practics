using System.Text;

namespace Nomi.Net;

public enum ParseResult
{
    Ok,
    TooShort,
    UnknownVersion,
    SizeMismatch,
    ChecksumMismatch,
}

public sealed class Packet
{
    public PacketHeader Header { get; }
    public byte[] Payload { get; }

    public Packet(PacketHeader header, byte[] payload)
    {
        Header = header;
        Payload = payload;
    }

    public static byte[] Build(PacketType type, ushort sequence, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > ushort.MaxValue)
            throw new ArgumentException("Полезная нагрузка слишком велика.", nameof(payload));

        var datagram = new byte[PacketHeader.Size + payload.Length];
        var header = new PacketHeader(type, sequence, (ushort)payload.Length, Crc32.Compute(payload));

        header.WriteTo(datagram);
        payload.CopyTo(datagram.AsSpan(PacketHeader.Size));
        return datagram;
    }

    public static byte[] Build(PacketType type, ushort sequence) =>
        Build(type, sequence, ReadOnlySpan<byte>.Empty);

    public static ParseResult TryParse(ReadOnlySpan<byte> datagram, out Packet? packet)
    {
        packet = null;

        if (!PacketHeader.TryRead(datagram, out var header))
            return ParseResult.TooShort;

        if (header.Version != PacketHeader.CurrentVersion)
            return ParseResult.UnknownVersion;

        var payload = datagram[PacketHeader.Size..];
        if (payload.Length != header.PayloadSize)
            return ParseResult.SizeMismatch;

        if (Crc32.Compute(payload) != header.Checksum)
            return ParseResult.ChecksumMismatch;

        packet = new Packet(header, payload.ToArray());
        return ParseResult.Ok;
    }

    public static string Describe(ParseResult result) => result switch
    {
        ParseResult.TooShort         => "датаграмма короче заголовка",
        ParseResult.UnknownVersion   => "неизвестная версия протокола",
        ParseResult.SizeMismatch     => "PayloadSize не совпадает",
        ParseResult.ChecksumMismatch => "CRC32 не сошлась",
        _                            => "пакет корректен",
    };

    public static string ToHex(ReadOnlySpan<byte> data, int maxBytes = 24)
    {
        var sb = new StringBuilder();
        int count = Math.Min(data.Length, maxBytes);

        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(data[i].ToString("X2"));
        }

        if (data.Length > count) sb.Append(" ...");
        return sb.ToString();
    }
}