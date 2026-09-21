using System.Text;
using Nomi.Net.Client;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "Nomi — UDP-клиент";

string host = ArgString(args, "--host", "127.0.0.1");
int port = ArgInt(args, "--port", 9050);
string name = ArgString(args, "--name", $"User-{Random.Shared.Next(100, 999)}");
int intervalMs = ArgInt(args, "--interval", 1000);
int count = ArgInt(args, "--count", 0);

Console.WriteLine("=== Nomi UDP-клиент, практическая работа №1 ===");
Console.WriteLine($"Сервер: {host}:{port} | пользователь: {name} | интервал {intervalMs} мс");
Console.WriteLine();

using var cts = new CancellationTokenSource();
using var client = new NomiClient(host, port, name);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Ctrl+C — отключаемся...");
    cts.Cancel();
};

if (!await client.HandshakeAsync(cts.Token))
{
    Console.WriteLine($"Не удалось выполнить рукопожатие с {host}:{port}. Сервер запущен?");
    return 1;
}

await client.RunAsync(TimeSpan.FromMilliseconds(intervalMs), count, cts.Token);

client.SendDisconnect();
client.PrintSummary();
return 0;

static string ArgString(string[] args, string name, string fallback)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
}

static int ArgInt(string[] args, string name, int fallback) =>
    int.TryParse(ArgString(args, name, fallback.ToString()), out int value) ? value : fallback;