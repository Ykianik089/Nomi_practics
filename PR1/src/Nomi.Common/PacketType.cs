namespace Nomi.Net;

/// <summary>
/// Тип команды (пакета). Занимает 1 байт в заголовке.
/// Диапазоны зарезервированы, чтобы протокол можно было расширять
/// механиками геосоциального приложения Nomi без ломки уже написанного кода.
/// </summary>
public enum PacketType : byte
{
    // --- служебные (1..9) ---
    Connect    = 1,
    ConnectAck = 2,
    Disconnect = 3,
    Error      = 9,

    // --- команды клиента (10..19) ---
    LocationUpdate = 10,
    Reaction       = 11,

    // зарезервировано: CheckIn = 12, SendMessage = 13, CreateMark = 14, StatusUpdate = 15, RouteRequest = 16

    // --- ответы сервера (20..29) ---
    StateUpdate    = 20,
    ReactionResult = 21,
}