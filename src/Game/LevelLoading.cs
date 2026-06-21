using System.Text;

namespace Preagonal.GServer.Game;

public enum LevelFileFormat
{
    Unknown = -1,
    Nw = 0,
    Graal = 1,
    Zelda = 2,
}

public static class LevelFileFormatDetector
{
    private static readonly byte[] NwHeader = Encoding.ASCII.GetBytes("GLEVNW01");
    private static readonly byte[] GraalHeader103 = Encoding.ASCII.GetBytes("GR-V1.03");
    private static readonly byte[] GraalHeader102 = Encoding.ASCII.GetBytes("GR-V1.02");
    private static readonly byte[] GraalHeader101 = Encoding.ASCII.GetBytes("GR-V1.01");
    private static readonly byte[] ZeldaHeader104 = Encoding.ASCII.GetBytes("Z3-V1.04");
    private static readonly byte[] ZeldaHeader103 = Encoding.ASCII.GetBytes("Z3-V1.03");

    public static LevelFileFormat Choose(string levelName, ReadOnlySpan<byte> firstEightBytes)
    {
        var extensionFormat = FromExtension(levelName);
        return extensionFormat != LevelFileFormat.Unknown
            ? extensionFormat
            : DetectFromHeader(firstEightBytes);
    }

    public static LevelFileFormat FromExtension(string levelName)
    {
        var extension = Path.GetExtension(levelName);

        return extension switch
        {
            ".nw" => LevelFileFormat.Nw,
            ".graal" => LevelFileFormat.Graal,
            ".zelda" => LevelFileFormat.Zelda,
            _ => LevelFileFormat.Unknown,
        };
    }

    public static LevelFileFormat DetectFromHeader(ReadOnlySpan<byte> firstEightBytes)
    {
        if (firstEightBytes.Length < 8)
        {
            return LevelFileFormat.Unknown;
        }

        var header = firstEightBytes[..8];

        if (header.SequenceEqual(NwHeader))
        {
            return LevelFileFormat.Nw;
        }

        if (header.SequenceEqual(GraalHeader103) ||
            header.SequenceEqual(GraalHeader102) ||
            header.SequenceEqual(GraalHeader101))
        {
            return LevelFileFormat.Graal;
        }

        if (header.SequenceEqual(ZeldaHeader104) ||
            header.SequenceEqual(ZeldaHeader103))
        {
            return LevelFileFormat.Zelda;
        }

        return LevelFileFormat.Unknown;
    }
}
