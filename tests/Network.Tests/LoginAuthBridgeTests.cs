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
    public void LoginSendsAccountDefaultWeaponsLikeCpp()
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
        var decoded = DecodeSocketPayload(result.OutboundBytes, key: 42);

        Assert.True(IndexOf(decoded, EntityPackets.DefaultWeapon((byte)LevelItemType.Bomb)) >= 0);
        Assert.True(IndexOf(decoded, EntityPackets.DefaultWeapon((byte)LevelItemType.Bow)) >= 0);
        Assert.True(
            IndexOf(decoded, EntityPackets.DefaultWeapon((byte)LevelItemType.Bomb)) <
            IndexOf(decoded, EntityPackets.DefaultWeapon((byte)LevelItemType.Bow)));
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
        Assert.True(IndexOf(DecodeSocketPayload(second.OutboundBytes, key: 43), LoginPeerPrefix(7)) >= 0);
        Assert.True(IndexOf(DecodeSocketPayload(broadcast.OutboundBytes, key: 42), LoginPeerPrefix(8)) >= 0);
    }

    [Fact]
    public void ActiveClientPlayerPropsBroadcastToLevelPeer()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(8, "127.0.0.1"), Client3LoginPacket("Z", key: 43));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(PlayerPropsPacket(PlayerPropertyId.X, 70, PlayerPropertyId.Y, 71), 42));

        Assert.True(result.ContinueSession);
        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.NotEmpty(broadcast.OutboundBytes);
    }

    [Fact]
    public void ItemAddForwardsToLevelPeer()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(8, "127.0.0.1"), Client3LoginPacket("Z", key: 43));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(ItemAddPacket(20, 22, (byte)LevelItemType.Bombs), 42));

        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(
            EntityPackets.ItemAdd(20, 22, (byte)LevelItemType.Bombs),
            DecodeSocketPayload(broadcast.OutboundBytes, key: 43));
    }

    [Fact]
    public void OpenChestRewardsAndPersists()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(OpenChestPacket(20, 24), 42));

        Assert.True(IndexOf(DecodeSocketPayload(result.OutboundBytes, key: 42), OpenedChestPacket(20, 24)) >= 0);
        _ = bridge.EndClientSession(7);
        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "pc_Ruan.txt"));
        Assert.Contains("\r\nCHEST 20:24:onlinestartlocal.nw\r\n", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void BoardModifyForwardsAndRespawns()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var loaded = levelLoader.TryLoad("onlinestartlocal.nw");
        var (tileX, tileY) = FindRespawningTile(loaded.Level);
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(8, "127.0.0.1"), Client3LoginPacket("Z", key: 43));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var payload = BoardModifyPayload((byte)tileX, (byte)tileY, 1, 1, 0);
        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(BoardModifyPacket(payload), 42));

        Assert.Equal(BoardChangeRuntime.BuildBoardModifyPacket(payload), DecodeSocketPayload(result.OutboundBytes, key: 42));
        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(BoardChangeRuntime.BuildBoardModifyPacket(payload), DecodeSocketPayload(broadcast.OutboundBytes, key: 43));

        IReadOnlyList<ClientSessionOutbound> respawns = [];
        for (var i = 0; i < 15; i++)
            respawns = bridge.TickLevelTimedEvents();

        Assert.Contains(respawns, packet => packet.PlayerId == 7 && packet.OutboundBytes.Length != 0);
        Assert.Contains(respawns, packet => packet.PlayerId == 8 && packet.OutboundBytes.Length != 0);
    }

    [Fact]
    public void ServerWarpRequestsListserver()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(ServerWarpPacket("Login"), 42));

        Assert.True(result.ContinueSession);
        Assert.Empty(result.OutboundBytes);
        Assert.Equal(
            ServerListAuthPackets.ServerInfoForPlayer(7, "Login"),
            Assert.Single(gateway.SentServerInfos));
    }

    [Fact]
    public void ServerInfoWarpsClient()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));

        var response = bridge.HandleServerInfo(ServerListAuthPackets.ServerInfoForPlayer(7, "Login,127.0.0.1,14899")[1..]);

        Assert.Equal(7, response.PlayerId);
        Assert.Equal(ExpectedServerWarp("Login,127.0.0.1,14899"), DecodeSocketPayload(response.OutboundBytes, key: 42));
    }

    [Fact]
    public void EndSessionSavesAccountAndRemovesListserverPlayer()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));
        _ = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(PlayerPropsPacket(PlayerPropertyId.X, 70, PlayerPropertyId.Y, 71), 42));

        var end = bridge.EndClientSession(7);

        Assert.NotNull(end.SaveResult);
        Assert.True(end.SaveResult.WriteSucceeded);
        Assert.Equal(ServerListAuthPackets.PlayerRemove(7), Assert.Single(gateway.SentPlayerRemoves));
        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "pc_Ruan.txt"));
        Assert.Contains("\r\nX 35\r\n", saved, StringComparison.Ordinal);
        Assert.Contains("\r\nY 35.5\r\n", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void EndSessionBroadcastsDisconnectToClientPeer()
    {
        using var serverRoot = TestDefaultServerRoot();
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(8, "127.0.0.1"), Client3LoginPacket("Z", key: 43));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var end = bridge.EndClientSession(7);

        var broadcast = Assert.Single(end.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(ExpectedDisconnectPacket(7), DecodeSocketPayload(broadcast.OutboundBytes, key: 43));
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

    private static byte[] SocketPayload(byte[] raw, byte key) =>
        SocketFrame(raw, key)[2..];

    private static byte[] DecodeSocketPayload(byte[] socketFrame, byte key) =>
        new InboundPacketDecoder(EncryptionGeneration.Gen5, key)
            .DecodeSocketFrame(socketFrame.AsSpan(2))
            .DecodedPayload;

    private static int IndexOf(byte[] bytes, byte[] pattern)
    {
        for (var i = 0; i <= bytes.Length - pattern.Length; i++)
        {
            if (bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                return i;
        }

        return -1;
    }

    private static byte[] ExpectedDisconnectPacket(ushort playerId)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.OtherPlayerProps);
        packet.WriteGShort(playerId);
        packet.WriteGChar((byte)PlayerPropertyId.PlayerConnected);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] ExpectedServerWarp(string serverPacket)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.ServerWarp);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(serverPacket));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] LoginPeerPrefix(ushort playerId)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.OtherPlayerProps);
        packet.WriteGShort(playerId);
        packet.WriteGChar((byte)PlayerPropertyId.JoinLeaveLevel);
        packet.WriteGChar(1);
        return packet.ToArray();
    }

    private static byte[] PlayerPropsPacket(PlayerPropertyId first, byte firstValue, PlayerPropertyId second, byte secondValue)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        packet.WriteGChar((byte)first);
        packet.WriteGChar(firstValue);
        packet.WriteGChar((byte)second);
        packet.WriteGChar(secondValue);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] ItemAddPacket(byte encodedX, byte encodedY, byte itemType)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.ItemAdd);
        packet.WriteGChar(encodedX);
        packet.WriteGChar(encodedY);
        packet.WriteGChar(itemType);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] OpenChestPacket(byte x, byte y)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.OpenChest);
        packet.WriteGChar(x);
        packet.WriteGChar(y);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] OpenedChestPacket(byte x, byte y)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.LevelChest);
        packet.WriteGChar(1);
        packet.WriteGChar(x);
        packet.WriteGChar(y);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] BoardModifyPacket(byte[] payload)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.BoardModify);
        packet.WriteBytes(payload);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] BoardModifyPayload(byte x, byte y, byte width, byte height, ushort tile)
    {
        var tiles = new GraalBinaryWriter();
        tiles.WriteGShort(tile);
        return BoardChangeRuntime.BuildPayload(x, y, width, height, tiles.ToArray());
    }

    private static (int X, int Y) FindRespawningTile(NwLevelSnapshot level)
    {
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                if (BoardChangeRuntime.ShouldRespawn(level.GetTile(0, x, y)))
                    return (x, y);
            }
        }

        throw new InvalidOperationException("Default level must contain a C++ respawning tile.");
    }

    private static byte[] ServerWarpPacket(string serverName)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.ServerWarp);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(serverName));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
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
        public List<byte[]> SentPlayerRemoves { get; } = [];
        public List<byte[]> SentServerInfos { get; } = [];

        public void SendLoginPacketForPlayer(byte[] packetBody)
        {
            SentPackets.Add(packetBody);
        }

        public void SendPlayerAdd(byte[] packetBody)
        {
            SentPlayerAdds.Add(packetBody);
        }

        public void SendPlayerRemove(byte[] packetBody)
        {
            SentPlayerRemoves.Add(packetBody);
        }

        public void SendServerInfoForPlayer(byte[] packetBody)
        {
            SentServerInfos.Add(packetBody);
        }
    }
}
