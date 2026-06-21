using Preagonal.GServer.Protocol;
using System.Globalization;
using System.Text;

namespace Preagonal.GServer.Game;

public sealed record LevelChestOpenResult(
    bool Opened,
    string ChestKey,
    LevelItemType ItemType,
    byte[] Packet)
{
    public static LevelChestOpenResult NotOpened { get; } =
        new(false, string.Empty, LevelItemType.Invalid, []);
}

public static class LevelInteraction
{
    public static NwLevelLink? FindTouchedLink(NwLevelSnapshot level, int tileX, int tileY)
    {
        foreach (var link in level.Links)
        {
            if (tileX >= link.X &&
                tileX <= link.X + link.Width &&
                tileY >= link.Y &&
                tileY <= link.Y + link.Height)
            {
                return link;
            }
        }

        return null;
    }

    public static string BuildChestKey(NwLevelChest chest, string levelName)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{chest.X}:{chest.Y}:{levelName}");
    }

    public static LevelChestOpenResult TryOpenChest(
        NwLevelSnapshot level,
        string levelName,
        byte x,
        byte y,
        ISet<string> openedChests)
    {
        foreach (var chest in level.Chests)
        {
            if (chest.X != x || chest.Y != y)
                continue;

            var chestKey = BuildChestKey(chest, levelName);
            if (openedChests.Contains(chestKey))
                return LevelChestOpenResult.NotOpened;

            openedChests.Add(chestKey);
            return new LevelChestOpenResult(true, chestKey, chest.ItemType, BuildOpenedChestPacket(x, y));
        }

        return LevelChestOpenResult.NotOpened;
    }

    public static LevelChestOpenResult TryOpenChestAndApplyReward(
        NwLevelSnapshot level,
        string levelName,
        byte x,
        byte y,
        ISet<string> openedChests,
        DurablePlayerInventoryState player)
    {
        var result = TryOpenChest(level, levelName, x, y, openedChests);
        if (!result.Opened)
            return result;

        var payload = InventoryItemRules.BuildPickupPlayerProps(result.ItemType, player);
        InventoryItemRules.ApplyPickupPlayerProps(payload, player);
        return result;
    }

    public static byte[] BuildTouchedSignPackets(
        NwLevelSnapshot level,
        bool serverside,
        int sprite,
        float x,
        float y)
    {
        if (!serverside || sprite % 4 != 0)
            return [];

        var output = new List<byte>();
        foreach (var sign in level.Signs)
        {
            if (y == sign.Y && x >= sign.X - 1.5f && x <= sign.X + 0.5f)
                output.AddRange(BuildSay2Packet(sign.Text.Replace("\n", "#b", StringComparison.Ordinal)));
        }

        return output.ToArray();
    }

    public static byte[] BuildMovementTriggeredSignPackets(
        NwLevelSnapshot level,
        RuntimePlayer player,
        bool serverside)
    {
        if (!player.MovementUpdated || !player.TouchTestRequested)
            return [];

        return BuildTouchedSignPackets(
            level,
            serverside,
            player.Sprite,
            player.PixelX / 16.0f,
            player.PixelY / 16.0f);
    }

    private static byte[] BuildOpenedChestPacket(byte x, byte y)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.LevelChest);
        writer.WriteGChar(1);
        writer.WriteGChar(x);
        writer.WriteGChar(y);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static byte[] BuildSay2Packet(string text)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.Say2);
        writer.WriteBytes(Encoding.ASCII.GetBytes(text));
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }
}
