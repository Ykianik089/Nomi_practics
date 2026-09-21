using System.Net.Sockets;

namespace Nomi.Net.Client;

public sealed class NomiClient : IDisposable
{
    private readonly UdpClient _socket = new();
    private readonly string _host;
    private readonly int _port;
    private readonly string _userName;
    private readonly Random _random = new();

    private ushort _sequence;
    private ushort _userId;
    private int _sent;
    private int _received;

    private float _lat = 55.751244f;
    private float _lng = 37.618423f;
    private float _alt = 150f;

    public NomiClient(string host, int port, string userName)
    {
        _host = host; _port = port; _userName = userName;
        _socket.Connect(host, port);
    }

    public async Task<bool> HandshakeAsync(CancellationToken token)
    {
        for (int attempt = 1; attempt <= 5 && !token.IsCancellationRequested; attempt++)
        {
            var payload = new ConnectPayload(_userName).ToBytes();
            Send(PacketType.Connect, payload);
            Print(ConsoleColor.DarkGray, $"-> CONNECT seq={_sequence} имя=«{_userName}» (попытка {attempt}/5)");

            var packet = await ReceiveWithTimeoutAsync(TimeSpan.FromSeconds(1), token);
            if (packet is null) { Print(ConsoleColor.Yellow, "   ответа нет, повторяем..."); continue; }

            if (packet.Header.Type == PacketType.Error)
            {
                Print(ConsoleColor.Red, $"<- ERROR: {ErrorPayload.Read(packet.Payload).Message}");
                return false;
            }

            if (packet.Header.Type == PacketType.ConnectAck &&
                StatePayload.TryRead(packet.Payload, out var state))
            {
                _userId = state.UserId;
                _lat = state.Lat; _lng = state.Lng; _alt = state.Alt;
                Print(ConsoleColor.Green,
                      $"<- CONNECT_ACK: id={state.UserId}, " +
                      $"({state.Lat:F5}; {state.Lng:F5}; {state.Alt:F0} м), статус={state.Status}");
                return true;
            }
        }
        return false;
    }

    public async Task RunAsync(TimeSpan interval, int commandLimit, CancellationToken token)
    {
        var receiving = Task.Run(() => ReceiveLoopAsync(token), token);
        int tick = 0;
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (tick % 2 == 0) SendLocation();
                else SendReaction();

                tick++;
                if (commandLimit > 0 && tick >= commandLimit) break;
            }
        }
        catch (OperationCanceledException) { }

        await Task.WhenAny(receiving, Task.Delay(300, CancellationToken.None));
    }

    private void SendLocation()
    {
        bool cheat = _sequence % 8 == 7;
        if (cheat)
        {
            _lat += 1.0f; _lng += 1.0f;
            Print(ConsoleColor.DarkRed, "   [демо] намеренно большой шаг ~111 км");
        }
        else
        {
            _lat += (float)(_random.NextDouble() - 0.5) * 0.0005f;
            _lng += (float)(_random.NextDouble() - 0.5) * 0.0005f;
        }

        var payload = new LocationPayload(_lat, _lng, _alt).ToBytes();
        Send(PacketType.LocationUpdate, payload);

        Print(ConsoleColor.DarkCyan,
              $"-> LOCATION_UPDATE seq={_sequence} ({_lat:F5}; {_lng:F5}; {_alt:F0} м)" +
              (cheat ? " [завышено]" : ""));
    }

    private void SendReaction()
    {
        byte emoji = (byte)_random.Next(1, 6);
        uint targetUid = (uint)_random.Next(1, 4);

        var payload = new ReactionPayload(emoji, targetUid).ToBytes();
        Send(PacketType.Reaction, payload);

        Print(ConsoleColor.DarkYellow, $"-> REACTION seq={_sequence} {Emojis.Name(emoji)} -> #{targetUid}");
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var packet = await ReceiveWithTimeoutAsync(Timeout.InfiniteTimeSpan, token);
            if (packet is null)
            {
                try { await Task.Delay(100, token); } catch (OperationCanceledException) { break; }
                continue;
            }
            PrintServerAnswer(packet);
        }
    }

    private void PrintServerAnswer(Packet packet)
    {
        switch (packet.Header.Type)
        {
            case PacketType.StateUpdate when StatePayload.TryRead(packet.Payload, out var state):
                Print(ConsoleColor.Cyan,
                      $"<- STATE_UPDATE (ack seq={state.AckSequence}): " +
                      $"({state.Lat:F5}; {state.Lng:F5}; {state.Alt:F0} м), статус={state.Status}");
                break;

            case PacketType.ReactionResult when ReactionResultPayload.TryRead(packet.Payload, out var r):
                Print(ConsoleColor.Yellow,
                      $"<- REACTION_RESULT (ack seq={r.AckSequence}): {Emojis.Name(r.EmojiId)} — " +
                      $"{(r.Delivered == 1 ? "доставлено" : "не доставлено")}");
                break;

            case PacketType.Error:
                Print(ConsoleColor.Red, $"<- ERROR: {ErrorPayload.Read(packet.Payload).Message}");
                break;

            default:
                Print(ConsoleColor.DarkGray, $"<- {packet.Header}");
                break;
        }
    }

    private async Task<Packet?> ReceiveWithTimeoutAsync(TimeSpan timeout, CancellationToken token)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token);

        try
        {
            var result = await _socket.ReceiveAsync(linked.Token);
            _received++;

            var parseResult = Packet.TryParse(result.Buffer, out var packet);
            if (parseResult != ParseResult.Ok || packet is null)
            {
                Print(ConsoleColor.Red, $"<- пакет отброшен: {Packet.Describe(parseResult)}");
                return null;
            }
            return packet;
        }
        catch (OperationCanceledException) { return null; }
        catch (SocketException ex)
        {
            Print(ConsoleColor.Red, $"<- сокет: {ex.SocketErrorCode}");
            return null;
        }
    }

    public void SendDisconnect()
    {
        try
        {
            Send(PacketType.Disconnect, ReadOnlySpan<byte>.Empty);
            Print(ConsoleColor.DarkGray, $"-> DISCONNECT seq={_sequence}");
        }
        catch (SocketException) { }
    }

    private void Send(PacketType type, ReadOnlySpan<byte> payload)
    {
        var datagram = Packet.Build(type, ++_sequence, payload);
        _socket.Send(datagram, datagram.Length);
        _sent++;
    }

    private static void Print(ConsoleColor color, string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        Console.ForegroundColor = previous;
    }

    public void PrintSummary() =>
        Print(ConsoleColor.White, $"Итого: отправлено {_sent}, получено {_received}, id={_userId}.");

    public void Dispose() => _socket.Dispose();
}