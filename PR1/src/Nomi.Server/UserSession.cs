using System.Net;

namespace Nomi.Net.Server;

public sealed class UserSession
{
    public ushort Id { get; }
    public string Name { get; }
    public IPEndPoint EndPoint { get; }

    public float Lat { get; set; }
    public float Lng { get; set; }
    public float Alt { get; set; }

    public byte Status { get; set; } = NomiRules.StatusOnline;

    public ushort LastSequence { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastReactionUtc { get; set; } = DateTime.MinValue;

    public UserSession(ushort id, string name, IPEndPoint endPoint)
    {
        Id = id;
        Name = name;
        EndPoint = endPoint;
    }

    public StatePayload ToState(ushort ackSequence) =>
        new(Id, Lat, Lng, Alt, Status, ackSequence);

    public override string ToString() => $"#{Id} «{Name}» {EndPoint}";
}

public static class NomiRules
{
    public const byte StatusOnline    = 1;
    public const byte StatusInvisible = 2;

    public const float MaxStepMeters = 500f;
    public const int ReactionCooldownMs = 1000;

    public static float ClampLat(float lat) => Math.Clamp(lat, -90f, 90f);
    public static float ClampLng(float lng) => Math.Clamp(lng, -180f, 180f);
    public static float ClampAlt(float alt) => Math.Clamp(alt, -100f, 9000f);
}