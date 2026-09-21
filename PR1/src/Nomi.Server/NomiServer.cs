using System.Net;
using System.Net.Sockets;

namespace Nomi.Net.Server;

public sealed class NomiServer : IDisposable
{
    private readonly UdpClient _socket;
    private readonly Logger _log;
    private readonly Dictionary<IPEndPoint, UserSession> _users = new();
    private readonly Dictionary<ushort, UserSession> _byId = new();

    private ushort _nextUserId = 1;
    private ushort _sequence;
    private int _receivedPackets;
    private int _rejectedPackets;

    public NomiServer(int port, Logger log)
    {
        _log = log;
        _socket = new UdpClient(new IPEndPoint(IPAddress.Any, port));

        if (OperatingSystem.IsWindows())
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            _socket.Client.IOControl(SIO_UDP_CONNRESET, new byte[4], null);
        }

        _log.Info($"Nomi UDP-сервер слушает 0.0.0.0:{port} (заголовок {PacketHeader.Size} байт)");
    }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await _socket.ReceiveAsync(token);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException ex)
            {
                _log.Warn($"Ошибка сокета: {ex.SocketErrorCode} ({ex.Message})");
                continue;
            }

            try { HandleDatagram(received.Buffer, received.RemoteEndPoint); }
            catch (Exception ex) { _log.Error($"Сбой обработки: {ex.Message}"); }
        }

        _log.Info($"Сервер остановлен. Принято: {_receivedPackets}, отвергнуто: {_rejectedPackets}.");
    }

    private void HandleDatagram(byte[] datagram, IPEndPoint sender)
    {
        _receivedPackets++;

        var parseResult = Packet.TryParse(datagram, out var packet);
        if (parseResult != ParseResult.Ok || packet is null)
        {
            _rejectedPackets++;
            _log.Warn($"Пакет от {sender} отвергнут: {Packet.Describe(parseResult)}.");
            return;
        }

        var header = packet.Header;
        _log.Packet($"{sender} <- {header} | данные: [{Packet.ToHex(packet.Payload)}]");

        if (header.Type == PacketType.Connect) { HandleConnect(packet, sender); return; }

        if (!_users.TryGetValue(sender, out var user))
        {
            _rejectedPackets++;
            SendError(sender, "Сначала отправьте CONNECT.");
            return;
        }

        user.LastSeenUtc = DateTime.UtcNow;

        if (header.Sequence <= user.LastSequence && header.Type != PacketType.Disconnect)
        {
            _rejectedPackets++;
            _log.Warn($"{user}: устаревший seq={header.Sequence} (последний {user.LastSequence}).");
            Send(sender, PacketType.StateUpdate, user.ToState(header.Sequence).ToBytes());
            return;
        }

        user.LastSequence = header.Sequence;

        switch (header.Type)
        {
            case PacketType.LocationUpdate: HandleLocation(packet, user); break;
            case PacketType.Reaction:       HandleReaction(packet, user); break;
            case PacketType.Disconnect:
                _log.Command($"{user}: DISCONNECT.");
                _users.Remove(sender); _byId.Remove(user.Id);
                break;
            default:
                _rejectedPackets++;
                SendError(sender, $"Тип {(byte)header.Type} не поддерживается.");
                break;
        }
    }

    private void HandleConnect(Packet packet, IPEndPoint sender)
    {
        if (!ConnectPayload.TryRead(packet.Payload, out var connect))
        {
            _rejectedPackets++;
            SendError(sender, "Некорректная нагрузка CONNECT.");
            return;
        }

        if (!_users.TryGetValue(sender, out var user))
        {
            var rnd = Random.Shared;
            user = new UserSession(_nextUserId++, connect.UserName, sender)
            {
                Lat = 55.751244f + (float)(rnd.NextDouble() - 0.5) * 0.01f,
                Lng = 37.618423f + (float)(rnd.NextDouble() - 0.5) * 0.01f,
                Alt = 150f,
            };
            _users[sender] = user;
            _byId[user.Id] = user;

            _log.Command($"{user}: CONNECT, старт ({user.Lat:F5}; {user.Lng:F5}). Всего: {_users.Count}.");
        }

        user.LastSequence = packet.Header.Sequence;
        user.LastSeenUtc = DateTime.UtcNow;
        Send(sender, PacketType.ConnectAck, user.ToState(packet.Header.Sequence).ToBytes());
    }

    private void HandleLocation(Packet packet, UserSession user)
    {
        if (!LocationPayload.TryRead(packet.Payload, out var location))
        {
            _rejectedPackets++;
            SendError(user.EndPoint, "Некорректная нагрузка LOCATION_UPDATE.");
            return;
        }

        float newLat = NomiRules.ClampLat(location.Lat);
        float newLng = NomiRules.ClampLng(location.Lng);
        float newAlt = NomiRules.ClampAlt(location.Alt);

        double distance = HaversineMeters(user.Lat, user.Lng, newLat, newLng);
        if (distance > NomiRules.MaxStepMeters)
        {
            _log.Warn($"{user}: шаг {distance:F0} м > {NomiRules.MaxStepMeters} м — отклонено.");
            Send(user.EndPoint, PacketType.StateUpdate, user.ToState(packet.Header.Sequence).ToBytes());
            return;
        }

        user.Lat = newLat; user.Lng = newLng; user.Alt = newAlt;

        _log.Command($"{user}: LOCATION_UPDATE seq={packet.Header.Sequence} " +
                     $"({user.Lat:F5}; {user.Lng:F5}; {user.Alt:F0} м), шаг {distance:F0} м");

        Send(user.EndPoint, PacketType.StateUpdate, user.ToState(packet.Header.Sequence).ToBytes());
    }

    private void HandleReaction(Packet packet, UserSession user)
    {
        if (!ReactionPayload.TryRead(packet.Payload, out var reaction))
        {
            _rejectedPackets++;
            SendError(user.EndPoint, "Некорректная нагрузка REACTION.");
            return;
        }

        string emoji = Emojis.Name(reaction.EmojiId);
        var now = DateTime.UtcNow;
        bool onCooldown = (now - user.LastReactionUtc).TotalMilliseconds < NomiRules.ReactionCooldownMs;
        bool knownEmoji = Emojis.IsKnown(reaction.EmojiId);
        bool targetExists = _byId.TryGetValue((ushort)reaction.TargetUid, out var target);

        byte delivered = 0;
        string outcome;

        if (!knownEmoji) outcome = "реакция неизвестна";
        else if (onCooldown) outcome = "кулдаун";
        else if (!targetExists) outcome = $"получатель #{reaction.TargetUid} не найден";
        else { user.LastReactionUtc = now; delivered = 1; outcome = $"доставлено {target!.Name}"; }

        _log.Command($"{user}: REACTION seq={packet.Header.Sequence} {emoji} -> #{reaction.TargetUid}: {outcome}");

        var result = new ReactionResultPayload(user.Id, reaction.EmojiId, delivered, packet.Header.Sequence);
        Send(user.EndPoint, PacketType.ReactionResult, result.ToBytes());
    }

    private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLng = (lng2 - lng1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                 * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private void SendError(IPEndPoint target, string message) =>
        Send(target, PacketType.Error, new ErrorPayload(message).ToBytes());

    private void Send(IPEndPoint target, PacketType type, ReadOnlySpan<byte> payload)
    {
        var datagram = Packet.Build(type, ++_sequence, payload);

        try
        {
            _socket.Send(datagram, datagram.Length, target);
            _log.Packet($"{target} -> {type} seq={_sequence} size={payload.Length}");
        }
        catch (SocketException ex)
        {
            _log.Warn($"Не отправить {type} на {target}: {ex.SocketErrorCode}");
        }
    }

    public void Dispose() => _socket.Dispose();
}