using System.Globalization;

namespace Nomi.Telemetry;

public sealed class CsvLatencyWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public CsvLatencyWriter(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _writer = new StreamWriter(path, append: true);

        if (_writer.BaseStream.Length == 0)
            _writer.WriteLine(
                "timestamp_utc,sequence,nonce,event,rtt_ms,srtt_ms,jitter_ms,loss_rate,status");
    }

    public void Write(
        DateTime utc,
        ushort sequence,
        uint nonce,
        string eventName,
        RttStatistics stats,
        double? rttMs = null,
        string status = "")
    {
        _writer.WriteLine(string.Join(',',
            utc.ToString("O", CultureInfo.InvariantCulture),
            sequence.ToString(CultureInfo.InvariantCulture),
            nonce.ToString(CultureInfo.InvariantCulture),
            eventName,
            rttMs?.ToString("F3", CultureInfo.InvariantCulture) ?? "",
            stats.SrttMs.ToString("F3", CultureInfo.InvariantCulture),
            stats.JitterMs.ToString("F3", CultureInfo.InvariantCulture),
            stats.LossRate.ToString("F4", CultureInfo.InvariantCulture),
            status));

        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();
}