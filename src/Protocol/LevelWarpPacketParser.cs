using System.Text;

namespace Preagonal.GServer.Protocol;

public sealed record LevelWarpPacket(
    PlayerToServerPacketId PacketId,
    string LevelName,
    float X,
    float Y,
    long ModTime);

public static class LevelWarpPacketParser
{
    public static LevelWarpPacket Parse(ReadOnlySpan<byte> packet)
    {
        var reader = new GraalBinaryReader(packet);
        var packetId = (PlayerToServerPacketId)reader.ReadGChar();

        var modTime = 0L;
        if (packetId == PlayerToServerPacketId.LevelWarpMod)
            modTime = reader.ReadGInt5();
        else if (packetId != PlayerToServerPacketId.LevelWarp)
            throw new InvalidDataException($"Packet id {packetId} is not handled by msgPLI_LEVELWARP.");

        var x = reader.ReadGChar() / 2.0f;
        var y = reader.ReadGChar() / 2.0f;
        var levelName = Encoding.ASCII.GetString(reader.ReadBytes(reader.BytesLeft));

        return new LevelWarpPacket(packetId, levelName, x, y, modTime);
    }
}
