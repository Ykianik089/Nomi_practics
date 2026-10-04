namespace Nomi.Protocol;

public enum PacketType : byte
{
    CONNECT         = 1,
    CONNECT_ACK     = 2,
    LOCATION_UPDATE = 3,
    REACTION        = 4,
    STATE_UPDATE    = 5,
    REACTION_RESULT = 6,
    DISCONNECT      = 7,
    ERROR           = 8,

    // Практика №2
    PING            = 17,
    PONG            = 18
}