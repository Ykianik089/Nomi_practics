using System.Net;
using System.Net.Sockets;

namespace Nomi.Transport;

public interface IUdpTransport : IDisposable
{
    ValueTask<UdpReceiveResult> ReceiveAsync(CancellationToken ct);
    ValueTask<int> SendAsync(ReadOnlyMemory<byte> datagram, IPEndPoint endpoint, CancellationToken ct);
}