namespace Nomi.Telemetry;

public sealed class RttStatistics
{
    private const double Alpha = 0.125;         // 1/8
    private const double Beta = 0.25;           // 1/4
    private const double JitterAlpha = 0.0625;  // 1/16

    private double _lastRtt;
    private bool _hasLast;

    public int Sent { get; private set; }
    public int Received { get; private set; }
    public int Lost { get; private set; }
    public int Duplicates { get; private set; }
    public int Late { get; private set; }
    public int Invalid { get; private set; }

    public double LastRttMs { get; private set; }
    public double SrttMs { get; private set; }
    public double RttVarMs { get; private set; }
    public double JitterMs { get; private set; }

    public double LossRate => Sent == 0 ? 0 : (double)Lost / Sent;

    public void OnSent()      => Sent++;
    public void OnInvalid()   => Invalid++;
    public void OnDuplicate() => Duplicates++;
    public void OnLate()      => Late++;
    public void OnLoss()      => Lost++;

    public void OnRttSample(double rttMs)
    {
        Received++;
        LastRttMs = rttMs;

        if (!_hasLast)
        {
            SrttMs = rttMs;
            RttVarMs = rttMs / 2.0;
            JitterMs = 0;
            _lastRtt = rttMs;
            _hasLast = true;
            return;
        }

        RttVarMs = (1 - Beta) * RttVarMs + Beta * Math.Abs(SrttMs - rttMs);
        SrttMs   = (1 - Alpha) * SrttMs + Alpha * rttMs;
        JitterMs = (1 - JitterAlpha) * JitterMs + JitterAlpha * Math.Abs(rttMs - _lastRtt);
        _lastRtt = rttMs;
    }
}