using System.Net;
using GServ.Game;
using GServ.Network;
using GServ.Persistence;

var config = DevOnlyLocalServerCommandLine.Parse(args);
if (!config.Enabled)
{
    Console.WriteLine("GServ C# compatibility foundation initialized. Full server runtime is not implemented yet.");
    var productionArgs = ProductionStartupCommandLine.Parse(args, Environment.GetEnvironmentVariable);
    if (productionArgs.ShowHelp)
    {
        Console.WriteLine("Confirmed C++ options: -h, --help, -s/--server, -p/--port, --localip, --serverip, --interface, --staff, --name.");
        Console.WriteLine("Dev-only shell: --dev-only-local --dev-root <path> --dev-level <level.nw> [--port <port>].");
        return;
    }

    var snapshot = ProductionStartupLoader.Load(Environment.CurrentDirectory, productionArgs);
    if (snapshot.Resolution.Success)
    {
        Console.WriteLine($"Production startup resolved server '{snapshot.Resolution.ServerName}' from {snapshot.Resolution.Source}.");
        Console.WriteLine($"Server path: {snapshot.Resolution.ServerPath}");
        Console.WriteLine($"config/serveroptions.txt opened: {snapshot.ServerOptions.IsOpened}");
        Console.WriteLine($"config/adminconfig.txt opened: {snapshot.AdminConfig.IsOpened}");
    }
    else
    {
        Console.WriteLine(snapshot.Resolution.Diagnostic);
        return;
    }

    var runtime = new ProductionHostRuntime(new RuntimeServer());
    var hostLoop = new ProductionHostLoop(runtime, ProductionHostLoop.StaticTime, TimeSpan.Zero);

    using var productionCts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        productionCts.Cancel();
    };

    Console.WriteLine("Production startup resolved. Running host loop skeleton. Press Ctrl+C to stop.");
    hostLoop.Run(TimeSpan.FromMilliseconds(5), productionCts.Token);
    return;
}

Console.WriteLine("WARNING: running DEV-ONLY local shell.");
Console.WriteLine("This is not production-compatible auth, server-list, movement, NPC, script, or file-transfer behavior.");
Console.WriteLine($"Root: {config.RootPath}");
Console.WriteLine($"Level: {config.LevelName}");
Console.WriteLine($"Port: {config.Port}");

var fileSystems = ServerResourceFileSystems.LoadAllFolders(config.RootPath, shareFolder: string.Empty);
var fileSystem = fileSystems.Get(ServerFileSystemKind.All);
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
