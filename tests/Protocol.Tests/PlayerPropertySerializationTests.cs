using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class PlayerPropertySerializationTests
{
    [Fact]
    public void SendLoginPropertySetMatchesCppSendLoginTable()
    {
        Assert.Equal(
            new byte[]
            {
                1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 13, 17, 18, 21, 22, 23,
                25, 26, 32, 34, 35, 36, 37, 38, 39, 40, 41, 46, 47, 48,
                49, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66,
                67, 68, 69, 70, 71, 72, 73, 74, 82
            },
            SendLoginPropertySet.All.Select(id => (byte)id).ToArray());
    }

    [Fact]
    public void SendLoginPropertySetForPreClient21StopsBeforeProperty37()
    {
        Assert.Equal(
            new byte[]
            {
                1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 13, 17, 18, 21, 22, 23,
                25, 26, 32, 34, 35, 36
            },
            SendLoginPropertySet.ForClient(preClient21: true).Select(id => (byte)id).ToArray());
    }

    [Fact]
    public void SendLoginPropertySetForModernClientUsesAllCppLoginProperties()
    {
        Assert.Same(SendLoginPropertySet.All, SendLoginPropertySet.ForClient(preClient21: false));
    }

    [Fact]
    public void GetLoginPropertySetMatchesCppGetLoginTable()
    {
        Assert.Equal(
            new byte[]
            {
                0, 8, 9, 10, 11, 12, 13, 15, 16, 17, 18, 19, 20, 21,
                24, 30, 31, 32, 34, 35, 36, 37, 38, 39, 40, 41, 43,
                44, 45, 46, 47, 48, 49, 50, 53, 54, 55, 56, 57, 58,
                59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71,
                72, 73, 74, 78, 79, 80, 82
            },
            GetLoginPropertySet.All.Select(id => (byte)id).ToArray());
    }

    [Fact]
    public void CompleteSendLoginPropertySetSerializesFromExplicitSourceData()
    {
        var bytes = PlayerPropertySerializer.SerializeConfirmedLoginSubset(BaseSource(), SendLoginPropertySet.All);

        Assert.Equal(new byte[] { 33, 35, 34, 40, 35, 32, 41, 114 }, bytes.Take(8).ToArray());
        Assert.Equal(
            new byte[] { 114, 36, (byte)'R', (byte)'u', (byte)'a', (byte)'n' },
            bytes.Skip(bytes.Length - 6).ToArray());
    }

    [Fact]
    public void ConfirmedLoginSubsetUsesAscendingPropertyOrder()
    {
        var source = BaseSource();
        var bytes = PlayerPropertySerializer.SerializeConfirmedLoginSubset(
            source,
            [
                PlayerPropertyId.AccountName,
                PlayerPropertyId.CurrentPower,
                PlayerPropertyId.MaxPower
            ]);

        Assert.Equal(
            new byte[]
            {
                33, 35,
                34, 40,
                66, 39, (byte)'p', (byte)'c', (byte)':', (byte)'R', (byte)'u', (byte)'a', (byte)'n'
            },
            bytes);
    }

    [Fact]
    public void ConfirmedScalarAndStringPropertiesMatchGetPropEncodings()
    {
        var source = BaseSource();
        var bytes = PlayerPropertySerializer.SerializeConfirmedLoginSubset(
            source,
            [
                PlayerPropertyId.RupeesCount,
                PlayerPropertyId.SwordPower,
                PlayerPropertyId.CurrentLevel,
                PlayerPropertyId.IpAddress
            ]);

        Assert.Equal(
            new byte[]
            {
                35, 32, 41, 114,
                40, 64, 41, (byte)'s', (byte)'w', (byte)'o', (byte)'r', (byte)'d', (byte)'.', (byte)'p', (byte)'n', (byte)'g',
                52, 40, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w',
                62, 32, 32, 32, 32, 33
            },
            bytes);
    }

    [Fact]
    public void PreClient21GaniPropertySerializesBowPowerWhenBowImageIsEmpty()
    {
        var source = BaseSource() with { BowPower = 3, BowImage = "" };
        var bytes = PlayerPropertySerializer.SerializeConfirmedLoginSubset(
            source,
            [PlayerPropertyId.Gani],
            preClient21: true);

        Assert.Equal(new byte[] { 42, 35 }, bytes);
    }

    [Fact]
    public void PreClient21GaniPropertySerializesBowImageWithLengthOffset()
    {
        var source = BaseSource() with { BowPower = 3, BowImage = "bow.gif" };
        var bytes = PlayerPropertySerializer.SerializeConfirmedLoginSubset(
            source,
            [PlayerPropertyId.Gani],
            preClient21: true);

        Assert.Equal(
            new byte[] { 42, 49, (byte)'b', (byte)'o', (byte)'w', (byte)'.', (byte)'g', (byte)'i', (byte)'f' },
            bytes);
    }

    [Fact]
    public void ModernClientGaniPropertyKeepsGaniStringEncoding()
    {
        var source = BaseSource() with { Gani = "walk", BowPower = 3, BowImage = "bow.gif" };
        var bytes = PlayerPropertySerializer.SerializeConfirmedLoginSubset(
            source,
            [PlayerPropertyId.Gani],
            preClient21: false);

        Assert.Equal(new byte[] { 42, 36, (byte)'w', (byte)'a', (byte)'l', (byte)'k' }, bytes);
    }

    [Fact]
    public void PreciseCoordinatePropsUseCppLowBitSignEncoding()
    {
        var source = BaseSource() with { X = -560, Y = 560, Z = -39 };

        var bytes = PlayerPropertySerializer.SerializeConfirmedLoginSubset(
            source,
            [PlayerPropertyId.X2, PlayerPropertyId.Y2, PlayerPropertyId.Z2]);

        Assert.Equal(
            new byte[]
            {
                110, 40, 129,
                111, 40, 128,
                112, 32, 111
            },
            bytes);
    }

    [Fact]
    public void PlayerPropsPacketWrapsConfirmedSubsetWithPloPlayerpropsAndNewline()
    {
        var source = BaseSource();
        var payload = PlayerPropertySerializer.SerializeConfirmedLoginSubset(
            source,
            [PlayerPropertyId.MaxPower, PlayerPropertyId.CurrentPower]);

        Assert.Equal(
            new byte[] { 41, 33, 35, 34, 40, 10 },
            PlayerPropertySerializer.BuildPlayerPropsPacket(payload, appendNewline: true));
    }

    [Fact]
    public void OtherPlayerPropsPacketInjectsJoinLeaveBeforeSortedProps()
    {
        var payload = PlayerPropertySerializer.SerializeOtherPlayerPropsPayload(
            BaseSource(),
            [PlayerPropertyId.X, PlayerPropertyId.JoinLeaveLevel, PlayerPropertyId.Nickname]);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                82, 33,
                32, 36, (byte)'R', (byte)'u', (byte)'a', (byte)'n',
                47, 102,
                10
            },
            PlayerPropertySerializer.BuildOtherPlayerPropsPacket(7, payload, appendNewline: true));
    }

    private static PlayerPropertySource BaseSource() =>
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
            GaniAttributes: new Dictionary<int, string>
            {
                [37] = "attr1",
                [38] = "attr2"
            },
            Os: "win",
            TextCodePage: 1252,
            CommunityName: "Ruan");
}
