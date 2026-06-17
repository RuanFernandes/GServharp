using System.Net;
using GServ.Game;
using GServ.Network;
using GServ.Persistence;
using GServ.Protocol;

var config = LocalDebugServerCommandLine.Parse(args);
if (!config.Enabled)
{
    Console.WriteLine("GServ C# compatibility foundation initialized. Full server runtime is not implemented yet.");
    var productionArgs = ServerStartupCommandLine.Parse(args, Environment.GetEnvironmentVariable);
    if (productionArgs.ShowHelp)
    {
        Console.WriteLine("Confirmed C++ options: -h, --help, -s/--server, -p/--port, --localip, --serverip, --interface, --staff, --name.");
        Console.WriteLine("Local debug shell: --local-debug --dev-root <path> --dev-level <level.nw> [--port <port>].");
        return;
    }

    var snapshot = ServerStartupLoader.Load(Environment.CurrentDirectory, productionArgs);
    if (snapshot.Resolution.Success)
    {
        Console.WriteLine($"Server startup resolved server '{snapshot.Resolution.ServerName}' from {snapshot.Resolution.Source}.");
        Console.WriteLine($"Server path: {snapshot.Resolution.ServerPath}");
        Console.WriteLine($"config/serveroptions.txt opened: {snapshot.ServerOptions.IsOpened}");
        Console.WriteLine($"config/adminconfig.txt opened: {snapshot.AdminConfig.IsOpened}");
    }
    else
    {
        Console.WriteLine(snapshot.Resolution.Diagnostic);
        return;
    }

    var runtimeServer = new RuntimeServer();
    var runtimeLevelCache = new RuntimeLevelCache();
    var runtime = new ServerHostRuntime(runtimeServer);
    using var serverListSocket = new ServerListTcpSocket();
    var serverListOptions = ServerListStartupOptions.FromStartupSnapshot(snapshot, productionArgs);
    var serverListResult = new ServerListLifecycle(serverListSocket).ConnectServer(serverListOptions);
    Console.WriteLine(serverListResult.Connected
        ? $"Registered '{serverListOptions.Name}' with list server {serverListOptions.ListIp}:{serverListOptions.ListPort}."
        : $"Could not connect/register with list server {serverListOptions.ListIp}:{serverListOptions.ListPort}.");
    if (!int.TryParse(serverListOptions.ServerPort, out var gamePort))
    {
        Console.WriteLine($"Invalid serverport '{serverListOptions.ServerPort}'.");
        return;
    }

    var authBridge = new LoginAuthBridge(
        serverListSocket,
        new PreWorldAuthOptions(
            MaxPlayers: snapshot.ServerOptions.GetInt("maxplayers", 128),
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: serverListResult.Connected,
            AllowedVersions: serverListOptions.AllowedVersions,
            AllowedVersionText: string.Join(", ", serverListOptions.AllowedVersions)));
    var clientConnections = new TcpClientConnectionRegistry();
    using var clientServer = new ClientTcpServer(
        IPAddress.Any,
        gamePort,
        new LoginSocketFrameHandler(authBridge),
        clientConnections);

    runtime.CleanupHandler = () =>
    {
        runtimeServer.CleanupForShutdown(player => { });
        runtimeLevelCache.Clear();
    };
    var hostLoop = new ServerHostLoop(runtime, ServerHostLoop.StaticTime, TimeSpan.Zero);

    using var productionCts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        productionCts.Cancel();
    };

    clientServer.Start();
    var acceptTask = RunClientAcceptLoop(clientServer, productionCts.Token);
    var listServerReceiveTask = serverListResult.Connected
        ? RunServerListReceiveLoop(serverListSocket, authBridge, clientConnections, productionCts.Token)
        : Task.CompletedTask;

    Console.WriteLine($"Server startup resolved. Listening for clients on port {gamePort}. Press Ctrl+C to stop.");
    hostLoop.Run(TimeSpan.FromMilliseconds(5), productionCts.Token);
    await Task.WhenAll(acceptTask, listServerReceiveTask);
    return;
}

Console.WriteLine("WARNING: running LOCAL DEBUG local shell.");
Console.WriteLine("This is not production-compatible auth, server-list, movement, NPC, script, or file-transfer behavior.");
Console.WriteLine($"Root: {config.RootPath}");
Console.WriteLine($"Level: {config.LevelName}");
Console.WriteLine($"Port: {config.Port}");

var fileSystems = ServerResourceFileSystems.LoadAllFolders(config.RootPath, shareFolder: string.Empty);
var fileSystem = fileSystems.Get(ServerFileSystemKind.All);
var pipeline = new LocalDebugSessionPipeline(
    new LocalDebugServerOptions(EnableLocalDebugAuth: true, LevelName: config.LevelName),
    new NwLevelFileLoader(fileSystem));

using var server = new LocalDebugTcpServer(IPAddress.Any, config.Port, pipeline);
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

static async Task RunClientAcceptLoop(ClientTcpServer server, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            var result = await server.AcceptOneAsync(cancellationToken);
            Console.WriteLine($"Client session {result.PlayerId} stopped: {result.StopReason}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client accept loop failed: {ex.Message}");
        }
    }
}

static async Task RunServerListReceiveLoop(
    ServerListTcpSocket serverListSocket,
    LoginAuthBridge authBridge,
    TcpClientConnectionRegistry clientConnections,
    CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested && serverListSocket.IsConnected)
    {
        IReadOnlyList<byte[]> packets;
        try
        {
            packets = await serverListSocket.ReceivePacketsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Listserver receive loop failed: {ex.Message}");
            break;
        }

        foreach (var packet in packets)
        {
            if (packet.Length == 0)
                continue;

            if (packet[0] != (byte)ListServerToServerPacketId.VerifyAccount2)
                continue;

            var result = authBridge.HandleVerifyAccount2(packet.AsSpan(1));
            if (result.OutboundBytes.Length != 0)
                await clientConnections.SendAsync(result.PlayerId, result.OutboundBytes, cancellationToken);
        }
    }
}
