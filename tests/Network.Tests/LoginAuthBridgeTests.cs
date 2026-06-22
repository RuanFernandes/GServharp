using Preagonal.GServer.Game;
using Preagonal.GServer.Persistence;
using Preagonal.GServer.Protocol;
using Preagonal.GServer.Scripting;
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
        Assert.True(File.Exists(Path.Combine(serverRoot.Path, "accounts", "Ruan.txt")));
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
        Assert.Empty(gateway.SentPlayerAdds);
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
        var firstRcPeerBytes = secondLogin.Broadcasts.Where(packet => packet.PlayerId == 7).Select(packet => packet.OutboundBytes).ToArray();
        Assert.True(IndexOf(DecodeLastSocketPayload(42, [firstLogin.OutboundBytes, .. firstRcPeerBytes, result.OutboundBytes]), RcNcPackets.RcChat("YOURACCOUNT: hello")) >= 0);
        var broadcast = Assert.Single(result.Broadcasts);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.True(IndexOf(DecodeLastSocketPayload(43, secondLogin.OutboundBytes, broadcast.OutboundBytes), RcNcPackets.RcChat("YOURACCOUNT: hello")) >= 0);
    }

    [Fact]
    public void RcLogoffFreesAccount()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        _ = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        _ = bridge.EndClientSession(7);
        var secondLogin = LoginRc(bridge, "YOURACCOUNT", 8, 43);

        Assert.Equal(ServerListAuthResponseStatus.AcceptedPreWorld, secondLogin.Status);
        Assert.DoesNotContain(secondLogin.Broadcasts, packet => packet.PlayerId == 7);
    }

    [Fact]
    public void DuplicateRcKicksOldSession()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var firstLogin = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var secondLogin = LoginRc(bridge, "YOURACCOUNT", 8, 43);

        Assert.Equal(ServerListAuthResponseStatus.AcceptedPreWorld, secondLogin.Status);
        var oldSession = Assert.Single(secondLogin.Broadcasts, packet => packet.PlayerId == 7);
        Assert.True(IndexOf(
            DecodeLastSocketPayload(42, firstLogin.OutboundBytes, oldSession.OutboundBytes),
            OutboundLoginPackets.DisconnectMessage("Someone else has logged into your account.", appendNewline: true)) >= 0);
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
    public void RcSavesServerOps()
    {
        using var serverRoot = TestDefaultServerRoot();
        var optionsPath = Path.Combine(serverRoot.Path, "config", "serveroptions.txt");
        var foldersPath = Path.Combine(serverRoot.Path, "config", "foldersconfig.txt");
        var flagsPath = Path.Combine(serverRoot.Path, "serverflags.txt");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        var setOptions = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcServerOptionsSet, GTokenize("name = Changed\nserverport = 14901\n"))));
        var setFolders = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcFolderConfigSet, GTokenize("level world/*.nw\nfile accounts/*.txt\n"))));
        var setFlags = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcFlagsSetPacket("event=on", "motd=hello")));

        Assert.Contains("name = Changed", File.ReadAllText(optionsPath));
        Assert.Contains("level world/*.nw", File.ReadAllText(foldersPath));
        Assert.Equal("event=on\nmotd=hello\n", File.ReadAllText(flagsPath).Replace("\r", "", StringComparison.Ordinal));
        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, setOptions.OutboundBytes, setFolders.OutboundBytes, setFlags.OutboundBytes), RcNcPackets.RcChat("YOURACCOUNT has updated the server flags.")) >= 0);
    }

    [Fact]
    public void RcGetsNpcAddress()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(Path.Combine(serverRoot.Path, "config", "npcserver.txt"), "enabled = true\nid = 44\nhost = 127.0.0.1\nport = 14950\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var decoded = DecodeSocketPayload(login.OutboundBytes, key: 42);

        Assert.True(IndexOf(decoded, RcNcPackets.NpcServerAddress(44, "127.0.0.1", 14950)) >= 0);
    }

    [Fact]
    public void ControlsDoNotPublishAsListserverPlayers()
    {
        using var serverRoot = TestDefaultServerRoot();
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = CreateBridge(serverRoot, new RuntimeServer(), gateway);

        _ = LoginRc(bridge, "YOURACCOUNT", 8, 42);
        _ = LoginNc(bridge, "YOURACCOUNT", 9);

        Assert.DoesNotContain(gateway.SentPlayerAdds, packet => PlayerAddId(packet) == 8);
        Assert.DoesNotContain(gateway.SentPlayerAdds, packet => PlayerAddId(packet) == 9);
    }

    [Fact]
    public void RcSeesOneSelfAndNpcServer()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "config", "serveroptions.txt"),
            "name = GSharp\nserverport = 14899\nserverside = true\nnickname = Testbed\n");
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = CreateBridge(serverRoot, new RuntimeServer(), gateway);

        var login = LoginRc(bridge, "YOURACCOUNT", 8, 42);
        var decoded = DecodeSocketPayload(login.OutboundBytes, 42);

        Assert.Equal(1, CountOf(decoded, RcNcPackets.AddPlayer(8, "YOURACCOUNT", " ", 0, "*YOURACCOUNT", "YOURACCOUNT")));
        Assert.Equal(1, CountOf(decoded, RcNcPackets.RcChat("Welcome to the Graal Reborn GServer Remote Control")));
        Assert.Equal(1, CountOf(decoded, RcNcPackets.RcChat("Say /help for a list of available commands")));
        Assert.Equal(1, CountOf(decoded, RcNcPackets.RcChat("New RC: YOURACCOUNT")));
        var decodedText = System.Text.Encoding.Latin1.GetString(decoded);
        Assert.Contains("(npcserver)", decodedText);
        Assert.Contains("Testbed (Server)", decodedText);
        Assert.Contains(gateway.SentPlayerAdds, packet => PlayerAddId(packet) == 1 && PlayerAddType(packet) == PlayerSessionType.NpcServer);
    }

    [Fact]
    public void RcRelogShowsOnlyNewConnection()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        _ = LoginRc(bridge, "YOURACCOUNT", 2, 42);
        var relog = LoginRc(bridge, "YOURACCOUNT", 3, 43);
        var decoded = DecodeSocketPayload(relog.OutboundBytes, 43);

        Assert.Equal(0, CountOf(decoded, RcNcPackets.AddPlayer(2, "YOURACCOUNT", " ", 0, "*YOURACCOUNT", "YOURACCOUNT")));
        Assert.Equal(1, CountOf(decoded, RcNcPackets.AddPlayer(3, "YOURACCOUNT", " ", 0, "*YOURACCOUNT", "YOURACCOUNT")));
    }

    [Fact]
    public void ClientLoginReplacesSameAccountRc()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        _ = LoginRc(bridge, "moondeath", 2, 42);
        var login = LoginClient(bridge, "moondeath", 3, 43);
        var decoded = DecodeSocketPayload(login.OutboundBytes, 43);

        Assert.Equal(0, CountOf(decoded, RcNcPackets.AddPlayer(2, "moondeath", " ", 0, "*moondeath", "moondeath")));
    }

    [Fact]
    public void RcDoesNotShowNpcControlAsPlayer()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        _ = LoginNc(bridge, "YOURACCOUNT", 2);
        var login = LoginRc(bridge, "YOURACCOUNT", 3, 42);
        var decoded = DecodeSocketPayload(login.OutboundBytes, 42);

        Assert.Equal(0, CountOf(decoded, RcNcPackets.AddPlayer(2, "YOURACCOUNT", " ", 0, "*YOURACCOUNT", "YOURACCOUNT")));
        Assert.Equal(1, CountOf(decoded, RcNcPackets.AddPlayer(3, "YOURACCOUNT", " ", 0, "*YOURACCOUNT", "YOURACCOUNT")));
    }

    [Fact]
    public void RcListRefreshShowsNpcServer()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "config", "serveroptions.txt"),
            "name = GSharp\nserverport = 14899\nserverside = true\nnickname = Testbed\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(RcPacket(PlayerToServerPacketId.RcListRemoteControls), 42));

        var decoded = DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes);
        Assert.True(IndexOf(decoded, RcNcPackets.AddPlayer(1, "(npcserver)", " ", 0, "Testbed (Server)", "(npcserver)")) >= 0);
    }

    [Fact]
    public void RcLoginReceivesNpcServer()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "config", "serveroptions.txt"),
            "name = GSharp\nserverport = 14899\nserverside = true\nnickname = Testbed\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var decoded = DecodeSocketPayload(login.OutboundBytes, 42);

        Assert.True(IndexOf(decoded, RcNcPackets.AddPlayer(1, "(npcserver)", " ", 0, "Testbed (Server)", "(npcserver)")) >= 0);
    }

    [Fact]
    public void RcLoginUsesNpcServerEndpointId()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "config", "serveroptions.txt"),
            "name = GSharp\nserverport = 14899\nserverside = true\nnickname = Testbed\n");
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "config", "npcserver.txt"),
            "enabled = true\nid = 44\nhost = 127.0.0.1\nport = 14899\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var decoded = DecodeSocketPayload(login.OutboundBytes, 42);

        Assert.True(IndexOf(decoded, RcNcPackets.AddPlayer(44, "(npcserver)", " ", 0, "Testbed (Server)", "(npcserver)")) >= 0);
    }

    [Fact]
    public void RcSeesNpcServerWhenServerSideTurnsOn()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "config", "serveroptions.txt"),
            "name = GSharp\nserverport = 14899\nserverside = false\nnickname = Testbed\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcServerOptionsSet, GTokenize("name = GSharp\nserverport = 14899\nserverside = true\nnickname = Testbed\n"))));
        var decoded = DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes);

        Assert.True(IndexOf(decoded, RcNcPackets.AddPlayer(1, "(npcserver)", " ", 0, "Testbed (Server)", "(npcserver)")) >= 0);
    }

    [Fact]
    public void ClientLoginReceivesExistingRc()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginRc(bridge, "YOURACCOUNT", 7, 42);

        var clientLogin = LoginClient(bridge, "Ruan", 8, 43);
        var decoded = DecodeSocketPayload(clientLogin.OutboundBytes, 43);

        Assert.True(IndexOf(decoded, LoginPeerPrefix(7)) >= 0);
    }

    [Fact]
    public void ClientLoginReceivesNpcServer()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "config", "serveroptions.txt"),
            "name = GSharp\nserverport = 14899\nserverside = true\nnickname = Testbed\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var clientLogin = LoginClient(bridge, "Ruan", 8, 43);
        var decoded = DecodeSocketPayload(clientLogin.OutboundBytes, 43);

        Assert.True(IndexOf(decoded, LoginPeerPrefix(1)) >= 0);
    }

    [Fact]
    public void ExistingClientReceivesJoiningRc()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var clientLogin = LoginClient(bridge, "Ruan", 8, 43);

        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var broadcast = Assert.Single(rcLogin.Broadcasts, packet => packet.PlayerId == 8);
        var decoded = DecodeLastSocketPayload(43, clientLogin.OutboundBytes, broadcast.OutboundBytes);

        Assert.True(IndexOf(decoded, LoginPeerPrefix(7)) >= 0);
    }

    [Fact]
    public void RcSeesDeleteWhenClientLeaves()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 2, 42);
        var clientLogin = LoginClient(bridge, "Ruan", 3, 43);
        var clientLoginRcBroadcast = Assert.Single(clientLogin.Broadcasts, packet => packet.PlayerId == 2);

        var end = bridge.EndClientSession(3);

        var rcBroadcast = Assert.Single(end.Broadcasts, packet => packet.PlayerId == 2);
        var decoded = DecodeLastSocketPayload(42, rcLogin.OutboundBytes, clientLoginRcBroadcast.OutboundBytes, rcBroadcast.OutboundBytes);
        Assert.True(IndexOf(decoded, RcNcPackets.DeletePlayer(3)) >= 0);
    }

    [Fact]
    public void ControlDisconnectDoesNotSavePlayerState()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginRc(bridge, "YOURACCOUNT", 2, 42);

        var end = bridge.EndClientSession(2);

        Assert.Null(end.SaveResult);
        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"));
        Assert.DoesNotContain("head25.png", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\nX 0\r\n", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void NcNpcAddCreatesDatabaseNpcRow()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcNpcAddPacket("Control-NPC", 10000, "CONTROL", "moondeath", "onlinestartlocal.nw", "30", "30")));

        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, result.OutboundBytes);
        Assert.True(IndexOf(decoded, RcNcPackets.NcNpcAdd(10000, "Control-NPC", "CONTROL", "onlinestartlocal.nw")) >= 0);
        Assert.Contains("ID 10000", File.ReadAllText(Path.Combine(serverRoot.Path, "npcs", "npcControl-NPC.txt")));
    }

    [Fact]
    public void NcLoginSendsDbNpcs()
    {
        using var serverRoot = TestDefaultServerRoot();
        var npcPath = Path.Combine(serverRoot.Path, "npcs", "npcControl-NPC.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(npcPath)!);
        File.WriteAllText(
            npcPath,
            "GRNPC001\nNAME Control-NPC\nID 10000\nTYPE CONTROL\nSCRIPTER moondeath\nSTARTLEVEL \nSTARTX 30.00\nSTARTY 30.00\nNPCSCRIPT\nNPCSCRIPTEND\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var login = LoginNc(bridge, "YOURACCOUNT", 7);

        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, login.OutboundBytes);
        Assert.True(IndexOf(decoded, RcNcPackets.NcNpcAdd(10000, "Control-NPC", "CONTROL", "")) >= 0);
    }

    [Fact]
    public void NcNpcScriptGetOpensDatabaseNpcScript()
    {
        using var serverRoot = TestDefaultServerRoot();
        var npcPath = Path.Combine(serverRoot.Path, "npcs", "npcControl-NPC.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(npcPath)!);
        File.WriteAllText(
            npcPath,
            "GRNPC001\nNAME Control-NPC\nID 10000\nTYPE CONTROL\nSCRIPTER moondeath\nSTARTLEVEL \nSTARTX 30.00\nSTARTY 30.00\nNPCSCRIPT\nfunction onCreated() {\n  echo(\"hi\");\n}\nNPCSCRIPTEND\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcNpcScriptGetPacket(10000)));
        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, login.OutboundBytes, result.OutboundBytes);

        Assert.True(IndexOf(decoded, RcNcPackets.NcNpcScript(10000, "function onCreated() {\n  echo(\"hi\");\n}")) >= 0);
    }

    [Fact]
    public void NcNpcScriptSetSavesDatabaseNpcScript()
    {
        using var serverRoot = TestDefaultServerRoot();
        var npcPath = Path.Combine(serverRoot.Path, "npcs", "npcControl-NPC.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(npcPath)!);
        File.WriteAllText(
            npcPath,
            "GRNPC001\nNAME Control-NPC\nID 10000\nTYPE CONTROL\nSCRIPTER moondeath\nSTARTLEVEL \nSTARTX 30.00\nSTARTY 30.00\nNPCSCRIPT\nNPCSCRIPTEND\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcNpcScriptSetPacket(10000, "function onCreated() {\n  echo(\"saved\");\n}")));
        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, login.OutboundBytes, result.OutboundBytes);
        var saved = File.ReadAllText(npcPath).Replace("\r", "", StringComparison.Ordinal);

        Assert.Contains("NPCSCRIPT\nfunction onCreated() {\n  echo(\"saved\");\n}\nNPCSCRIPTEND", saved);
        Assert.True(IndexOf(decoded, RcNcPackets.RcChat("NPC script of Control-NPC updated by YOURACCOUNT")) >= 0);
    }

    [Fact]
    public void NcLoginSendsWelcome()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var login = LoginNc(bridge, "YOURACCOUNT", 7);

        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, login.OutboundBytes);
        Assert.True(IndexOf(decoded, RcNcPackets.RcChat("Welcome to the NPC-Server for My Server")) >= 0);
        Assert.Equal(0, CountOf(decoded, RcNcPackets.RcChat("Welcome to the Graal Reborn GServer Remote Control")));
        Assert.Equal(0, CountOf(decoded, RcNcPackets.RcChat("Say /help for a list of available commands")));
    }

    [Fact]
    public void RcFileBrowserStartReturnsFolderListAndDirectory()
    {
        using var serverRoot = TestDefaultServerRoot();
        Directory.CreateDirectory(Path.Combine(serverRoot.Path, "accounts"));
        File.WriteAllText(Path.Combine(serverRoot.Path, "accounts", "sample.txt"), "data");
        File.WriteAllText(Path.Combine(serverRoot.Path, "accounts", ".empty"), "");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(RcPacket(PlayerToServerPacketId.RcFileBrowserStart), 42));
        var decoded = DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes);

        Assert.True(IndexOf(decoded, RcNcPackets.FileBrowserMessage("Welcome to the File Browser.")) >= 0);
        Assert.True(IndexOf(decoded, RcNcPackets.FileBrowserDirList("rw accounts/*\nrw config/*\nrw documents/*\nrw guilds/*\nrw logs/*\nrw npcprops/*\nrw translations/*\nr weapons/*\nrw world/*\nrw world/levels/*\nrw world/bodies/*\nrw world/ganis/*\nrw world/global/*\nrw world/global/heads/*\nrw world/global/bodies/*\nrw world/global/swords/*\nrw world/global/shields/*\nrw world/hats/*\nrw world/heads/*\nrw world/images/*\nrw world/shields/*\nrw world/swords/*\nrw world/sounds/*\n")) >= 0);
        Assert.True(IndexOf(decoded, FileBrowserDirPrefix("accounts/")) >= 0);
        Assert.DoesNotContain(".empty"u8.ToArray(), decoded);
    }

    [Fact]
    public void RcFileBrowserDownloadSendsFile()
    {
        using var serverRoot = TestDefaultServerRoot();
        var filePath = Path.Combine(serverRoot.Path, "accounts", "sample.txt");
        File.WriteAllText(filePath, "data");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(RcPacket(PlayerToServerPacketId.RcFileBrowserDownload, "sample.txt"), 42));
        var decoded = DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes);

        Assert.NotEmpty(result.OutboundBytes);
        Assert.Contains("sample.txt"u8.ToArray(), decoded);
    }

    [Fact]
    public void NcOpensWeaponScripts()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginNc(bridge, "YOURACCOUNT", 7);
        Assert.Equal(ServerListAuthResponseStatus.AcceptedPreWorld, login.Status);
        Assert.NotEmpty(login.OutboundBytes);
        var clientQueue = Gen3Queue();

        var list = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcPacket(PlayerToServerPacketId.NcWeaponListGet)));
        var get = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcPacket(PlayerToServerPacketId.NcWeaponGet, "-gr_movement")));

        var listed = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, list.OutboundBytes);
        var opened = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, get.OutboundBytes);
        Assert.True(
            IndexOf(listed, RcNcPackets.NcWeaponList(["-gr_movement"])) >= 0,
            Convert.ToHexString(listed) + " " + System.Text.Encoding.ASCII.GetString(listed));
        Assert.Contains("-gr_movement"u8.ToArray(), opened);
        Assert.Contains("wbomb1.png"u8.ToArray(), opened);
    }

    [Fact]
    public void NcUpdatesWeaponScripts()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.Copy(
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"),
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT2.txt"));
        var runtime = new RuntimeServer();
        var bridge = CreateBridge(serverRoot, runtime);
        var clientLogin = LoginClient(bridge, "YOURACCOUNT", 8, 43);
        var rcLogin = LoginRc(bridge, "YOURACCOUNT2", 9, 42);
        var rcLoginClientBroadcast = Assert.Single(rcLogin.Broadcasts, packet => packet.PlayerId == 8);
        var ncLogin = LoginNc(bridge, "YOURACCOUNT", 7);
        var ncLoginRcBroadcast = Assert.Single(ncLogin.Broadcasts, packet => packet.PlayerId == 9);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcWeaponAddPacket("-gr_movement", "tool.png", "//#CLIENTSIDE\nplayer.chat = 1;")));

        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "weapons", "weapon-gr_movement.txt"));
        Assert.Contains("REALNAME -gr_movement", saved);
        Assert.Contains("IMAGE tool.png", saved);
        Assert.Contains("//#CLIENTSIDE\nplayer.chat = 1;", saved.Replace("\r", "", StringComparison.Ordinal));
        var clientBroadcast = Assert.Single(result.Broadcasts, packet => packet.PlayerId == 8);
        var rcBroadcast = Assert.Single(result.Broadcasts, packet => packet.PlayerId == 9);
        var clientDecoded = DecodeLastSocketPayload(43, clientLogin.OutboundBytes, rcLoginClientBroadcast.OutboundBytes, clientBroadcast.OutboundBytes);
        var rcDecoded = DecodeLastSocketPayload(42, rcLogin.OutboundBytes, ncLoginRcBroadcast.OutboundBytes, rcBroadcast.OutboundBytes);
        Assert.True(IndexOf(clientDecoded, EntityPackets.NpcWeaponDelete("-gr_movement")) >= 0);
        Assert.True(IndexOf(clientDecoded, EntityPackets.NpcWeaponAdd("-gr_movement", "tool.png", "//#CLIENTSIDE\u00a7player.chat = 1;")) >= 0);
        Assert.True(IndexOf(clientDecoded, [(byte)ServerToPlayerPacketId.RawData + 32]) >= 0);
        Assert.True(IndexOf(clientDecoded, [(byte)ServerToPlayerPacketId.NpcWeaponScript + 32]) >= 0);
        Assert.Equal(1, CountOf(rcDecoded, RcNcPackets.RcChat("Weapon/GUI-script -gr_movement updated by YOURACCOUNT")));
    }

    [Fact]
    public void ClientLoginSendsCustomWeapons()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "weapons", "weapon-gr_movement.txt"),
            "GRAWP001\nREALNAME -gr_movement\nIMAGE wbomb1.png\nSCRIPT\n//#CLIENTSIDE\nplayer.chat = 1;\nSCRIPTEND\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var login = LoginClient(bridge, "YOURACCOUNT", 8, 43);
        var decoded = DecodeSocketPayload(login.OutboundBytes, 43);

        Assert.True(IndexOf(decoded, EntityPackets.NpcWeaponAdd("-gr_movement", "wbomb1.png", "//#CLIENTSIDE\u00a7player.chat = 1;")) >= 0);
    }

    [Fact]
    public void LoginCompilesOwnedWeapon()
    {
        using var serverRoot = TestDefaultServerRoot();
        const string source = "function onActionServerSide() {\n  triggerclient(\"gui\", name, \"kek\");\n}\n//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  player.chat = 1;\n}";
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "weapons", "weapon-gr_movement.txt"),
            "GRAWP001\nREALNAME -gr_movement\nIMAGE wbomb1.png\nSCRIPT\n" + source + "\nSCRIPTEND\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var expectedCompile = new Gs2CompilerAdapter().Compile(
            SourceCodeSlices.Parse(source, gs2Default: true, serverSideVm: true).ClientGs2,
            "weapon",
            "-gr_movement");
        Assert.True(expectedCompile.Success);

        var login = LoginClient(bridge, "YOURACCOUNT", 8, 43);
        var decoded = DecodeSocketPayload(login.OutboundBytes, 43);

        Assert.True(IndexOf(decoded, EntityPackets.NpcWeaponAdd("-gr_movement", "wbomb1.png", "function onActionServerSide() {\u00a7  triggerclient(\"gui\", name, \"kek\");\u00a7}\u00a7//#CLIENTSIDE\u00a7//#GS2\u00a7function onCreated() {\u00a7  player.chat = 1;\u00a7}")) >= 0);
        Assert.True(IndexOf(decoded, [(byte)ServerToPlayerPacketId.RawData + 32]) >= 0);
        Assert.True(IndexOf(decoded, [(byte)ServerToPlayerPacketId.NpcWeaponScript + 32]) >= 0);
        Assert.True(IndexOf(decoded, "weapon,-gr_movement,1,"u8.ToArray()) >= 0);
    }

    [Fact]
    public void BadGs2KeepsSavedWeapon()
    {
        using var serverRoot = TestDefaultServerRoot();
        var weaponPath = Path.Combine(serverRoot.Path, "weapons", "weapon-gr_movement.txt");
        var original = "GRAWP001\nREALNAME -gr_movement\nIMAGE wbomb1.png\nSCRIPT\n//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  player.chat = \"ok\";\n}\nSCRIPTEND\n";
        File.WriteAllText(weaponPath, original);
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcWeaponAddPacket("-gr_movement", "wbomb1.png", "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  player.chat = application::version SPC graalversion \n}")));

        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, result.OutboundBytes);
        Assert.Empty(result.Broadcasts);
        Assert.Equal(original, File.ReadAllText(weaponPath).Replace("\r", "", StringComparison.Ordinal));
        Assert.True(IndexOf(decoded, RcNcPackets.RcChat("Script compiler output for Weapon -gr_movement:")) >= 0);
        Assert.Contains("error:", System.Text.Encoding.Latin1.GetString(decoded));
    }

    [Fact]
    public void NcReportsClientGs2Errors()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcWeaponAddPacket("badclient", "tool.png", "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  player.chat = \"unterminated;\n}")));

        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, result.OutboundBytes);
        Assert.Empty(result.Broadcasts);
        Assert.False(File.Exists(Path.Combine(serverRoot.Path, "weapons", "weapon-badclient.txt")));
        Assert.True(IndexOf(decoded, RcNcPackets.RcChat("Script compiler output for Weapon badclient:")) >= 0);
        Assert.Contains("error: line", System.Text.Encoding.Latin1.GetString(decoded));
    }

    [Fact]
    public void NcRejectsGs1ClientScriptBeforeCompile()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcWeaponAddPacket("badgs1", "tool.png", "function onCreated() echo(1);\n//#CLIENTSIDE\nsetplayerprop #c,hi;")));

        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, result.OutboundBytes);
        Assert.Empty(result.Broadcasts);
        Assert.False(File.Exists(Path.Combine(serverRoot.Path, "weapons", "weapon-badgs1.txt")));
        Assert.True(IndexOf(decoded, RcNcPackets.RcChat("Script compiler output for Weapon badgs1:")) >= 0);
        Assert.Contains("client-side GS1 is not compiled", System.Text.Encoding.Latin1.GetString(decoded));
    }

    [Fact]
    public void NcReportsServerGs2Errors()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcWeaponAddPacket("badserver", "tool.png", "function onCreated() {\n  player.chat = \"unterminated;\n}\n//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n}")));

        var decoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, result.OutboundBytes);
        Assert.Empty(result.Broadcasts);
        Assert.False(File.Exists(Path.Combine(serverRoot.Path, "weapons", "weapon-badserver.txt")));
        Assert.True(IndexOf(decoded, RcNcPackets.RcChat("Script compiler output for Weapon badserver server-side:")) >= 0);
        Assert.Contains("error: line", System.Text.Encoding.Latin1.GetString(decoded));
    }

    [Fact]
    public void NcWeaponEchoes()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 8, 42);
        var ncLogin = LoginNc(bridge, "YOURACCOUNT", 7);
        var ncLoginRcBroadcast = Assert.Single(ncLogin.Broadcasts, packet => packet.PlayerId == 8);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcWeaponAddPacket(
                "-gr_movement",
                "tool.png",
                "echo(1);\n//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n}")));

        var rcBroadcast = Assert.Single(result.Broadcasts, packet => packet.PlayerId == 8);
        var rcDecoded = DecodeLastSocketPayload(42, rcLogin.OutboundBytes, ncLoginRcBroadcast.OutboundBytes, rcBroadcast.OutboundBytes);
        Assert.True(IndexOf(rcDecoded, RcNcPackets.RcChat("1")) >= 0);
        Assert.True(IndexOf(rcDecoded, RcNcPackets.RcChat("GS2 -gr_movement: 1")) < 0);
    }

    [Fact]
    public void NcWeaponEchoCanUseScriptCallPrefix()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.AppendAllText(Path.Combine(serverRoot.Path, "config", "serveroptions.txt"), "\nscriptcall = debug\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 8, 42);
        var ncLogin = LoginNc(bridge, "YOURACCOUNT", 7);
        var ncLoginRcBroadcast = Assert.Single(ncLogin.Broadcasts, packet => packet.PlayerId == 8);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcWeaponAddPacket(
                "-gr_movement",
                "tool.png",
                "echo(1);\n//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n}")));

        var rcBroadcast = Assert.Single(result.Broadcasts, packet => packet.PlayerId == 8);
        var rcDecoded = DecodeLastSocketPayload(42, rcLogin.OutboundBytes, ncLoginRcBroadcast.OutboundBytes, rcBroadcast.OutboundBytes);
        Assert.True(
            IndexOf(rcDecoded, RcNcPackets.RcChat("GS2 -gr_movement: 1")) >= 0,
            System.Text.Encoding.Latin1.GetString(rcDecoded));
    }

    [Fact]
    public void TriggerServerSendsTriggerClient()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var clientLogin = LoginClient(bridge, "YOURACCOUNT", 8, 43);
        _ = LoginNc(bridge, "YOURACCOUNT", 7);
        var ncQueue = Gen3Queue();
        var add = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(ncQueue, NcWeaponAddPacket(
                "-gr_movement",
                "tool.png",
                "function onCreated() {\n  if (serverr.poopybutthole[0] == true) echo(\"bad\");\n}\nfunction onActionServerSide() {\n  triggerclient(\"gui\", name, \"kek\");\n}\n//#CLIENTSIDE\n//#GS2\nfunction onActionClientside() {\n}")));
        var clientBroadcast = Assert.Single(add.Broadcasts, packet => packet.PlayerId == 8);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 43);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(8, "127.0.0.1"),
            SocketPayload(clientQueue, TriggerActionPacket("serverside,-gr_movement,from clientside,1")));
        var decoded = DecodeLastSocketPayload(43, clientLogin.OutboundBytes, clientBroadcast.OutboundBytes, result.OutboundBytes);

        Assert.True(IndexOf(decoded, TriggerActionPackets.BuildClient(0, 0, 0, 0, "clientside,-gr_movement,kek")) >= 0);
    }

    [Fact]
    public void ServerOptionsSetReloadsScriptCall()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 8, 42);
        var ncLogin = LoginNc(bridge, "YOURACCOUNT", 7);
        var ncLoginRcBroadcast = Assert.Single(ncLogin.Broadcasts, packet => packet.PlayerId == 8);
        var setOptions = bridge.HandleClientFrame(
            new ClientSocketSessionContext(8, "127.0.0.1"),
            SocketPayload(RcPacket(PlayerToServerPacketId.RcServerOptionsSet, GTokenize("name = GSharp\nserverport = 14899\nscriptcall = debug\n")), 42));
        Assert.Contains("scriptcall = debug", File.ReadAllText(Path.Combine(serverRoot.Path, "config", "serveroptions.txt")));
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcWeaponAddPacket(
                "-gr_movement",
                "tool.png",
                "echo(1);\n//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n}")));

        var rcBroadcast = Assert.Single(result.Broadcasts, packet => packet.PlayerId == 8);
        var rcDecoded = DecodeLastSocketPayload(42, rcLogin.OutboundBytes, ncLoginRcBroadcast.OutboundBytes, setOptions.OutboundBytes, rcBroadcast.OutboundBytes);
        Assert.True(
            IndexOf(rcDecoded, RcNcPackets.RcChat("GS2 -gr_movement: 1")) >= 0,
            System.Text.Encoding.Latin1.GetString(rcDecoded));
    }

    [Fact]
    public void ControlLoginsAnnounceRcAndNc()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.Copy(
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"),
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT2.txt"),
            overwrite: true);
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var firstRc = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var secondRc = LoginRc(bridge, "YOURACCOUNT2", 8, 43);
        var firstNc = LoginNc(bridge, "YOURACCOUNT", 9);
        var secondNc = LoginNc(bridge, "YOURACCOUNT2", 10);

        Assert.True(IndexOf(DecodeLastSocketPayload(42, firstRc.OutboundBytes), RcNcPackets.RcChat("New RC: YOURACCOUNT")) >= 0);
        Assert.True(IndexOf(DecodeLastSocketPayload(43, secondRc.OutboundBytes), RcNcPackets.RcChat("New RC: YOURACCOUNT2")) >= 0);
        var firstRcPeerBytes = secondRc.Broadcasts.Where(packet => packet.PlayerId == 7).Select(packet => packet.OutboundBytes).ToArray();
        Assert.True(IndexOf(DecodeLastSocketPayload(42, [firstRc.OutboundBytes, .. firstRcPeerBytes]), RcNcPackets.RcChat("New RC: YOURACCOUNT2")) >= 0);
        Assert.True(IndexOf(DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, secondNc.OutboundBytes), RcNcPackets.RcChat("New NC: YOURACCOUNT")) >= 0);
        Assert.True(IndexOf(DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, secondNc.OutboundBytes), RcNcPackets.RcChat("New NC: YOURACCOUNT2")) < 0);
        var firstNcPeerBytes = secondNc.Broadcasts.Where(packet => packet.PlayerId == 9).Select(packet => packet.OutboundBytes).ToArray();
        Assert.True(IndexOf(DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, [firstNc.OutboundBytes, .. firstNcPeerBytes]), RcNcPackets.RcChat("New NC: YOURACCOUNT2")) >= 0);
    }

    [Fact]
    public void NcUpdatesClasses()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, NcClassAddPacket("sample", "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n}")));

        Assert.Contains("function onCreated", File.ReadAllText(Path.Combine(serverRoot.Path, "classes", "sample.txt")));
        Assert.True(IndexOf(DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, result.OutboundBytes), RcNcPackets.NcClassAdd("sample")) >= 0);
    }

    [Fact]
    public void NcHandlesDatabaseNpcPackets()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginNc(bridge, "YOURACCOUNT", 7);
        var clientQueue = Gen3Queue();

        var add = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, NcNpcAddPacket("n0", 10000, "OBJECT", "moondeath", "onlinestartlocal.nw", "1.5", "2.5")));
        var get = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, NcNpcIdPacket(PlayerToServerPacketId.NcNpcGet, 10000)));
        var flagsSet = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, NcNpcFlagsSetPacket(10000, "foo=bar\n")));
        var flagsGet = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, NcNpcIdPacket(PlayerToServerPacketId.NcNpcFlagsGet, 10000)));
        var warp = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, NcNpcWarpPacket(10000, 8, 10, "onlinestartlocal.nw")));
        var delete = bridge.HandleClientFrame(new ClientSocketSessionContext(7, "127.0.0.1"), SocketPayload(clientQueue, NcNpcIdPacket(PlayerToServerPacketId.NcNpcDelete, 10000)));

        var getDecoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, get.OutboundBytes);
        var flagsDecoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, flagsGet.OutboundBytes);
        var deletePayloads = new[] { delete.OutboundBytes }.Concat(delete.Broadcasts.Select(static packet => packet.OutboundBytes)).ToArray();
        var deleteDecoded = DecodeLastSocketPayload(EncryptionGeneration.Gen3, 0, deletePayloads);
        var attrDump = "Variables dump from npc n0\n\nn0.type: OBJECT\nn0.scripter: moondeath\nn0.level: onlinestartlocal.nw\n\nAttributes:\nn0.id: 10000\nn0.name: n0\nn0.type: OBJECT\nn0.scripter: moondeath\nn0.level: onlinestartlocal.nw\nn0.xprecise: 1.5\nn0.yprecise: 2.5\n";
        var attrPacket = new[] { (byte)((byte)ServerToPlayerPacketId.NcNpcAttributes + 32) }.Concat(System.Text.Encoding.ASCII.GetBytes(GTokenize(attrDump))).ToArray();
        Assert.True(IndexOf(getDecoded, attrPacket) >= 0);
        Assert.True(IndexOf(flagsDecoded, RcNcPackets.NcNpcFlags(10000, "foo=bar")) >= 0);
        Assert.True(IndexOf(deleteDecoded, RcNcPackets.NcNpcDelete(10000)) >= 0);
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

        Assert.True(IndexOf(decoded, RcAddPlayerPrefix(8, "Ruan")) >= 0);
    }

    [Fact]
    public void RcPlayerRowsUseDisplayNicknames()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "accounts", "moondeath.txt"),
            "GRACC001\nNAME moondeath\nNICK moondeath\nCOMMUNITYNAME moondeath\nLEVEL onlinestartlocal.nw\nX 30\nY 30.5\nIPRANGE *.*.*.*\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginClient(bridge, "moondeath", 8, 43);

        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var decoded = DecodeSocketPayload(rcLogin.OutboundBytes, 42);

        Assert.True(IndexOf(decoded, RcNcPackets.AddPlayer(8, "moondeath", "onlinestartlocal.nw", 0, "*moondeath", "moondeath")) >= 0);
    }

    [Fact]
    public void RcLoginUsesSavedNickname()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"),
            File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"))
                .Replace("NICK unknown", "NICK Not Denveous", StringComparison.Ordinal));
        var bridge = CreateBridge(serverRoot, new RuntimeServer());

        var rcLogin = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var decoded = DecodeSocketPayload(rcLogin.OutboundBytes, 42);

        Assert.True(IndexOf(decoded, RcNcPackets.AddPlayer(7, "YOURACCOUNT", " ", 0, "Not Denveous", "YOURACCOUNT")) >= 0);
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
        Assert.True(IndexOf(DecodeLastSocketPayload(42, rcLogin.OutboundBytes, broadcast.OutboundBytes), RcAddPlayerPrefix(8, "Ruan")) >= 0);
    }

    [Fact]
    public void RcAccountButtonsReturnDiskAccountPackets()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        var list = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcPacketWithGCharStrings(PlayerToServerPacketId.RcAccountListGet, "YOUR%", "")));
        var account = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcAccountGet, "YOURACCOUNT")));
        var rights = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcPlayerRightsGet, "YOURACCOUNT")));
        var comments = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcPlayerCommentsGet, "YOURACCOUNT")));
        var ban = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcPacket(PlayerToServerPacketId.RcPlayerBanGet, "YOURACCOUNT")));

        Assert.DoesNotContain("pending", list.Diagnostic, StringComparison.OrdinalIgnoreCase);
        var decoded = DecodeLastSocketPayload(
            42,
            login.OutboundBytes,
            list.OutboundBytes,
            account.OutboundBytes,
            rights.OutboundBytes,
            comments.OutboundBytes,
            ban.OutboundBytes);
        Assert.True(IndexOf(decoded, RcNcPackets.PlayerBanGet("YOURACCOUNT", false, "")) >= 0);
        Assert.NotEmpty(list.OutboundBytes);
        Assert.NotEmpty(account.OutboundBytes);
        Assert.NotEmpty(rights.OutboundBytes);
        Assert.NotEmpty(comments.OutboundBytes);
        Assert.NotEmpty(ban.OutboundBytes);
    }

    [Fact]
    public void RcOpenPlayerByAccountReturnsEditorProps()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.Copy(
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"),
            Path.Combine(serverRoot.Path, "accounts", "OTHERACCOUNT.txt"),
            overwrite: true);
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(RcPacketWithGCharStrings(PlayerToServerPacketId.RcPlayerPropsGetByAccount, "OTHERACCOUNT"), 42));
        var decoded = DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes);

        Assert.True(IndexOf(decoded, RcPlayerPropsPrefix(0, "OTHERACCOUNT")) >= 0);
    }

    [Fact]
    public void RcSlashOpenCommandsDispatchToPanels()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        var props = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcChatPacket("/open YOURACCOUNT")));
        var account = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcChatPacket("/openacc YOURACCOUNT")));
        var comments = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcChatPacket("/opencomments YOURACCOUNT")));
        var ban = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcChatPacket("/openban YOURACCOUNT")));
        var rights = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcChatPacket("/openrights YOURACCOUNT")));

        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, props.OutboundBytes), RcPlayerPropsPrefix(7, "YOURACCOUNT")) >= 0);
        AssertPacketId(DecodeLastSocketPayload(42, login.OutboundBytes, props.OutboundBytes, account.OutboundBytes), ServerToPlayerPacketId.RcAccountGet);
        AssertPacketId(DecodeLastSocketPayload(42, login.OutboundBytes, props.OutboundBytes, account.OutboundBytes, comments.OutboundBytes), ServerToPlayerPacketId.RcPlayerCommentsGet);
        AssertPacketId(DecodeLastSocketPayload(42, login.OutboundBytes, props.OutboundBytes, account.OutboundBytes, comments.OutboundBytes, ban.OutboundBytes), ServerToPlayerPacketId.RcPlayerBanGet);
        AssertPacketId(DecodeLastSocketPayload(42, login.OutboundBytes, props.OutboundBytes, account.OutboundBytes, comments.OutboundBytes, ban.OutboundBytes, rights.OutboundBytes), ServerToPlayerPacketId.RcPlayerRightsGet);
    }

    [Fact]
    public void RcSlashOpenCommandsDefaultToSelf()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        var rights = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcChatPacket("/openrights")));

        var decoded = DecodeLastSocketPayload(42, login.OutboundBytes, rights.OutboundBytes);
        AssertPacketId(decoded, ServerToPlayerPacketId.RcPlayerRightsGet);
        Assert.True(IndexOf(decoded, RcNcPackets.RcChat("Server: Unknown command: /openrights")) < 0);
    }

    [Fact]
    public void RcWriteButtonsPersistAccountChanges()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.Copy(
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"),
            Path.Combine(serverRoot.Path, "accounts", "OTHERACCOUNT.txt"),
            overwrite: true);
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        _ = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        _ = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcSetCommentsPacket("OTHERACCOUNT", "note")));
        _ = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcSetBanPacket("OTHERACCOUNT", true, "reason")));

        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "OTHERACCOUNT.txt"));
        Assert.Contains("COMMENTS note", saved);
        Assert.Contains("BANNED 1", saved);
        Assert.Contains("BANREASON reason", saved);
    }

    [Fact]
    public void RcPlayerPropsSetSavesNickname()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientQueue = new GraalFileQueue();
        clientQueue.SetCodec(EncryptionGeneration.Gen5, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(clientQueue, RcSetPlayerPropsPacket("YOURACCOUNT", "NewNick")));

        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"));
        Assert.Contains("NICK NewNick", saved);
        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes), RcNcPackets.RcChat("YOURACCOUNT set the attributes of player YOURACCOUNT")) >= 0);
    }

    [Fact]
    public void RcDisconnectPlayerSendsCppDisconnectMessage()
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
                new AccountLoginOptions(false, "My Server", [], ["YOURACCOUNT"], "")),
            new RuntimeServer());
        _ = LoginRc(bridge, "YOURACCOUNT", 7, 42);
        var clientLogin = LoginClient(bridge, "Ruan", 8, 43);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(RcDisconnectPacket(8, "testing"), 42));

        var clientBroadcast = result.Broadcasts.Single(outbound => outbound.PlayerId == 8);
        Assert.True(IndexOf(
            DecodeLastSocketPayload(43, clientLogin.OutboundBytes, clientBroadcast.OutboundBytes),
            OutboundLoginPackets.DisconnectMessage(
                "One of the server administrators, YOURACCOUNT, has disconnected you for the following reason: testing",
                appendNewline: true)) >= 0);
        Assert.Equal(ServerListAuthPackets.PlayerRemove(8), Assert.Single(gateway.SentPlayerRemoves));
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
    public void ClientUnstickMeWarpsToConfiguredLevel()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(
            Path.Combine(serverRoot.Path, "config", "serveroptions.txt"),
            "name = GSharp\nserverport = 14899\nunstickmelevel = onlinestartlocal.nw\nunstickmex = 30\nunstickmey = 31\nunstickmetime = 0\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginClient(bridge, "YOURACCOUNT", 8, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(8, "127.0.0.1"),
            SocketPayload(PlayerChatPacket("unstick me"), 42));

        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes), ExpectedPlayerWarp(30, 31, "onlinestartlocal.nw")) >= 0);
    }

    [Fact]
    public void ClientWarptoXyUpdatesPosition()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginClient(bridge, "YOURACCOUNT", 8, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(8, "127.0.0.1"),
            SocketPayload(PlayerChatPacket("warpto 10 11"), 42));

        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes), PlayerXYProps(10, 11)) >= 0);
    }

    [Fact]
    public void ClientWarptoXyLevelWarps()
    {
        using var serverRoot = TestDefaultServerRoot();
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginClient(bridge, "YOURACCOUNT", 8, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(8, "127.0.0.1"),
            SocketPayload(PlayerChatPacket("warpto 12 13 onlinestartlocal.nw"), 42));

        Assert.True(IndexOf(DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes), ExpectedPlayerWarp(12, 13, "onlinestartlocal.nw")) >= 0);
    }

    [Fact]
    public void ClientWarptoAccountWarpsToPlayer()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.Copy(
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT.txt"),
            Path.Combine(serverRoot.Path, "accounts", "YOURACCOUNT2.txt"),
            overwrite: true);
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var first = LoginClient(bridge, "YOURACCOUNT", 8, 42);
        var second = LoginClient(bridge, "YOURACCOUNT2", 9, 43);
        var secondBroadcast = Assert.Single(second.Broadcasts, packet => packet.PlayerId == 8);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(8, "127.0.0.1"),
            SocketPayload(PlayerChatPacket("warpto YOURACCOUNT2"), 42));

        Assert.True(IndexOf(DecodeLastSocketPayload(42, first.OutboundBytes, secondBroadcast.OutboundBytes, result.OutboundBytes), ExpectedPlayerWarp(30, 30.5f, "onlinestartlocal.nw")) >= 0);
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
    public void PrivateMessageToNpcServerGetsDefaultReply()
    {
        using var serverRoot = TestDefaultServerRoot();
        File.WriteAllText(Path.Combine(serverRoot.Path, "config", "npcserver.txt"), "enabled = true\nid = 44\nhost = 127.0.0.1\nport = 14950\n");
        var bridge = CreateBridge(serverRoot, new RuntimeServer());
        var login = LoginClient(bridge, "YOURACCOUNT", 7, 42);

        var result = bridge.HandleClientFrame(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            SocketPayload(PrivateMessagePacket([44], "\"hello\""), 42));

        Assert.Empty(result.Broadcasts);
        Assert.Equal(
            ExpectedNpcServerPrivateMessage(44),
            DecodeLastSocketPayload(42, login.OutboundBytes, result.OutboundBytes));
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
        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "Ruan.txt"));
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
        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "Ruan.txt"));
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
        var saved = File.ReadAllText(Path.Combine(serverRoot.Path, "accounts", "Ruan.txt"));
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
        return CreateBridge(serverRoot, runtimeServer, new RecordingGateway { IsConnected = true }, settings);
    }

    private static LoginAuthBridge CreateBridge(
        TempServerRoot serverRoot,
        RuntimeServer runtimeServer,
        IServerListGateway gateway,
        IAccountLoadSettings? settings = null)
    {
        var resources = ServerResourceFileSystems.LoadFolderConfig(
            serverRoot.Path,
            File.ReadAllText(Path.Combine(serverRoot.Path, "config", "foldersconfig.txt")));
        var levelLoader = new NwLevelFileLoader(resources.Get(ServerFileSystemKind.All));
        return new LoginAuthBridge(
            gateway,
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

    private static ServerListLoginResponseResult LoginNc(LoginAuthBridge bridge, string account, ushort id)
    {
        _ = bridge.HandleClientFrame(new ClientSocketSessionContext(id, "127.0.0.1"), NcLoginPacket(account));
        return bridge.HandleVerifyAccount2(VerifyAccount2Payload(account, id, PlayerSessionType.NpcControl, "SUCCESS"));
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

    private static byte[] NcLoginPacket(string account = "Ruan", string versionToken = "NCL21075")
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(3);
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

    private static byte[] DecodeLastSocketPayload(EncryptionGeneration generation, byte key, params byte[][] socketFrames)
    {
        var decoder = new InboundPacketDecoder(generation, key);
        var decoded = Array.Empty<byte>();
        foreach (var socketFrame in socketFrames)
        {
            if (socketFrame.Length == 0)
                continue;

            decoded = decoder.DecodeSocketFrame(socketFrame.AsSpan(2)).DecodedPayload;
        }

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

    private static int CountOf(byte[] bytes, byte[] pattern)
    {
        var count = 0;
        for (var i = 0; i <= bytes.Length - pattern.Length; i++)
        {
            if (!bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                continue;

            count++;
            i += pattern.Length - 1;
        }

        return count;
    }

    private static void AssertPacketId(byte[] decoded, ServerToPlayerPacketId packetId) =>
        Assert.Equal((byte)packetId + 32, decoded[0]);

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

    private static byte[] ExpectedPlayerWarp(float x, float y, string levelName) =>
        AppendNewline(WarpPackets.BuildPlayerWarp(x, y, levelName));

    private static byte[] PlayerXYProps(float x, float y)
    {
        var props = new GraalBinaryWriter();
        props.WriteGChar((byte)PlayerPropertyId.X);
        props.WriteGChar((byte)(x * 2));
        props.WriteGChar((byte)PlayerPropertyId.Y);
        props.WriteGChar((byte)(y * 2));
        return PlayerPropertySerializer.BuildPlayerPropsPacket(props.ToArray(), appendNewline: true);
    }

    private static byte[] AppendNewline(byte[] packet) =>
        [.. packet, (byte)'\n'];

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

    private static byte[] PlayerChatPacket(string message)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        packet.WriteGChar((byte)PlayerPropertyId.CurrentChat);
        packet.WriteGChar((byte)message.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(message));
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

    private static byte[] RcFlagsSetPacket(params string[] flags)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.RcServerFlagsSet);
        packet.WriteGShort((ushort)flags.Length);
        foreach (var flag in flags)
            WriteGCharString(packet, flag);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static string GTokenize(string value)
    {
        var lines = value.EndsWith('\n') ? value.Split('\n') : (value + "\n").Split('\n');
        var tokens = new List<string>();
        foreach (var raw in lines.Take(lines.Length - 1))
        {
            var temp = raw.Replace("\r", string.Empty, StringComparison.Ordinal);
            var complex = temp.StartsWith('"') ||
                          temp.Any(static c => c < 33 || c > 126 || c == ',' || c == '/') ||
                          temp.Trim().Length == 0;
            if (!complex)
            {
                tokens.Add(temp);
                continue;
            }

            temp = temp
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\"\"", StringComparison.Ordinal);
            tokens.Add($"\"{temp}\"");
        }

        return string.Join(",", tokens);
    }

    private static byte[] RcPacketWithGCharStrings(PlayerToServerPacketId id, params string[] values)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)id);
        foreach (var value in values)
        {
            packet.WriteGChar((byte)value.Length);
            packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(value));
        }

        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static void WriteGCharString(GraalBinaryWriter writer, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        writer.WriteGChar((byte)bytes.Length);
        writer.WriteBytes(bytes);
    }

    private static GraalFileQueue Gen3Queue()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen3, 0);
        return queue;
    }

    private static byte[] NcPacket(PlayerToServerPacketId id, string payload = "") =>
        RcPacket(id, payload);

    private static byte[] NcWeaponAddPacket(string name, string image, string source)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.NcWeaponAdd);
        packet.WriteGChar((byte)name.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(name));
        packet.WriteGChar((byte)image.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(image));
        packet.WriteBytes(System.Text.Encoding.Latin1.GetBytes(source.Replace('\n', '\u00a7')));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] TriggerActionPacket(string action)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.TriggerAction);
        packet.WriteGInt(0);
        packet.WriteGChar(0);
        packet.WriteGChar(0);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(action));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] NcClassAddPacket(string name, string source)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.NcClassAdd);
        packet.WriteGChar((byte)name.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(name));
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(source.Replace("\n", ",", StringComparison.Ordinal)));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] NcNpcAddPacket(string name, uint id, string type, string owner, string level, string x, string y)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.NcNpcAdd);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(GTokenize($"{name}\n{id}\n{type}\n{owner}\n{level}\n{x}\n{y}\n")));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] NcNpcScriptGetPacket(uint id)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.NcNpcScriptGet);
        packet.WriteGInt(id);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] NcNpcIdPacket(PlayerToServerPacketId packetId, uint id)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)packetId);
        packet.WriteGInt(id);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] NcNpcFlagsSetPacket(uint id, string flags)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.NcNpcFlagsSet);
        packet.WriteGInt(id);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(GTokenize(flags)));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] NcNpcWarpPacket(uint id, byte x2, byte y2, string level)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.NcNpcWarp);
        packet.WriteGInt(id);
        packet.WriteGChar(x2);
        packet.WriteGChar(y2);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(level));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] NcNpcScriptSetPacket(uint id, string source)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.NcNpcScriptSet);
        packet.WriteGInt(id);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(GTokenize(source)));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] RcSetCommentsPacket(string accountName, string comments)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.RcPlayerCommentsSet);
        packet.WriteGChar((byte)accountName.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(accountName));
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(comments));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] RcSetBanPacket(string accountName, bool banned, string reason)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.RcPlayerBanSet);
        packet.WriteGChar((byte)accountName.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(accountName));
        packet.WriteGChar((byte)(banned ? 1 : 0));
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(reason));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] RcDisconnectPacket(ushort playerId, string reason)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.RcDisconnectPlayer);
        packet.WriteGShort(playerId);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(reason));
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] RcSetPlayerPropsPacket(string accountName, string nickname)
    {
        var props = new GraalBinaryWriter();
        props.WriteGChar((byte)PlayerPropertyId.Nickname);
        props.WriteGChar((byte)nickname.Length);
        props.WriteBytes(System.Text.Encoding.ASCII.GetBytes(nickname));
        var propBytes = props.ToArray();

        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.RcPlayerPropsSetById);
        packet.WriteGChar((byte)accountName.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(accountName));
        packet.WriteGChar(4);
        packet.WriteBytes("main"u8);
        packet.WriteGChar((byte)propBytes.Length);
        packet.WriteBytes(propBytes);
        packet.WriteGShort(0);
        packet.WriteGShort(0);
        packet.WriteGChar(0);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static byte[] RcPlayerPropsPrefix(ushort playerId, string accountName)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.RcPlayerPropertiesGet);
        packet.WriteGShort(playerId);
        packet.WriteGChar((byte)accountName.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(accountName));
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

    private static byte[] ExpectedNpcServerPrivateMessage(ushort npcServerId)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.PrivateMessage);
        packet.WriteGShort(npcServerId);
        packet.WriteBytes("\"\","u8);
        packet.WriteBytes("\"I am the npcserver for\",\"this game server. Almost\",\"all npc actions are controlled\",\"by me.\""u8);
        packet.WriteByte((byte)'\n');
        return packet.ToArray();
    }

    private static ushort PlayerAddId(byte[] packet)
    {
        if (packet.Length < 4 || packet[0] != (byte)ServerToListServerPacketId.PlayerAdd + 32)
            return 0;

        var reader = new GraalBinaryReader(packet.AsSpan(1));
        return reader.ReadGShort();
    }

    private static PlayerSessionType PlayerAddType(byte[] packet)
    {
        if (packet.Length < 4 || packet[0] != (byte)ServerToListServerPacketId.PlayerAdd + 32)
            return 0;

        var reader = new GraalBinaryReader(packet.AsSpan(1));
        _ = reader.ReadGShort();
        return (PlayerSessionType)reader.ReadGChar();
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
