using Preagonal.GServer.Game;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class PostLoginPacketDispatcherTests
{
    [Fact]
    public void DispatchDecodedPacketAppliesConfirmedPlayerPropsSubset()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);
        var packet = PlayerPropsPacket(
            PlayerPropertyId.X,
            70,
            PlayerPropertyId.Y,
            71);

        var result = dispatcher.DispatchDecodedPacket(packet);

        Assert.Equal(PostLoginPacketDispatchStatus.Handled, result.Status);
        Assert.True(result.ContinueSession);
        Assert.Equal(PlayerToServerPacketId.PlayerProps, result.PacketId);
        Assert.Equal(560, player.PixelX);
        Assert.Equal(568, player.PixelY);
        Assert.Equal(0, dispatcher.InvalidPacketCount);
    }

    [Fact]
    public void DispatchDecodedPacketBlocksAssignedButUnimplementedPacketWithoutInvalidCount()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);
        var packet = Packet(4, 1, 2, 3);

        var result = dispatcher.DispatchDecodedPacket(packet);

        Assert.Equal(PostLoginPacketDispatchStatus.Blocked, result.Status);
        Assert.False(result.ContinueSession);
        Assert.Equal(4, result.RawPacketId);
        Assert.Contains("PLI_BOMBADD", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, dispatcher.InvalidPacketCount);
    }

    [Fact]
    public void DispatchDecodedPacketBlocksClaimPKerAndParsesKillerId()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.ClaimPker);
        packet.WriteGShort(21);

        var result = dispatcher.DispatchDecodedPacket(packet.ToArray());

        Assert.Equal(PostLoginPacketDispatchStatus.Blocked, result.Status);
        Assert.False(result.ContinueSession);
        Assert.Contains("PLI_CLAIMPKER", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, dispatcher.InvalidPacketCount);
    }

    [Fact]
    public void DispatchDecodedPacketBlocksBaddyHurtAndKeepsPayloadOnly()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.BaddyHurt);
        packet.WriteBytes([16, 32, 33]);

        var result = dispatcher.DispatchDecodedPacket(packet.ToArray());

        Assert.Equal(PostLoginPacketDispatchStatus.Blocked, result.Status);
        Assert.False(result.ContinueSession);
        Assert.Contains("PLI_BADDYHURT", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, dispatcher.InvalidPacketCount);
    }

    [Fact]
    public void DispatchDecodedPacketBlocksHurtPlayerUntilCombatRuntimeIsWired()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.HurtPlayer);
        packet.WriteGShort(9);
        packet.WriteGChar(10);
        packet.WriteGChar(20);
        packet.WriteGChar(5);
        packet.WriteGInt(200);

        var result = dispatcher.DispatchDecodedPacket(packet.ToArray());

        Assert.Equal(PostLoginPacketDispatchStatus.Blocked, result.Status);
        Assert.False(result.ContinueSession);
        Assert.Contains("PLI_HURTPLAYER", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, dispatcher.InvalidPacketCount);
    }

    [Fact]
    public void DispatchDecodedPacketBlocksParsedPlayerPropWhoseRuntimeSideEffectsAreNotPorted()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        packet.WriteGChar((byte)PlayerPropertyId.X);
        packet.WriteGChar(70);
        packet.WriteGChar((byte)PlayerPropertyId.CarryNpc);
        packet.WriteGInt(1234);

        var result = dispatcher.DispatchDecodedPacket(packet.ToArray());

        Assert.Equal(PostLoginPacketDispatchStatus.Blocked, result.Status);
        Assert.False(result.ContinueSession);
        Assert.Equal(560, player.PixelX);
        Assert.Contains("PLPROP_CARRYNPC", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, dispatcher.InvalidPacketCount);
    }

    [Fact]
    public void DispatchDecodedPacketAppliesStatus()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        packet.WriteGChar((byte)PlayerPropertyId.X);
        packet.WriteGChar(70);
        packet.WriteGChar((byte)PlayerPropertyId.Status);
        packet.WriteGChar(4);

        var result = dispatcher.DispatchDecodedPacket(packet.ToArray());

        Assert.Equal(PostLoginPacketDispatchStatus.Handled, result.Status);
        Assert.True(result.ContinueSession);
        Assert.Equal(560, player.PixelX);
        Assert.Equal(PlayerStatus.Male, player.Status);
        Assert.Contains("Applied confirmed PLI_PLAYERPROPS subset.", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, dispatcher.InvalidPacketCount);
    }

    [Fact]
    public void DispatchDecodedPacketBlocksParsedNicknameBecauseWordFilterAndSetNickAreNotPorted()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        packet.WriteGChar((byte)PlayerPropertyId.X);
        packet.WriteGChar(70);
        packet.WriteGChar((byte)PlayerPropertyId.Nickname);
        packet.WriteGChar(4);
        packet.WriteBytes("Ruan"u8);

        var result = dispatcher.DispatchDecodedPacket(packet.ToArray());

        Assert.Equal(PostLoginPacketDispatchStatus.Blocked, result.Status);
        Assert.False(result.ContinueSession);
        Assert.Equal(560, player.PixelX);
        Assert.Contains("PLPROP_NICKNAME", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, dispatcher.InvalidPacketCount);
    }

    [Fact]
    public void DispatchDecodedPacketMatchesMsgPliNullDisconnectThresholdForUnassignedPackets()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var dispatcher = new PostLoginPacketDispatcher(player);

        PostLoginPacketDispatchResult result = null!;
        for (var i = 0; i < 6; i++)
            result = dispatcher.DispatchDecodedPacket(Packet(25, 1, 2, 3));

        Assert.Equal(PostLoginPacketDispatchStatus.InvalidPacketLimitExceeded, result.Status);
        Assert.False(result.ContinueSession);
        Assert.Equal(25, result.RawPacketId);
        Assert.Equal(6, dispatcher.InvalidPacketCount);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("Disconnected for sending invalid packets.", appendNewline: true),
            result.OutboundBytes);
    }

    private static byte[] PlayerPropsPacket(PlayerPropertyId first, byte firstValue, PlayerPropertyId second, byte secondValue)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        packet.WriteGChar((byte)first);
        packet.WriteGChar(firstValue);
        packet.WriteGChar((byte)second);
        packet.WriteGChar(secondValue);
        return packet.ToArray();
    }

    private static byte[] Packet(byte id, params byte[] body)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(id);
        packet.WriteBytes(body);
        return packet.ToArray();
    }
}
