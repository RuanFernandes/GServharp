using System.Text;

namespace Preagonal.GServer.Protocol;

public sealed record TriggerActionRequest(uint NpcId, float X, float Y, string Action, IReadOnlyList<string> Tokens);

public static class TriggerActionPackets
{
    public static TriggerActionRequest ParseIncoming(ReadOnlySpan<byte> packet)
    {
        var reader = new GraalBinaryReader(packet);
        var opcode = (PlayerToServerPacketId)reader.ReadGChar();
        if (opcode != PlayerToServerPacketId.TriggerAction)
            throw new InvalidDataException($"Expected {nameof(PlayerToServerPacketId.TriggerAction)} packet.");

        var npcId = reader.ReadGUInt();
        var x = reader.ReadGChar() / 2.0f;
        var y = reader.ReadGChar() / 2.0f;
        var action = Encoding.ASCII.GetString(reader.ReadBytes(reader.BytesLeft)).Trim();
        var tokens = action.Split(',', StringSplitOptions.TrimEntries).Where(static token => token.Length != 0).ToArray();
        return new TriggerActionRequest(npcId, x, y, action, tokens);
    }

    public static byte[] BuildClient(ushort playerId, uint npcId, byte x, byte y, string action)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.TriggerAction);
        writer.WriteGShort(playerId);
        writer.WriteGInt(npcId);
        writer.WriteGChar(x);
        writer.WriteGChar(y);
        writer.WriteBytes(Encoding.ASCII.GetBytes(action));
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }
}
