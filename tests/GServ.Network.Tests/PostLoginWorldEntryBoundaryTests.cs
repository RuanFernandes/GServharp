using GServ.Network;
using GServ.Protocol;
using Xunit;

namespace GServ.Network.Tests;

public sealed class PostLoginWorldEntryBoundaryTests
{
    [Fact]
    public void PlayerLoggedInBuildsServerListAddPlayerPacketWithConfirmedPropertyOrder()
    {
        var snapshot = BaseSnapshot();

        var packet = PostLoginWorldEntryBoundary.BuildServerListAddPlayerPacket(snapshot);

        Assert.Equal(
                new byte[]
            {
                46, 32, 39, 64,
                66, 39, (byte)'p', (byte)'c', (byte)':', (byte)'R', (byte)'u', (byte)'a', (byte)'n',
                32, 36, (byte)'R', (byte)'u', (byte)'a', (byte)'n',
                52, 40, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w',
                47, 70,
                48, 71,
                64, 72,
                62, 32, 32, 32, 32, 33
            },
            packet);
    }

    [Fact]
    public void ClientBoundaryQueuesConfirmedPacketsBeforeWarpAndStops()
    {
        var session = ReadyForWorldEntrySession();
        var snapshot = BaseSnapshot() with
        {
            LoginPropertyIds = [PlayerPropertyId.MaxPower, PlayerPropertyId.CurrentPower],
            PlayerFlags =
            [
                new LoginFlag("client.flag", "yes"),
                new LoginFlag("empty.flag", "")
            ],
            ServerFlags =
            [
                new LoginFlag("server.flag", "1")
            ]
        };

        var result = PostLoginWorldEntryBoundary.BeginClient(session, snapshot);

        Assert.True(result.Accepted);
        Assert.Equal(SessionLifecycle.ReadyForLevelWarp, session.Lifecycle);
        Assert.Equal(PostLoginClientStopPoint.BeforeWarp, result.StopPoint);
        Assert.Equal(
            new byte[]
            {
                41, 33, 35, 34, 40, 10,
                226, 10,
                60, (byte)'c', (byte)'l', (byte)'i', (byte)'e', (byte)'n', (byte)'t', (byte)'.', (byte)'f', (byte)'l', (byte)'a', (byte)'g', (byte)'=', (byte)'y', (byte)'e', (byte)'s', 10,
                60, (byte)'e', (byte)'m', (byte)'p', (byte)'t', (byte)'y', (byte)'.', (byte)'f', (byte)'l', (byte)'a', (byte)'g', 10,
                60, (byte)'s', (byte)'e', (byte)'r', (byte)'v', (byte)'e', (byte)'r', (byte)'.', (byte)'f', (byte)'l', (byte)'a', (byte)'g', (byte)'=', (byte)'1', 10,
                66, (byte)'B', (byte)'o', (byte)'m', (byte)'b', 10,
                66, (byte)'B', (byte)'o', (byte)'w', 10,
                222, 10
            },
            session.TakeOutboundBytes());
        Assert.Equal(
            PostLoginWorldEntryBoundary.BuildServerListAddPlayerPacket(snapshot),
            result.ServerListAddPlayerPacket);
    }

    [Fact]
    public void OldClientMapWorkaroundSendsLoadedBigMapFilesBeforeClearWeapons()
    {
        var session = ReadyForWorldEntrySession("GNW28015");
        var snapshot = BaseSnapshot();
        var files = new MemoryResourceFileSystem();
        files.Add("worldmap.txt", "mapdata"u8.ToArray(), modTime: 1);
        files.Add("ignored.gmap", "gmapdata"u8.ToArray(), modTime: 1);

        _ = PostLoginWorldEntryBoundary.BeginClient(
            session,
            snapshot,
            new PostLoginClientOptions(
                ResourceFileSystem: files,
                Maps:
                [
                    new LoginMapFile("worldmap.txt", LoginMapType.BigMap),
                    new LoginMapFile("ignored.gmap", LoginMapType.GMap)
                ]));

        Assert.Equal(
            new byte[] { 41, 10 }
                .Concat(FileTransferPackets.BuildFileChunk(
                    "worldmap.txt",
                    "mapdata"u8,
                    modTime: 1,
                    includeModTime: true))
                .Concat(new byte[]
                {
                    226, 10,
                    66, (byte)'B', (byte)'o', (byte)'m', (byte)'b', 10,
                    66, (byte)'B', (byte)'o', (byte)'w', 10,
                    222, 10
                })
                .ToArray(),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void WeaponProtectedWeaponAndClassPacketsKeepConfirmedSendLoginClientOrder()
    {
        var session = ReadyForWorldEntrySession("G3D28095");
        var snapshot = BaseSnapshot();
        var playerWeapon = EntityPackets.NpcWeaponAdd("Tool", "tool.png", "");
        var protectedWeapon = EntityPackets.DefaultWeapon(7);
        var classPacket = new byte[] { 229, (byte)'c', (byte)'l', (byte)'a', (byte)'s', (byte)'s', 10 };

        _ = PostLoginWorldEntryBoundary.BeginClient(
            session,
            snapshot,
            new PostLoginClientOptions(
                ResourceFileSystem: null,
                Maps: [],
                PlayerWeapons: [new LoginWeaponPacket("Tool", playerWeapon)],
                ProtectedWeaponNames: ["Tool", "bow"],
                ProtectedWeaponPackets: new Dictionary<string, byte[]>
                {
                    ["bow"] = protectedWeapon
                },
                OrderedClassPackets: [classPacket]));

        Assert.Equal(
            new byte[] { 41, 10 }
                .Concat(new byte[]
                {
                    226, 10,
                    66, (byte)'B', (byte)'o', (byte)'m', (byte)'b', 10,
                    66, (byte)'B', (byte)'o', (byte)'w', 10
                })
                .Concat(playerWeapon)
                .Concat(protectedWeapon)
                .Concat(classPacket)
                .Concat(new byte[] { 222, 10 })
                .ToArray(),
            session.TakeOutboundBytes());
    }

    private static PostLoginPlayerSnapshot BaseSnapshot()
    {
        var account = new GraalBinaryWriter();
        account.WriteGChar(7);
        account.WriteBytes("pc:Ruan"u8);

        var nickname = new GraalBinaryWriter();
        nickname.WriteGChar(4);
        nickname.WriteBytes("Ruan"u8);

        var level = new GraalBinaryWriter();
        level.WriteGChar(8);
        level.WriteBytes("start.nw"u8);

        return new PostLoginPlayerSnapshot(
            PlayerId: 7,
            Type: PlayerSessionType.Client3,
            AccountNameProperty: account.ToArray(),
            NicknameProperty: nickname.ToArray(),
            CurrentLevelProperty: level.ToArray(),
            XProperty: [70],
            YProperty: [71],
            AlignmentProperty: [72],
            IpAddressProperty: [32, 32, 32, 32, 33],
            LoginPropertySource: BasePropertySource(),
            LoginPropertyIds: [],
            PlayerFlags: [],
            ServerFlags: []);
    }

    private static PlayerPropertySource BasePropertySource() =>
        new(
            Nickname: "Ruan",
            MaxPower: 3,
            Hitpoints: 4.0f,
            Rupees: 1234,
            Arrows: 30,
            Bombs: 8,
            GlovePower: 2,
            SwordPower: 2,
            SwordImage: "sword.png",
            ShieldPower: 1,
            ShieldImage: "shield.png",
            Gani: "idle",
            HeadImage: "head1.png",
            ChatMessage: "hi",
            Colors: [0, 1, 2, 3, 4],
            PlayerId: 7,
            X: 560,
            Y: 568,
            Sprite: 2,
            Status: 1,
            CarrySprite: 0,
            CurrentLevel: "start.nw",
            HorseImage: "horse.png",
            HorseBombCount: 0,
            CarryNpcId: 0,
            ApCounter: 4,
            MagicPoints: 7,
            Kills: 11,
            Deaths: 12,
            OnlineSeconds: 99,
            AccountIp: 1,
            Alignment: 40,
            AdditionalFlags: 0,
            AccountName: "pc:Ruan",
            BodyImage: "body.png",
            EloRating: 1500,
            EloDeviation: 50,
            GaniAttributes: new Dictionary<int, string>(),
            Os: "win",
            TextCodePage: 1252,
            CommunityName: "Ruan");

    private static ClientSessionSkeleton ReadyForWorldEntrySession(string versionToken = "G3D0311C")
    {
        var session = new ClientSessionSkeleton(7);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(5);
        packet.WriteGChar(42);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(versionToken));
        packet.WriteGChar(4);
        packet.WriteBytes("Ruan"u8);
        packet.WriteGChar(2);
        packet.WriteBytes("pw"u8);
        packet.WriteBytes("win"u8);
        Assert.True(session.ReceiveLoginPacket(packet.ToArray()));
        Assert.True(session.ReceiveServerListAuthResponse(
            new ServerListVerifyAccount2Response("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS")));
        Assert.True(PlayerSendLoginContinuation.Begin(
            session,
            new PlayerSendLoginAccount("pc:Ruan", false, "", false, false, true, ["0.0.0.0"], false),
            new PlayerSendLoginOptions(false, "Graal Reborn", [])).Accepted);
        _ = session.TakeOutboundBytes();
        return session;
    }

    private sealed class MemoryResourceFileSystem : IResourceFileSystem
    {
        private readonly Dictionary<string, ResourceFile> _files = new(StringComparer.Ordinal);

        public void Add(string name, byte[] data, long modTime) =>
            _files[name] = new ResourceFile(name, data, modTime);

        public ResourceFile? Find(string file) =>
            _files.GetValueOrDefault(file);
    }
}
