using System.Net;
using GServ.Game;
using GServ.Network;

var config = DevOnlyLocalServerCommandLine.Parse(args);
if (!config.Enabled)
{
    Console.WriteLine("GServ C# compatibility foundation initialized. Full server runtime is not implemented yet.");
    Console.WriteLine("Dev-only local shell is disabled. Pass --dev-only-local --dev-root <path> --dev-level <level.nw> to run the diagnostic shell.");
    return;
}

Console.WriteLine("WARNING: running DEV-ONLY local shell.");
Console.WriteLine("This is not production-compatible auth, server-list, movement, NPC, script, or file-transfer behavior.");
Console.WriteLine($"Root: {config.RootPath}");
Console.WriteLine($"Level: {config.LevelName}");
Console.WriteLine($"Port: {config.Port}");

var fileSystem = new IndexedServerFileSystem(config.RootPath);
fileSystem.AddDirectory("world", "*.nw", forceRecursive: true);
var pipeline = new DevOnlyLocalSessionPipeline(
    new DevOnlyLocalServerOptions(EnableDevOnlyAuth: true, LevelName: config.LevelName),
    new NwLevelFileLoader(fileSystem));

using var server = new DevOnlyLocalTcpServer(IPAddress.Any, config.Port, pipeline);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

server.Start();
Console.WriteLine("Listening. Press Ctrl+C to stop.");
while (!cts.IsCancellationRequested)
{
    try
    {
        var result = await server.AcceptOneAsync(cts.Token);
        foreach (var line in result.Log)
            Console.WriteLine(line);
        Console.WriteLine($"Connection stopped at {result.StopPoint}; lifecycle={result.Lifecycle}; accepted={result.Accepted}.");
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        break;
    }
}
