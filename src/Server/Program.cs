using System.Net;
using Preagonal.GServer.Game;
using Preagonal.GServer.Network;
using Preagonal.GServer.Persistence;
using Preagonal.GServer.Protocol;

var config = LocalDebugCommandLine.Parse(args);
if (!config.Enabled)
{
    Console.WriteLine("Preagonal.GServer C# runtime initialized.");
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

    var serverRoot = snapshot.Resolution.ServerPath!;
    var resourceFileSystems = LoadResourceFileSystems(serverRoot, snapshot.ServerOptions);
    var levelLoader = new NwLevelFileLoader(resourceFileSystems.Get(ServerFileSystemKind.All));
    var staffAccounts = SplitCsv(snapshot.ServerOptions.GetString("staff", ""));
    var authBridge = new LoginAuthBridge(
        serverListSocket,
        new PreWorldAuthOptions(
            MaxPlayers: snapshot.ServerOptions.GetInt("maxplayers", 128),
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: serverListResult.Connected,
            AllowedVersions: serverListOptions.AllowedVersions,
            AllowedVersionText: string.Join(", ", serverListOptions.AllowedVersions)),
        new LoginWorldEntryOptions(
            new DiskAccountFileSystem(serverRoot),
            snapshot.ServerOptions,
            levelLoader,
            new FileLevelLookup(levelLoader),
            new AccountLoginOptions(
                OnlyStaff: snapshot.ServerOptions.GetBool("onlystaff", false),
                ServerName: serverListOptions.Name,
                ActiveSessions: [],
                StaffAccounts: staffAccounts,
                RemoteIp: "")),
        runtimeServer);
    var clientConnections = new TcpClientConnectionRegistry();
    using var clientServer = new ClientTcpServer(
        IPAddress.Any,
        gamePort,
        new LoginSocketFrameHandler(authBridge, clientConnections),
        clientConnections,
        session => Console.WriteLine($"Accepted client session {session.PlayerId} from {session.RemoteAddress}."));

    var nextServerListKeepalive = DateTimeOffset.UtcNow.AddMinutes(1);
    runtime.ServerListTimedEventsHandler = () =>
    {
        if (!serverListSocket.IsConnected || DateTimeOffset.UtcNow < nextServerListKeepalive)
            return;

        nextServerListKeepalive = DateTimeOffset.UtcNow.AddMinutes(1);
        var ip = snapshot.ServerOptions.GetString("serverip", "AUTO");
        if (string.IsNullOrEmpty(ip))
            ip = "AUTO";

        serverListSocket.SendPacket(ServerListAuthPackets.SetIp(ip));
        Console.WriteLine($"Sent listserver set-ip keepalive: {ip}.");
    };

    runtime.CleanupHandler = () =>
    {
        runtimeServer.CleanupForShutdown(player => authBridge.EndClientSession(player.Id));
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
    var acceptTask = clientServer.RunAsync(productionCts.Token, result =>
    {
        var endResult = authBridge.EndClientSession(result.PlayerId);
        foreach (var broadcast in endResult.Broadcasts)
        {
            if (broadcast.OutboundBytes.Length != 0)
                clientConnections.SendAsync(broadcast.PlayerId, broadcast.OutboundBytes, productionCts.Token).AsTask().GetAwaiter().GetResult();
        }

        var saveResult = endResult.SaveResult;
        if (saveResult is { WriteAttempted: true })
            Console.WriteLine($"Saved account for client session {result.PlayerId}: writeSucceeded={saveResult.WriteSucceeded}; path={saveResult.Path}");

        Console.WriteLine(string.IsNullOrEmpty(result.Diagnostic)
            ? $"Client session {result.PlayerId} stopped: {result.StopReason}."
            : $"Client session {result.PlayerId} stopped: {result.StopReason}; {result.Diagnostic}");
    });
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
    new LocalDebugOptions(EnableLocalDebugAuth: true, LevelName: config.LevelName),
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

            var rawPacketId = packet[0];
            var packetId = (ListServerToServerPacketId)DecodeGChar(rawPacketId);
            Console.WriteLine($"Listserver packet raw={rawPacketId} decoded={(byte)packetId} ({packetId}) received: {packet.Length} bytes.");

            switch (packetId)
            {
                case ListServerToServerPacketId.VerifyAccount2:
                {
                    var result = authBridge.HandleVerifyAccount2(packet.AsSpan(1));
                    if (result.OutboundBytes.Length != 0)
                        await clientConnections.SendAsync(result.PlayerId, result.OutboundBytes, cancellationToken);
                    foreach (var broadcast in result.Broadcasts)
                    {
                        if (broadcast.OutboundBytes.Length != 0)
                            await clientConnections.SendAsync(broadcast.PlayerId, broadcast.OutboundBytes, cancellationToken);
                    }
                    break;
                }
                case ListServerToServerPacketId.Ping:
                    Console.WriteLine("Replying to listserver ping.");
                    serverListSocket.SendPacket(ServerListAuthPackets.Ping());
                    break;
                case ListServerToServerPacketId.ErrorMessage:
                    Console.WriteLine($"Listserver error: {System.Text.Encoding.ASCII.GetString(packet.AsSpan(1))}");
                    break;
                case ListServerToServerPacketId.SendText:
                    Console.WriteLine($"Listserver text: {PreviewAscii(packet.AsSpan(1))}");
                    break;
                case ListServerToServerPacketId.RequestText:
                    Console.WriteLine($"Listserver request text: {PreviewAscii(packet.AsSpan(1))}");
                    break;
                case ListServerToServerPacketId.ServerInfo:
                    Console.WriteLine($"Listserver server info: {PreviewAscii(packet.AsSpan(1))}");
                    break;
            }
        }
    }
}

static ServerResourceFileSystems LoadResourceFileSystems(string serverRoot, Gs2Settings options)
{
    var foldersConfig = Path.Combine(serverRoot, "config", "foldersconfig.txt");
    if (!options.GetBool("nofoldersconfig", false) && File.Exists(foldersConfig))
        return ServerResourceFileSystems.LoadFolderConfig(serverRoot, File.ReadAllText(foldersConfig));

    return ServerResourceFileSystems.LoadAllFolders(serverRoot, options.GetString("sharefolder", ""));
}

static IReadOnlyList<string> SplitCsv(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(entry => entry.Length > 0 && !entry.StartsWith('('))
        .ToArray();

static byte DecodeGChar(byte value) => unchecked((byte)(value - 32));

static string PreviewAscii(ReadOnlySpan<byte> bytes)
{
    var text = System.Text.Encoding.ASCII.GetString(bytes);
    text = text.Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);
    return text.Length <= 180 ? text : text[..180] + "...";
}
