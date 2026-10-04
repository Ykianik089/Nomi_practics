using System.Net;
using Nomi.Server;
using Nomi.Transport;

int port = 5000;

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--port")
        port = int.Parse(args[i + 1]);
}

using var transport = new UdpTransport(port);
var server = new UdpServer(transport);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try { await server.RunAsync(cts.Token); }
catch (OperationCanceledException) { }

Console.WriteLine("[SERVER] stopped");