using Nomi.Protocol;
using Xunit;

namespace Nomi.Tests;

public sealed class PacketSerializerTests
{
    [Fact]
    public void Ping_Roundtrip()
    {
        byte[] payload = PingPongCodec.EncodePing(123456789, 42);
        byte[] packet  = PacketSerializer.Serialize(PacketType.PING, 7, payload);

        Assert.True(PacketSerializer.TryDeserialize(packet, out var header, out var parsed, out _));
        Assert.Equal(PacketType.PING, header.Type);
        Assert.Equal(7, header.Sequence);

        Assert.True(PingPongCodec.TryDecodePing(parsed, out long ts, out uint nonce));
        Assert.Equal(123456789, ts);
        Assert.Equal(42u, nonce);
    }

    [Fact]
    public void BadCrc_Rejected()
    {
        byte[] packet = PacketSerializer.Serialize(PacketType.PING, 1, PingPongCodec.EncodePing(1, 1));
        packet[^1] ^= 0xFF;

        Assert.False(PacketSerializer.TryDeserialize(packet, out _, out _, out string? error));
        Assert.Contains("crc", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BadVersion_Rejected()
    {
        byte[] packet = PacketSerializer.Serialize(PacketType.PING, 1, PingPongCodec.EncodePing(1, 1));
        packet[0] = 99;

        Assert.False(PacketSerializer.TryDeserialize(packet, out _, out _, out string? error));
        Assert.Contains("version", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TooShort_Rejected()
    {
        byte[] packet = new byte[5];
        Assert.False(PacketSerializer.TryDeserialize(packet, out _, out _, out string? error));
        Assert.Contains("short", error!, StringComparison.OrdinalIgnoreCase);
    }
}