using System.Text;

namespace Preagonal.GServer.Protocol;

public static class WarpPackets
{
    public static byte[] BuildWarpFailed(string levelName)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.WarpFailed);
        writer.WriteBytes(Encoding.ASCII.GetBytes(levelName));
        return writer.ToArray();
    }

    public static byte[] BuildPlayerWarp(float x, float y, string levelName)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.PlayerWarp);
        writer.WriteGChar((byte)(x * 2));
        writer.WriteGChar((byte)(y * 2));
        writer.WriteBytes(Encoding.ASCII.GetBytes(levelName));
        return writer.ToArray();
    }

    public static byte[] BuildPlayerWarp2(float x, float y, float z, byte mapX, byte mapY, string mapName)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.PlayerWarp2);
        writer.WriteGChar((byte)(x * 2));
        writer.WriteGChar((byte)(y * 2));
        writer.WriteGChar((byte)(z * 2 + 50));
        writer.WriteGChar(mapX);
        writer.WriteGChar(mapY);
        writer.WriteBytes(Encoding.ASCII.GetBytes(mapName));
        return writer.ToArray();
    }

    public static byte[] BuildLevelName(string levelName)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.LevelName);
        writer.WriteBytes(Encoding.ASCII.GetBytes(levelName));
        return writer.ToArray();
    }
}
