namespace Preagonal.GServer.Protocol;

public sealed record InboundHurtPlayerPacket(
    ushort VictimPlayerId,
    byte HurtDx,
    byte HurtDy,
    byte Power,
    uint NpcId);

public sealed record InboundHurtPlayerParseResult(bool Success, InboundHurtPlayerPacket? Packet);

public sealed record InboundBaddyHurtPacket(byte[] Payload);

public sealed record InboundBaddyHurtParseResult(bool Success, InboundBaddyHurtPacket? Packet);

public sealed record InboundClaimPkerPacket(ushort KillerPlayerId);

public sealed record InboundClaimPkerParseResult(bool Success, InboundClaimPkerPacket? Packet);

public static class CombatPackets
{
    public static InboundBaddyHurtParseResult ParseBaddyHurt(ReadOnlySpan<byte> clientPacket)
    {
        if (clientPacket.IsEmpty)
            return new InboundBaddyHurtParseResult(false, null);

        var reader = new GraalBinaryReader(clientPacket);
        var opcode = (PlayerToServerPacketId)reader.ReadGChar();
        if (opcode != PlayerToServerPacketId.BaddyHurt)
            return new InboundBaddyHurtParseResult(false, null);

        var payload = clientPacket[1..].ToArray();
        return new InboundBaddyHurtParseResult(true, new InboundBaddyHurtPacket(payload));
    }

    public static InboundClaimPkerParseResult ParseClaimPker(ReadOnlySpan<byte> clientPacket)
    {
        if (clientPacket.IsEmpty)
            return new InboundClaimPkerParseResult(false, null);

        var reader = new GraalBinaryReader(clientPacket);
        var opcode = (PlayerToServerPacketId)reader.ReadGChar();
        if (opcode != PlayerToServerPacketId.ClaimPker)
            return new InboundClaimPkerParseResult(false, null);

        var killer = (ushort)reader.ReadGShort();
        return new InboundClaimPkerParseResult(true, new InboundClaimPkerPacket(killer));
    }

    public static byte[] BombAdd(ushort playerId, ReadOnlySpan<byte> clientPacket, bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.BombAdd);
        writer.WriteGShort(playerId);
        WriteAfterClientOpcode(writer, clientPacket);
        AppendNewline(writer, appendNewline);
        return writer.ToArray();
    }

    public static byte[] BombDelete(ReadOnlySpan<byte> clientPacket, bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.BombDelete);
        WriteAfterClientOpcode(writer, clientPacket);
        AppendNewline(writer, appendNewline);
        return writer.ToArray();
    }

    public static byte[] ArrowAdd(ushort playerId, ReadOnlySpan<byte> clientPacket, bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.ArrowAdd);
        writer.WriteGShort(playerId);
        WriteAfterClientOpcode(writer, clientPacket);
        AppendNewline(writer, appendNewline);
        return writer.ToArray();
    }

    public static byte[] HurtPlayer(
        ushort attackerId,
        byte hurtDx,
        byte hurtDy,
        byte power,
        uint npcId,
        bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.HurtPlayer);
        writer.WriteGShort(attackerId);
        writer.WriteGChar(hurtDx);
        writer.WriteGChar(hurtDy);
        writer.WriteGChar(power);
        writer.WriteGInt(npcId);
        AppendNewline(writer, appendNewline);
        return writer.ToArray();
    }

    public static InboundHurtPlayerParseResult ParseHurtPlayer(ReadOnlySpan<byte> clientPacket)
    {
        if (clientPacket.IsEmpty)
            return new InboundHurtPlayerParseResult(false, null);

        var reader = new GraalBinaryReader(clientPacket);
        var opcode = (PlayerToServerPacketId)reader.ReadGChar();
        if (opcode != PlayerToServerPacketId.HurtPlayer)
            return new InboundHurtPlayerParseResult(false, null);

        var packet = new InboundHurtPlayerPacket(
            reader.ReadGShort(),
            reader.ReadGChar(),
            reader.ReadGChar(),
            reader.ReadGChar(),
            unchecked((uint)reader.ReadGInt()));

        return new InboundHurtPlayerParseResult(true, packet);
    }

    public static byte[] BaddyHurtToLeader(ReadOnlySpan<byte> clientPacket, bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.BaddyHurt);
        WriteAfterClientOpcode(writer, clientPacket);
        AppendNewline(writer, appendNewline);
        return writer.ToArray();
    }

    public static byte[] Explosion(
        ushort playerId,
        byte radius,
        byte encodedX,
        byte encodedY,
        byte power,
        bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.Explosion);
        writer.WriteGShort(playerId);
        writer.WriteGChar(radius);
        writer.WriteGChar(encodedX);
        writer.WriteGChar(encodedY);
        writer.WriteGChar(power);
        AppendNewline(writer, appendNewline);
        return writer.ToArray();
    }

    public static byte[] HitObjectsFromPlayer(
        ushort playerId,
        byte encodedPower,
        byte encodedX,
        byte encodedY,
        bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.HitObjects);
        writer.WriteGShort(playerId);
        writer.WriteGChar(encodedPower);
        writer.WriteGChar(encodedX);
        writer.WriteGChar(encodedY);
        AppendNewline(writer, appendNewline);
        return writer.ToArray();
    }

    public static byte[] HitObjectsFromNpc(
        uint npcId,
        byte encodedPower,
        byte encodedX,
        byte encodedY,
        bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.HitObjects);
        writer.WriteGShort(0);
        writer.WriteGChar(encodedPower);
        writer.WriteGChar(encodedX);
        writer.WriteGChar(encodedY);
        writer.WriteGInt(npcId);
        AppendNewline(writer, appendNewline);
        return writer.ToArray();
    }

    private static void WriteAfterClientOpcode(GraalBinaryWriter writer, ReadOnlySpan<byte> clientPacket)
    {
        if (clientPacket.Length > 1)
            writer.WriteBytes(clientPacket[1..]);
    }

    private static void AppendNewline(GraalBinaryWriter writer, bool appendNewline)
    {
        if (appendNewline)
            writer.WriteByte((byte)'\n');
    }
}
