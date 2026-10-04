using System.Net;
using System.Net.Sockets;
using Nomi.Protocol;
using Nomi.Transport;

namespace Nomi.Server;

public sealed class UdpServer
{
    private readonly IUdpTransport _transport;

    public UdpServer(IUdpTransport transport) => _transport = transport;

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("[SERVER] listening...");

        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await _transport.ReceiveAsync(ct); }
            catch (OperationCanceledException) { break; }

            await HandleDatagramAsync(result, ct);
        }
    }

    private async Task HandleDatagramAsync(UdpReceiveResult result, CancellationToken ct)
    {
        if (!PacketSerializer.TryDeserialize(
                result.Buffer,
                out var header,
                out var payload,
                out var error))
        {
            Console.WriteLine($"[SERVER] invalid packet from {result.RemoteEndPoint}: {error}");
            return;
        }

        Console.WriteLine($"[SERVER] {header.Type} seq={header.Sequence} from {result.RemoteEndPoint}");

        switch (header.Type)
        {
            case PacketType.PING:
                if (!PingPongCodec.TryDecodePing(payload, out var clientTs, out var nonce))
                    return;

                long serverTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                byte[] pongPayload = PingPongCodec.EncodePong(clientTs, serverTs, nonce);
                byte[] response = PacketSerializer.Serialize(PacketType.PONG, header.Sequence, pongPayload);

                await _transport.SendAsync(response, result.RemoteEndPoint, ct);
                break;

            case PacketType.DISCONNECT:
                Console.WriteLine($"[SERVER] {result.RemoteEndPoint} disconnected");
                break;

            default:
                Console.WriteLine($"[SERVER] unhandled type {header.Type}");
                break;
        }
    }
}