using System.Text;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class TriggerActionPacketTests
{
    [Fact]
    public void ParsesClientTriggerAction()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerToServerPacketId.TriggerAction);
        writer.WriteGInt(123);
        writer.WriteGChar(16);
        writer.WriteGChar(24);
        writer.WriteBytes(Encoding.ASCII.GetBytes("serverside,-gr_movement,kek"));

        var action = TriggerActionPackets.ParseIncoming(writer.ToArray());

        Assert.Equal(123u, action.NpcId);
        Assert.Equal(8, action.X);
        Assert.Equal(12, action.Y);
        Assert.Equal(["serverside", "-gr_movement", "kek"], action.Tokens);
    }

    [Fact]
    public void BuildsClientTriggerAction()
    {
        var packet = TriggerActionPackets.BuildClient(0, 0, 0, 0, "clientside,-gr_movement,kek");

        Assert.Equal([
            (byte)ServerToPlayerPacketId.TriggerAction + 32,
            32,
            32,
            32,
            32,
            32,
            32,
            32,
            .. Encoding.ASCII.GetBytes("clientside,-gr_movement,kek\n")
        ], packet);
    }
}
