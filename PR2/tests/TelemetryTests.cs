using Nomi.Telemetry;
using Xunit;

namespace Nomi.Tests;

public sealed class TelemetryTests
{
    [Fact]
    public void FirstSample_SetsSrtt()
    {
        var stats = new RttStatistics();
        stats.OnRttSample(100);

        Assert.Equal(100, stats.SrttMs);
        Assert.Equal(50,  stats.RttVarMs);
        Assert.Equal(0,   stats.JitterMs);
    }

    [Fact]
    public void LossRate_Calculated()
    {
        var stats = new RttStatistics();
        stats.OnSent();
        stats.OnSent();
        stats.OnLoss();

        Assert.Equal(0.5, stats.LossRate, 3);
    }

    [Fact]
    public void Duplicate_And_Late_Are_Classified()
    {
        var now = DateTimeOffset.UtcNow;
        var stats   = new RttStatistics();
        var tracker = new PingTracker(stats, TimeSpan.FromMilliseconds(100), () => now);

        tracker.RegisterSent(1, 1, 1000);
        Assert.Equal(PongResult.Accepted, tracker.HandlePong(1, 1000, 1050, out double rtt));
        Assert.Equal(50, rtt);

        Assert.Equal(PongResult.Duplicate, tracker.HandlePong(1, 1000, 1060, out _));

        tracker.RegisterSent(2, 2, 2000);
        now = now.AddMilliseconds(200);
        tracker.CheckTimeouts();

        Assert.Equal(PongResult.Late, tracker.HandlePong(2, 2000, 2300, out _));
    }

    [Fact]
    public void Unknown_Nonce_Is_Invalid()
    {
        var stats   = new RttStatistics();
        var tracker = new PingTracker(stats, TimeSpan.FromMilliseconds(100));

        Assert.Equal(PongResult.Unknown, tracker.HandlePong(999, 0, 0, out _));
        Assert.Equal(1, stats.Invalid);
    }
}