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
    public void RcLoginUsesControlTailInsteadOfClientWorldWarp()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(Path.Combine(serverRoot.Path, "config", "rcmessage.txt"), "Welcome RC\n");
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
                new AccountLoadSettings(new Dictionary<string, string>
                {
                    ["staffguilds"] = "Server,Manager",
                    ["statuslist"] = "Online,Away"
                }),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")));

        _ = bridge.BeginClientLogin(new ClientSocketSessionContext(7, "127.0.0.1"), Rc2LoginPacket("YOURACCOUNT", key: 42));
        var result = bridge.HandleVerifyAccount2(VerifyAccount2Payload("YOURACCOUNT", 7, PlayerSessionType.RemoteControl2, "SUCCESS"));
        var decoded = DecodeSocketPayload(result.OutboundBytes, key: 42);

        Assert.Equal(ServerListAuthResponseStatus.AcceptedPreWorld, result.Status);
        Assert.True(IndexOf(decoded, RcNcPackets.ClearWeapons()) >= 0);
        Assert.True(IndexOf(decoded, RcNcPackets.RcChat("Welcome RC")) >= 0);
        Assert.True(IndexOf(decoded, RcNcPackets.Unknown190()) >= 0);
        Assert.True(IndexOf(decoded, RcNcPackets.StaffGuilds("Server,Manager")) >= 0);
        Assert.True(IndexOf(decoded, RcNcPackets.StatusList("Online,Away")) >= 0);
        Assert.True(IndexOf(decoded, RcNcPackets.RcMaxUploadFileSize(20 * 1024 * 1024)) >= 0);
        Assert.True(IndexOf(decoded, LevelNamePacketPrefix()) < 0);
        Assert.NotEmpty(gateway.SentPlayerAdds);
    }

    [Fact]
    public void RcChatBroadcastsToRemoteControls()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.Copy(
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"),
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT2.txt"),
            overwrite: true);
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var firstLogin = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var secondLogin = LoginRc(bridge, "YOURACCOUNT2", 8, 43);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(RcChatPacket("hello"), 42));

        Assert.True(result.ContinueSession);
        var secondLoginBroadcast = Assert.Single(secondLogin.Broadcasts);
        Assert.Equal(7, secondLoginBroadcast.PlayerId);
        Assert.True(IndexOf(DecodeLastSocketPayload(42, firstLogin.OutboundBytes, secondLoginBroadcast.OutboundBytes, result.OutboundBytes), RcNcPackets.RcChat("YOURACCOUNT: hello")) >= 0);
        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.True(IndexOf(DecodeLastSocketPayload(43, secondLogin.OutboundBytes, broadcast.OutboundBytes), RcNcPackets.RcChat("YOURACCOUNT: hello")) >= 0);
    }

    [Fact]
    public void RcButtonsReturnServerTextPackets()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(Path.Combine(serverRoot.Path, "config", "serveroptions.txt"), "name = GSharp\nserverport = 14899\n");
        File.WriteAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt"), "level *.nw\nlevel levels/*.graal\n");
        var bridge = CreateBridge(
            serverRoot,
            new RuntimeServer(),
            new AccountLoadSettings(new Dictionary<string, string>
            {
                ["staffguilds"] = "Server",
                ["statuslist"] = "Online"
            }));
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        var options = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcServerOptionsGet)));
        var folders = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcFolderConfigGet)));
        var flags = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcServerFlagsGet)));

        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, options.OutboundBytes), RcNcPackets.ServerOptionsGet("name = GSharp\nserverport = 14899\n")) >= 0);
        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, options.OutboundBytes, folders.OutboundBytes), RcNcPackets.FolderConfigGet("level *.nw\nlevel levels/*.graal\n")) >= 0);
        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, options.OutboundBytes, folders.OutboundBytes, flags.OutboundBytes), RcNcPackets.ServerFlagsGet([])) >= 0);
    }

    [Fact]
    public void RcFileBrowserStartReturnsFolderListAndDirectory()
    {
        using var serverRoot = TestDefaultServerRoot();
        Directory.CreateDirectory(Path.Combine(serverRoot.Path, "accounts"));
        File.WriteAllText(Path.Combine(serverRoot.Path, "accounts", "sample.txt"), "data");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(RcPacket(PlayerToServerPacketId.RcFileBrowserStart), 42));
        var decoded = DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes);

        Assert.True(IndexOf(decoded, RcNcPackets.FileBrowserMessage("Welcome to the File Browser.")) >= 0);
        Assert.True(IndexOf(decoded, FileBrowserDirPrefix("accounts/")) >= 0);
    }

    [Fact]
    public void RcAdminMessageBroadcastsToClientsAndRcs()
    {
        using var serverRoot = TestDefaultServerRoot();
        var runtimeServer = new RuntimeServer();
        var bridge = CreateBridge(serverRoot, runtimeServer);
        _ = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientLogin = LoginClient(bridge, "Ruan", 8, 43);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(RcPacket(PlayerToServerPacketId.RcAdminMessage, "maintenance"), 42));

        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.True(IndexOf(DecodeLastSocketPayload(43, clientLogin.OutboundBytes, broadcast.OutboundBytes), RcNcPackets.RcAdminMessage("Admin YOURACCOUNT:\u00a7maintenance")) >= 0);
    }

    [Fact]
    public void RcLoginReceivesExistingPlayers()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginClient(bridge, "Ruan", 8, 43);

        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var decoded = DecodeSocketPayload(rcLogin.OutboundBytes, 42);

        Assert.True(IndexOf(decoded, RcAddPlayerPrefix(8, "pc:Ruan")) >= 0);
    }

    [Fact]
    public void ExistingRcReceivesJoiningClientAddPlayer()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 7, 42);

        var clientLogin = LoginClient(bridge, "Ruan", 8, 43);

        var broadcast = Assert.Single(clientLogin.Broadcasts);
        Assert.Equal(7, broadcast.PlayerId);
        Assert.True(IndexOf(DecodeLastSocketPayload(42, rcLogin.OutboundBytes, broadcast.OutboundBytes), RcAddPlayerPrefix(8, "pc:Ruan")) >= 0);
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
        Assert.True(IndexOf(DecodeLastSocketPayload(42, first.OutboundBytes, broadcast.OutboundBytes), LoginPeerPrefix(8)) >= 0);
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
        var secondLogin = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(PlayerPropsPacket(PlayerPropertyId.X, 70, PlayerPropertyId.Y, 71), 42));

        Assert.True(result.ContinueSession);
        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.NotEmpty(broadcast.OutboundBytes);
    }

    [Fact]
    public void ShowImgForwardsToLevelPeer()
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
        var secondLogin = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(ShowImgPacket([0x21, 0x22, (byte)'i', (byte)'m', (byte)'g']), 42));

        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(
            ExpectedShowImgPacket(7, [0x21, 0x22, (byte)'i', (byte)'m', (byte)'g']),
            DecodeLastSocketPayload(43, secondLogin.OutboundBytes, broadcast.OutboundBytes));
    }

    [Fact]
    public void PrivateMessageForwardsToTarget()
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
        var secondLogin = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(PrivateMessagePacket([8], "\"hi\""), 42));

        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(
            ExpectedPrivateMessage(7, "\"Private message:\",", "\"hi\""),
            DecodeLastSocketPayload(43, secondLogin.OutboundBytes, broadcast.OutboundBytes));
    }

    [Fact]
    public void MassMessageUsesMassLabel()
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
        var secondLogin = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(PrivateMessagePacket([7, 8], "\"yo\""), 42));

        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(
            ExpectedPrivateMessage(7, "\"Mass message:\",", "\"yo\""),
            DecodeLastSocketPayload(43, secondLogin.OutboundBytes, broadcast.OutboundBytes));
    }

    [Fact]
    public void WeaponAddSavesDefaultWeapon()
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
            SocketPayload(WeaponAddPacket(LevelItemType.Bow), 42));
        var end = bridge.EndClientSession(7);

        Assert.NotNull(end.SaveResult);
        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "pc_Ruan.txt"));
        Assert.Contains("WEAPON bow", saved);
    }

    [Fact]
    public void NickPropKeepsSessionAlive()
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
            SocketPayload(NicknamePacket("Ruan"), 42));

        Assert.True(result.ContinueSession);
        Assert.Contains(result.Broadcasts, broadcast => broadcast.PlayerId == 8);
    }

    [Fact]
    public void PendingFrameKeepsCipherAligned()
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
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42));
        _ = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, PlayerPropsPacket(PlayerPropertyId.X, 69, PlayerPropertyId.Y, 70)));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(8, "127.0.0.1"), Client3LoginPacket("Z", key: 43));
        _ = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, PlayerPropsPacket(PlayerPropertyId.X, 70, PlayerPropertyId.Y, 71)));

        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
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
        var secondLogin = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(BundlePacket(ItemAddPacket(20, 22, (byte)LevelItemType.Bombs)), 42));

        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(
            EntityPackets.ItemAdd(20, 22, (byte)LevelItemType.Bombs),
            DecodeLastSocketPayload(43, secondLogin.OutboundBytes, broadcast.OutboundBytes));
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
        var login = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(BundlePacket(OpenChestPacket(20, 24)), 42));

        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes), OpenedChestPacket(20, 24)) >= 0);
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
        var firstLogin = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(8, "127.0.0.1"), Client3LoginPacket("Z", key: 43));
        var secondLogin = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));
        var secondLoginBroadcast = Assert.Single(secondLogin.Broadcasts);

        var payload = BoardModifyPayload((byte)tileX, (byte)tileY, 1, 1, 0);
        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(BundlePacket(BoardModifyPacket(payload)), 42));

        Assert.True(IndexOf(DecodeLastSocketPayload(42, firstLogin.OutboundBytes, secondLoginBroadcast.OutboundBytes, result.OutboundBytes), BoardChangeRuntime.BuildBoardModifyPacket(payload)) >= 0);
        var broadcast = Assert.Single(result.Broadcasts);
        Assert.True(IndexOf(DecodeLastSocketPayload(43, secondLogin.OutboundBytes, broadcast.OutboundBytes), BoardChangeRuntime.BuildBoardModifyPacket(payload)) >= 0);

        IReadOnlyList<ClientSessionOutbound> respawns = [];
        for (var i = 0; i < 15; i++)
            respawns = bridge.TickLevelTimedEvents();

        Assert.Contains(respawns, packet => packet.PlayerId == 7 && packet.OutboundBytes.Length != 0);
        Assert.Contains(respawns, packet => packet.PlayerId == 8 && packet.OutboundBytes.Length != 0);
    }

    [Fact]
    public void BoardModifyDropsBushItemForOldClient()
    {
        using var serverRoot = TestDefaultServerRoot();
        var optionsPath = Path.Combine(serverRoot.Path, "config", "serveroptions.txt");
        File.WriteAllText(
            optionsPath,
            File.ReadAllText(optionsPath).Replace("tiledroprate = 50", "tiledroprate = 100", StringComparison.Ordinal));

        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        var loaded = levelLoader.TryLoad("onlinestartlocal.nw");
        var (tileX, tileY) = FindDropTile(loaded.Level);
        var runtimeServer = new RuntimeServer();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(
            gateway,
            new PreWorldAuthOptions(128, 0, false, true, ["G3D03014"], "3.0.9"),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                Gs2Settings.LoadFile(optionsPath),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            runtimeServer);

        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), Client3LoginPacket("Ruan", key: 42, versionToken: "G3D03014"));
        var login = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));

        var payload = BoardModifyPayload((byte)tileX, (byte)tileY, 1, 1, 0);
        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(BundlePacket(BoardModifyPacket(payload)), 42));

        var decoded = DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes);
        Assert.True(IndexOf(decoded, [WrappedGChar((byte)ServerToPlayerPacketId.ItemAdd), WrappedGChar((byte)(tileX * 2)), WrappedGChar((byte)(tileY * 2))]) >= 0);
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
        var login = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS"));

        var response = bridge.HandleServerInfo(ServerListAuthPackets.ServerInfoForPlayer(7, "Login,127.0.0.1,14899")[1..]);

        Assert.Equal(7, response.PlayerId);
        Assert.Equal(ExpectedServerWarp("Login,127.0.0.1,14899"), DecodeLastSocketPayload(42, login.OutboundBytes, response.OutboundBytes));
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
        Assert.Contains("\r\nSTATUS 20\r\n", saved, StringComparison.Ordinal);
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
        var secondLogin = bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:Z", 8, PlayerSessionType.Client3, "SUCCESS"));

        var end = bridge.EndClientSession(7);

        var broadcast = Assert.Single(end.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(ExpectedDisconnectPacket(7), DecodeLastSocketPayload(43, secondLogin.OutboundBytes, broadcast.OutboundBytes));
    }

    private static PreWorldAuthOptions AuthOptions() =>
        new(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: true,
            AllowedVersions: ["G3D0311C"],
            AllowedVersionText: "6.037");

    private static LoginAuthBridge CreateBridge(
        TempServerRoot serverRoot,
        RuntimeServer runtimeServer,
        IAccountLoadSettings? settings = null)
    {
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        return new LoginAuthBridge(
            new RecordingGateway { IsConnected = true },
            AuthOptions(),
            new LoginWorldEntryOptions(
                new DiskAccountFileSystem(serverRoot.Path),
                settings ?? Gs2Settings.LoadFile(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")),
                levelLoader,
                new FileLevelLookup(levelLoader),
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT", "YOURACCOUNT2"], "")),
            runtimeServer);
    }

    private static ServerListLoginResponseResult LoginRc(LoginAuthBridge bridge, string account, ushort id, byte key)
    {
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(id, "127.0.0.1"), Rc2LoginPacket(account, key));
        return bridge.HandleVerifyAccount2(VerifyAccount2Payload(account, id, PlayerSessionType.RemoteControl2, "SUCCESS"));
    }

    private static ServerListLoginResponseResult LoginClient(LoginAuthBridge bridge, string account, ushort id, byte key)
    {
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(id, "127.0.0.1"), Client3LoginPacket(account, key));
        return bridge.HandleVerifyAccount2(VerifyAccount2Payload("pc:" + account, id, PlayerSessionType.Client3, "SUCCESS"));
    }

    private static byte[] Client3LoginPacket(string account = "Ruan", byte key = 42, string versionToken = "G3D0311C")
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(5);
        packet.WriteGChar(key);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(versionToken));
        packet.WriteGChar((byte)account.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(account));
        packet.WriteGChar(2);
        packet.WriteBytes("pw"u8);
        packet.WriteBytes("win"u8);
        return packet.ToArray();
    }

    private static byte[] Rc2LoginPacket(string account = "Ruan", byte key = 42, string versionToken = "GSERV025")
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(6);
        packet.WriteGChar(key);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(versionToken));
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
        queue.AddRawPacket(raw);
        return queue.FlushSocket(forceSendFiles: true);
    }

    private static byte[] SocketPayload(byte[] raw, byte key) =>
        SocketFrame(raw, key)[2..];

    private static byte[] SocketPayload(GraalFileQueue queue, byte[] raw)
    {
        queue.AddRawPacket(raw);
        return queue.FlushSocket(forceSendFiles: true)[2..];
    }

    private static byte[] DecodeSocketPayload(byte[] socketFrame, byte key) =>
        new InboundPacketDecoder(EncryptionGeneration.Gen5, key)
            .DecodeSocketFrame(socketFrame.AsSpan(2))
            .DecodedPayload;

    private static byte[] DecodeLastSocketPayload(byte key, params byte[][] socketFrames)
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen5, key);
        var decoded = Array.Empty<byte>();
        foreach (var socketFrame in socketFrames)
            decoded = decoder.DecodeSocketFrame(socketFrame.AsSpan(2)).DecodedPayload;

        return decoded;
    }

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

    private static byte[] LevelNamePacketPrefix()
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.LevelName);
        return packet.ToArray();
    }

    private static byte[] RcAddPlayerPrefix(ushort playerId, string accountName)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.AddPlayer);
        packet.WriteGShort(playerId);
        packet.WriteGChar((byte)accountName.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(accountName));
        return packet.ToArray();
    }

    private static byte[] FileBrowserDirPrefix(string folder)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.RcFileBrowserDir);
        packet.WriteGChar((byte)folder.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(folder));
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

    private static byte[] ShowImgPacket(byte[] body)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(24);
        packet.WriteBytes(body);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] ExpectedShowImgPacket(ushort playerId, byte[] body)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(32);
        packet.WriteGShort(playerId);
        packet.WriteBytes(body);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] PrivateMessagePacket(IReadOnlyList<ushort> targets, string message)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PrivateMessage);
        packet.WriteGShort((ushort)targets.Count);
        foreach (var target in targets)
            packet.WriteGShort(target);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(message));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] RcChatPacket(string message) =>
        RcPacket(PlayerToServerPacketId.RcChat, message);

    private static byte[] RcPacket(PlayerToServerPacketId id, string payload = "")
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)id);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(payload));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] ExpectedPrivateMessage(ushort senderId, string label, string message)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.PrivateMessage);
        packet.WriteGShort(senderId);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes("\"\","));
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(label));
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(message));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] WeaponAddPacket(LevelItemType itemType)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.WeaponAdd);
        packet.WriteGChar(0);
        packet.WriteGChar((byte)itemType);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] NicknamePacket(string nickname)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        packet.WriteGChar((byte)PlayerPropertyId.Nickname);
        packet.WriteGChar((byte)nickname.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(nickname));
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

    private static (int X, int Y) FindDropTile(NwLevelSnapshot level)
    {
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                if (level.GetTile(0, x, y) is 2 or 0x1a4 or 0x1ff or 0x3ff)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("Default level must contain a C++ bush drop tile.");
    }

    private static byte[] ServerWarpPacket(string serverName)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.ServerWarp);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(serverName));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] BundlePacket(params byte[][] packets)
    {
        var bundle = new GraalBinaryWriter();
        bundle.WriteByte(WrappedGChar((byte)PlayerToServerPacketId.Bundle));
        foreach (var rawPacket in packets)
        {
            var payload = rawPacket[^1] == (byte)'\n'
                ? rawPacket[..^1]
                : rawPacket;
            bundle.WriteRawShort((ushort)payload.Length);
            bundle.WriteBytes(payload);
        }

        bundle.WriteByte((byte)'\n');
        return bundle.ToArray();
    }

    private static byte WrappedGChar(byte value) => unchecked((byte)(value + 32));

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
