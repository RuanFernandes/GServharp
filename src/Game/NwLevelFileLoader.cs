namespace Preagonal.GServer.Game;

public enum LevelLoadStatus
{
    Loaded,
    Missing,
    UnsupportedFormat,
    Malformed,
}

public sealed record NwLevelStaticPayload(
    string LevelName,
    long LevelModTime,
    byte[] BoardPacket,
    IReadOnlyList<NwLevelStaticLayerPayload> Layers,
    byte[] LinksPacket,
    byte[] SignsPacket,
    IReadOnlyList<NwLevelStaticChestPayload> Chests,
    byte[] ChestsPacket);

public sealed record NwLevelStaticLayerPayload(int LayerIndex, byte[] Packet);
public sealed record NwLevelStaticChestPayload(bool HasChest, byte X, byte Y, byte ItemIndex, byte SignIndex);

public sealed record LoadedNwLevel(
    bool Success,
    LevelLoadStatus Status,
    string LevelName,
    long ModTime,
    NwLevelSnapshot Level)
{
    public static LoadedNwLevel Missing(string levelName) =>
        new(false, LevelLoadStatus.Missing, levelName, 0, NwLevelSnapshot.Empty);

    public static LoadedNwLevel Unsupported(string levelName, long modTime) =>
        new(false, LevelLoadStatus.UnsupportedFormat, levelName, modTime, NwLevelSnapshot.Empty);

    public static LoadedNwLevel Malformed(string levelName, long modTime) =>
        new(false, LevelLoadStatus.Malformed, levelName, modTime, NwLevelSnapshot.Empty);

    public NwLevelStaticPayload ToModernStaticPayload(Func<string, bool> playerHasChest)
    {
        if (!Success)
            throw new InvalidOperationException("Cannot build level packets for an unloaded level.");

        var layers = Level.Layers
            .Where(layer => layer.Key != 0)
            .OrderBy(layer => layer.Key)
            .Select(layer => new NwLevelStaticLayerPayload(
                layer.Key,
                NwLevelPacketBuilder.BuildLayerPacket(Level, layer.Key)))
            .ToArray();

        var chests = Level.Chests
            .Select(chest =>
            {
                var hasChest = playerHasChest($"{chest.X}:{chest.Y}:{LevelName}");
                return new NwLevelStaticChestPayload(
                    hasChest,
                    (byte)chest.X,
                    (byte)chest.Y,
                    (byte)chest.ItemType,
                    (byte)chest.SignIndex);
            })
            .ToArray();

        return new NwLevelStaticPayload(
            LevelName,
            ModTime,
            NwLevelPacketBuilder.BuildBoardPacket(Level),
            layers,
            NwLevelPacketBuilder.BuildLinksPacket(Level),
            NwLevelPacketBuilder.BuildSignsPacket(Level),
            chests,
            NwLevelPacketBuilder.BuildChestPacket(Level, LevelName, playerHasChest));
    }
}

public sealed class NwLevelFileLoader(IServerFileSystem fileSystem)
{
    public LoadedNwLevel TryLoad(string levelName, Func<string, bool>? linkTargetExists = null)
    {
        var path = fileSystem.Find(levelName);
        if (path.Length == 0)
            return LoadedNwLevel.Missing(levelName);

        var modTime = fileSystem.GetModTime(levelName);
        var content = fileSystem.Load(levelName);
        var header = System.Text.Encoding.ASCII.GetBytes(content.Length >= 8 ? content[..8] : content);
        var format = LevelFileFormatDetector.Choose(levelName, header);
        if (format != LevelFileFormat.Nw)
            return LoadedNwLevel.Unsupported(levelName, modTime);

        var parsed = NwLevelParser.Parse(content, linkTargetExists);
        return parsed.Success
            ? new LoadedNwLevel(true, LevelLoadStatus.Loaded, levelName, modTime, parsed.Level)
            : LoadedNwLevel.Malformed(levelName, modTime);
    }
}
