namespace Nomi.Telemetry;

public enum PongResult
{
    Accepted,
    Duplicate,
    Late,
    Unknown,
    Invalid
}

public sealed class PingTracker
{
    private sealed record PendingPing(ushort Sequence, long ClientSendTimestampMs, DateTimeOffset SentAt);

    private readonly object _gate = new();
    private readonly Dictionary<uint, PendingPing> _pending = new();
    private readonly HashSet<uint> _completed = new();
    private readonly HashSet<uint> _timedOut = new();
    private readonly RttStatistics _stats;
    private readonly TimeSpan _timeout;
    private readonly Func<DateTimeOffset> _clock;

    public PingTracker(RttStatistics stats, TimeSpan timeout, Func<DateTimeOffset>? clock = null)
    {
        _stats = stats;
        _timeout = timeout;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public void RegisterSent(uint nonce, ushort sequence, long clientSendTimestampMs)
    {
        lock (_gate)
        {
            _pending[nonce] = new PendingPing(sequence, clientSendTimestampMs, _clock());
            _stats.OnSent();
        }
    }

    public PongResult HandlePong(uint nonce, long clientSendTimestampMs, long nowTimestampMs, out double rttMs)
    {
        rttMs = 0;

        lock (_gate)
        {
            if (_completed.Contains(nonce)) { _stats.OnDuplicate(); return PongResult.Duplicate; }
            if (_timedOut.Contains(nonce))  { _stats.OnLate();      return PongResult.Late; }
            if (!_pending.TryGetValue(nonce, out var pending))
            {
                _stats.OnInvalid();
                return PongResult.Unknown;
            }

            _pending.Remove(nonce);
            _completed.Add(nonce);

            rttMs = Math.Max(0, nowTimestampMs - pending.ClientSendTimestampMs);
            _stats.OnRttSample(rttMs);
            return PongResult.Accepted;
        }
    }

    public IReadOnlyList<ushort> CheckTimeouts()
    {
        var lost = new List<ushort>();
        var now = _clock();

        lock (_gate)
        {
            foreach (var pair in _pending.ToArray())
            {
                if (now - pair.Value.SentAt <= _timeout)
                    continue;

                _pending.Remove(pair.Key);
                _timedOut.Add(pair.Key);
                _stats.OnLoss();
                lost.Add(pair.Value.Sequence);
            }
        }

        return lost;
    }
}