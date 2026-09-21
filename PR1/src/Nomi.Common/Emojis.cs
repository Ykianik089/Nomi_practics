namespace Nomi.Net;

/// <summary>Эмодзи-реакции Nomi.</summary>
public static class Emojis
{
    public const byte Wave  = 1; // 👋
    public const byte Heart = 2; // ❤️
    public const byte Laugh = 3; // 😂
    public const byte Fire  = 4; // 🔥
    public const byte Eyes  = 5; // 👀

    public static string Name(byte emojiId) => emojiId switch
    {
        Wave  => "👋 привет",
        Heart => "❤️ люблю",
        Laugh => "😂 смешно",
        Fire  => "🔥 класс",
        Eyes  => "👀 слежу",
        _     => $"неизвестная реакция #{emojiId}",
    };

    public static bool IsKnown(byte emojiId) =>
        emojiId is Wave or Heart or Laugh or Fire or Eyes;
}