using System.Net;
using System.Net.Sockets;
using Nomi.Protocol;
using Nomi.Telemetry;
using Nomi.Transport;

namespace Nomi.Client;

public sealed class PingClient
{
    private readonly IUdpTransport _transport;
    private readonly IPEndPoint _server;
    private readonly RttStatistics _stats = new();
    private readonly PingTracker _tracker;
    private readonly CsvLatencyWriter _csv;

    private ushort _sequence;
    private uint _nonce;

    public PingClient(IUdpTransport transport, IPEndPoint server, string csvPath)
    {
        _transport = transport;
        _server = server;
        _tracker = new PingTracker(_stats, TimeSpan.FromMilliseconds(500));
        _csv = new CsvLatencyWriter(csvPath);
    }

    public async Task RunAsync(TimeSpan interval, TimeSpan duration, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(duration);

        Task receiveTask  = ReceiveLoopAsync(linked.Token);
        Task timeoutTask  = TimeoutLoopAsync(linked.Token);
        Task sendTask     = SendLoopAsync(interval, linked.Token);

        try { await Task.WhenAll(sendTask, receiveTask, timeoutTask); }
        catch (OperationCanceledException) { }

        PrintSummary();
    }

    private async Task SendLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ushort seq = ++_sequence;
            uint nonce = ++_nonce;
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _tracker.RegisterSent(nonce, seq, nowMs);

            byte[] payload = PingPongCodec.EncodePing(nowMs, nonce);
            byte[] packet  = PacketSerializer.Serialize(PacketType.PING, seq, payload);

            await _transport.SendAsync(packet, _server, ct);

            _csv.Write(DateTime.UtcNow, seq, nonce, "SENT", _stats);

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await _transport.ReceiveAsync(ct); }
            catch (OperationCanceledException) { break; }

            if (!PacketSerializer.TryDeserialize(result.Buffer, out var header, out var payload, out _))
            {
                _stats.OnInvalid();
                continue;
            }

            if (header.Type != PacketType.PONG)
                continue;

            if (!PingPongCodec.TryDecodePong(payload, out _, out _, out uint nonce))
            {
                _stats.OnInvalid();
                continue;
            }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PongResult status = _tracker.HandlePong(nonce, 0, nowMs, out double rtt);

            _csv.Write(DateTime.UtcNow, header.Sequence, nonce, status.ToString(), _stats, rtt, status.ToString());

            Console.WriteLine(
                $"[CLIENT] {status,-9} seq={header.Sequence} rtt={rtt:F2} ms " +
                $"srtt={_stats.SrttMs:F2} jitter={_stats.JitterMs:F2} loss={_stats.LossRate:P1}");
        }
    }

    private async Task TimeoutLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(100, ct); }
            catch (OperationCanceledException) { break; }

            foreach (ushort seq in _tracker.CheckTimeouts())
                _csv.Write(DateTime.UtcNow, seq, 0, "LOSS", _stats, null, "timeout");
        }
    }

    private void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine($"Sent:       {_stats.Sent}");
        Console.WriteLine($"Received:   {_stats.Received}");
        Console.WriteLine($"Lost:       {_stats.Lost}");
        Console.WriteLine($"Duplicates: {_stats.Duplicates}");
        Console.WriteLine($"Late:       {_stats.Late}");
        Console.WriteLine($"Invalid:    {_stats.Invalid}");
        Console.WriteLine($"LossRate:   {_stats.LossRate:P2}");
        Console.WriteLine($"SRTT:       {_stats.SrttMs:F2} ms");
        Console.WriteLine($"Jitter:     {_stats.JitterMs:F2} ms");
    }
}