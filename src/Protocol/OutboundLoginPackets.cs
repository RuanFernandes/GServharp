using System.Text;

namespace Preagonal.GServer.Protocol;

public static class OutboundLoginPackets
{
    public static byte[] Signature(bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.Signature);
        writer.WriteGChar(73);
        if (appendNewline)
            writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    public static byte[] Unknown168(bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.Unknown168);
        if (appendNewline)
            writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    public static byte[] DisconnectMessage(string message, bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.DisconnectMessage);
        writer.WriteBytes(Encoding.ASCII.GetBytes(message));
        if (appendNewline)
            writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }
}
