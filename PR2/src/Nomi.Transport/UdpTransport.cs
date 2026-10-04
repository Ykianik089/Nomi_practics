using System.Net;
using System.Net.Sockets;

namespace Nomi.Transport;

public sealed class UdpTransport : IUdpTransport
{
    private readonly UdpClient _udp;

    /// <summary>Bind to a local port. Pass 0 to let the OS pick a free port (client).</summary>
    public UdpTransport(int localPort)
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, localPort));
    }

    public ValueTask<UdpReceiveResult> ReceiveAsync(CancellationToken ct)
        => _udp.ReceiveAsync(ct);

    public ValueTask<int> SendAsync(
        ReadOnlyMemory<byte> datagram,
        IPEndPoint endpoint,
        CancellationToken ct)
        => _udp.SendAsync(datagram, endpoint, ct);

    public void Dispose() => _udp.Dispose();
}