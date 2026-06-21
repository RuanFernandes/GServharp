using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

internal static class LevelGoldenFixtures
{
    public static readonly IReadOnlyList<LevelFormatExtensionFixture> ExtensionFormatFixtures =
    [
        new("start.nw", LevelFileFormat.Nw),
        new("start.graal", LevelFileFormat.Graal),
        new("start.zelda", LevelFileFormat.Zelda),
        new("start.txt", LevelFileFormat.Unknown),
        new("START.NW", LevelFileFormat.Unknown),
    ];

    public static readonly IReadOnlyList<LevelFormatHeaderFixture> HeaderFormatFixtures =
    [
        new("GLEVNW01", LevelFileFormat.Nw),
        new("GR-V1.03", LevelFileFormat.Graal),
        new("GR-V1.02", LevelFileFormat.Graal),
        new("GR-V1.01", LevelFileFormat.Graal),
        new("Z3-V1.04", LevelFileFormat.Zelda),
        new("Z3-V1.03", LevelFileFormat.Zelda),
        new("UNKNOWN!", LevelFileFormat.Unknown),
    ];

    public const string RepresentativeNwBoardSource =
        """
        GLEVNW01
        BOARD 0 0 2 0 AB+/
        """;

    public static readonly byte[] RepresentativeNwBoardPacketPrefix = [133, 1, 0, 191, 15];

    public const string RepresentativeNwLayerSource =
        """
        GLEVNW01
        BOARD 0 0 1 1 +/
        """;

    public static readonly byte[] RepresentativeNwLayerPacketPrefix = [139, 1, 0, 0, 64, 64, 191, 15];

    public const string RepresentativeNwLinkSource =
        """
        GLEVNW01
        LINK target level.nw 1 2 3 4 5.5 6.5
        """;

    public static readonly byte[] RepresentativeNwLinkPacket =
    [
        33,
        (byte)'t', (byte)'a', (byte)'r', (byte)'g', (byte)'e', (byte)'t', (byte)' ',
        (byte)'l', (byte)'e', (byte)'v', (byte)'e', (byte)'l', (byte)'.', (byte)'n', (byte)'w',
        (byte)' ', (byte)'1', (byte)' ', (byte)'2', (byte)' ', (byte)'3', (byte)' ', (byte)'4',
        (byte)' ', (byte)'5', (byte)'.', (byte)'5', (byte)' ', (byte)'6', (byte)'.', (byte)'5',
        10,
    ];

    public const string RepresentativeNwSignSource =
        """
        GLEVNW01
        SIGN 4 5
        A
        SIGNEND
        """;

    public static readonly byte[] RepresentativeNwSignPacket = [37, 36, 37, 32, 128, 10];

    public const string RepresentativeNwChestSource =
        """
        GLEVNW01
        CHEST 10 11 redrupee 3
        CHEST 12 13 bluerupee 4
        """;

    public static readonly byte[] RepresentativeNwChestPacket =
    [
        36, 32, 42, 43, 34, 35, 10,
        36, 33, 44, 45, 10,
    ];
}

internal sealed record LevelFormatExtensionFixture(string LevelName, LevelFileFormat Format);

internal sealed record LevelFormatHeaderFixture(string Header, LevelFileFormat Format);
