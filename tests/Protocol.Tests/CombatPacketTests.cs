using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class CombatPacketTests
{
    [Fact]
    public void ConfirmedCombatPacketIdsMatchGs2libIEnums()
    {
        Assert.Equal(11, (int)ServerToPlayerPacketId.BombAdd);
        Assert.Equal(12, (int)ServerToPlayerPacketId.BombDelete);
        Assert.Equal(19, (int)ServerToPlayerPacketId.ArrowAdd);
        Assert.Equal(36, (int)ServerToPlayerPacketId.Explosion);
        Assert.Equal(40, (int)ServerToPlayerPacketId.HurtPlayer);
        Assert.Equal(46, (int)ServerToPlayerPacketId.HitObjects);
        Assert.Equal(175, (int)ServerToPlayerPacketId.Shoot);
        Assert.Equal(191, (int)ServerToPlayerPacketId.Shoot2);
    }

    [Fact]
    public void BombAndArrowForwardingPacketsInjectSenderIdAndPreserveClientPayloadAfterOpcode()
    {
        Assert.Equal(
            [43, 32, 39, 21, 22, 3, 55, 10],
            CombatPackets.BombAdd(7, [4, 21, 22, 3, 55], appendNewline: true));

        Assert.Equal(
            [44, 21, 22, 3, 55, 10],
            CombatPackets.BombDelete([4, 21, 22, 3, 55], appendNewline: true));

        Assert.Equal(
            [51, 32, 39, 21, 22, 3, 55, 10],
            CombatPackets.ArrowAdd(7, [9, 21, 22, 3, 55], appendNewline: true));
    }

    [Fact]
    public void HurtExplosionAndHitObjectsPacketsMatchConfirmedCppFieldOrder()
    {
        Assert.Equal(
            [72, 32, 39, 50, 62, 37, 32, 32, 47, 10],
            CombatPackets.HurtPlayer(attackerId: 7, hurtDx: 18, hurtDy: 30, power: 5, npcId: 15, appendNewline: true));

        Assert.Equal(
            [68, 32, 39, 35, 54, 64, 38, 10],
            CombatPackets.Explosion(playerId: 7, radius: 3, encodedX: 22, encodedY: 32, power: 6, appendNewline: true));

        Assert.Equal(
            [78, 32, 39, 36, 54, 64, 10],
            CombatPackets.HitObjectsFromPlayer(playerId: 7, encodedPower: 4, encodedX: 22, encodedY: 32, appendNewline: true));

        Assert.Equal(
            [78, 32, 32, 36, 54, 64, 32, 33, 104, 10],
            CombatPackets.HitObjectsFromNpc(npcId: 200, encodedPower: 4, encodedX: 22, encodedY: 32, appendNewline: true));
    }

    [Fact]
    public void ParsesInboundHurtPlayerAndBuildsBaddyHurtLeaderForwardLikeCpp()
    {
        var hurt = new GraalBinaryWriter();
        hurt.WriteGChar((byte)PlayerToServerPacketId.HurtPlayer);
        hurt.WriteGShort(8);
        hurt.WriteGChar(18);
        hurt.WriteGChar(30);
        hurt.WriteGChar(5);
        hurt.WriteGInt(200);

        var parsed = CombatPackets.ParseHurtPlayer(hurt.ToArray());

        Assert.True(parsed.Success);
        Assert.Equal(8, parsed.Packet!.VictimPlayerId);
        Assert.Equal(18, parsed.Packet.HurtDx);
        Assert.Equal(30, parsed.Packet.HurtDy);
        Assert.Equal(5, parsed.Packet.Power);
        Assert.Equal(200u, parsed.Packet.NpcId);

        Assert.Equal(
            [59, 48, 49, 50, 10],
            CombatPackets.BaddyHurtToLeader([16, 48, 49, 50], appendNewline: true));
    }

    [Fact]
    public void ParsesInboundClaimPkerAndAppliesSourceConfirmedUnsignedShortDecoding()
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.ClaimPker);
        packet.WriteGShort(25);

        var parsed = CombatPackets.ParseClaimPker(packet.ToArray());

        Assert.True(parsed.Success);
        Assert.Equal(25u, parsed.Packet!.KillerPlayerId);
    }

    [Fact]
    public void ParsesClaimPkerWithMissingGShortBytesLikeGs2libEOFZeroFill()
    {
        var parsed = CombatPackets.ParseClaimPker([(byte)((byte)PlayerToServerPacketId.ClaimPker + 32)]);

        Assert.True(parsed.Success);
        Assert.Equal((ushort)61408, parsed.Packet!.KillerPlayerId);
    }

    [Fact]
    public void ParsesInboundBaddyHurtKeepsExactIncomingPayloadForLeaderForward()
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.BaddyHurt);
        packet.WriteBytes([16, 32, 33, 100]);

        var parsed = CombatPackets.ParseBaddyHurt(packet.ToArray());

        Assert.True(parsed.Success);
        Assert.Equal([16, 32, 33, 100], parsed.Packet!.Payload);
    }
}
