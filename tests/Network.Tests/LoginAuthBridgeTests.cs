using Preagonal.GServer.Game;
using Preagonal.GServer.Persistence;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class LoginAuthBridgeTests
{
    [Fact]
    public void BeginClientLoginSendsVerifyAccountAndWaitsForServerListResponse()
    {
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(gateway, AuthOptions());

        var result = bridge.BeginClientLogin(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket());

        Assert.True(result.Accepted);
        Assert.Empty(result.OutboundBytes);
        Assert.Equal(SessionLifecycle.WaitingForServerListAuth, result.Lifecycle);
        Assert.Equal(
            ServerListAuthPackets.VerifyAccount2Request("Ruan", "pw", 7, PlayerSessionType.Client3, "win"),
            Assert.Single(gateway.SentPackets));
    }

    [Fact]
    public void ExtraFrameWhileAuthPendingDoesNotStartSecondLogin()
    {
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(gateway, AuthOptions());
        _ = bridge.BeginClientLogin(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket());

        var result = bridge.BeginClientLogin(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            [0x04, 0x6F, 0x76, 0x4E]);

        Assert.True(result.Accepted);
        Assert.Empty(result.OutboundBytes);
        Assert.Equal(SessionLifecycle.WaitingForServerListAuth, result.Lifecycle);
        Assert.Single(gateway.SentPackets);
    }

    [Fact]
    public void HandleVerifyAccount2ResponseReturnsDisconnectBytesForRejectedLogin()
    {
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(gateway, AuthOptions());
        _ = bridge.BeginClientLogin(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket());

        var result = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "Bad password."));

        Assert.Equal(ServerListAuthResponseStatus.Rejected, result.Status);
        Assert.Equal(7, result.PlayerId);
        Assert.Equal(
            SocketFrame(OutboundLoginPackets.DisconnectMessage("Bad password.", appendNewline: true), 42),
            result.OutboundBytes);
    }

    [Fact]
    public void HandleVerifyAccount2SuccessLoadsDefaultServerAccountAndSendsWorldEntry()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")));
        _ = bridge.BeginClientLogin(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket());

        var result = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));

        Assert.Equal(ServerListAuthResponseStatus.AcceptedPreWorld, result.Status);
        Assert.True(result.OutboundBytes.Length > 64);
        Assert.Equal(7, result.PlayerId);
        Assert.NotEmpty(gateway.SentPlayerAdds);
        Assert.Equal((byte)ServerToListServerPacketId.PlayerAdd + 32, gateway.SentPlayerAdds[0][0]);
        Assert.True(File.Exists(Path.Combine(serverRoot.Path, "accounts", "pc_Ruan.txt")));
    }

    [Fact]
    public void SecondClientLoginExchangesPlayerPropsWithFirstClient()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")));

        _ = bridge.BeginClientLogin(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        var first = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));
        _ = bridge.BeginClientLogin(new ClientSocketSessionContext(8, "127.0.0.1"), Client3LoginPacket("Z", key: 43));
        var second = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        Assert.Empty(first.Broadcasts);
        var broadcast = Assert.Single(second.Broadcasts);
        Assert.Equal(7, broadcast.PlayerId);
        Assert.NotEmpty(broadcast.OutboundBytes);
        Assert.True(second.OutboundBytes.Length > first.OutboundBytes.Length);
    }

    private static PreWorldAuthOptions AuthOptions() =>
        new(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: true,
            AllowedVersions: ["G3D0311C"],
            AllowedVersionText: "6.037");

    private static byte[] Client3LoginPacket(string account = "Ruan", byte key = 42)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(5);
        packet.WriteGChar(key);
        packet.WriteBytes("G3D0311C"u8);
        packet.WriteGChar((byte)account.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(account));
        packet.WriteGChar(2);
        packet.WriteBytes("pw"u8);
        packet.WriteBytes("win"u8);
        return packet.ToArray();
    }

    private static byte[] VerifyAccount2Payload(
        string account,
        ushort id,
        PlayerSessionType type,
        string message)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)account.Length);
        writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes(account));
        writer.WriteGShort(id);
        writer.WriteGChar((byte)type);
        writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes(message));
        return writer.ToArray();
    }

    private static byte[] SocketFrame(byte[] raw, byte key)
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen5, key);
        queue.AddPacket(raw);
        return queue.FlushSocket(forceSendFiles: true);
    }

    private static TempServerRoot TestDefaultServerRoot()
    {
        var source = FindRepoRoot();
        var destination = Path.Combine(Path.GetTempPath(), "preagonal-gserver-test-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Path.Combine(source, "servers", "default"), destination);
        return new TempServerRoot(destination);
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "GServerSharp.sln")))
                return current;

            current = Directory.GetParent(current)?.FullName ?? "";
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
    }

    private sealed class TempServerRoot(string path) : IDisposable
    {
        public string Path { get; } = path;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class RecordingGateway : IServerListGateway
    {
        public bool IsConnected { get; init; }
        public List<byte[]> SentPackets { get; } = [];
        public List<byte[]> SentPlayerAdds { get; } = [];

        public void SendLoginPacketForPlayer(byte[] packetBody)
        {
            SentPackets.Add(packetBody);
        }

        public void SendPlayerAdd(byte[] packetBody)
        {
            SentPlayerAdds.Add(packetBody);
        }
    }
}
