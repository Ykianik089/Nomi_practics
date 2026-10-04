using System.Net;
using Nomi.Client;
using Nomi.Transport;

string host = "127.0.0.1";
int port = 5000;
int durationSec = 60;
int intervalMs = 1000;
string csvPath = "docs/latency_samples.csv";

for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--host":     host = args[i + 1]; break;
        case "--port":     port = int.Parse(args[i + 1]); break;
        case "--duration": durationSec = int.Parse(args[i + 1]); break;
        case "--interval": intervalMs = int.Parse(args[i + 1]); break;
        case "--csv":      csvPath = args[i + 1]; break;
    }
}

var server = new IPEndPoint(IPAddress.Parse(host), port);
using var transport = new UdpTransport(0);   // локальный порт выбирает ОС
var client = new PingClient(transport, server, csvPath);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"[CLIENT] -> {server}  duration={durationSec}s  interval={intervalMs}ms  csv={Path.GetFullPath(csvPath)}");
await client.RunAsync(TimeSpan.FromMilliseconds(intervalMs), TimeSpan.FromSeconds(durationSec), cts.Token);