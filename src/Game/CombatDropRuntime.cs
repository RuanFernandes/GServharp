using GServ.Protocol;

namespace GServ.Game;

public interface ICombatRandom
{
    int Next(int maxExclusive);
}

public sealed class DefaultCombatRandom : ICombatRandom
{
    private readonly Random _rng;

    public DefaultCombatRandom(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    }

    public int Next(int maxExclusive) => _rng.Next(maxExclusive);
}

public sealed record PlayerDropResult(int RemainingRupees, byte RemainingArrows, byte RemainingBombs, IReadOnlyList<byte[]> DropPackets);

public static class CombatDropRuntime
{
    public const int BaddyDropRange = 12;
    public const int DropArrowsBombsRange = 4;
    public const byte BaddyDropArrowsItem = (byte)LevelItemType.Darts;
    public const byte BaddyDropBombsItem = (byte)LevelItemType.Bombs;

    public static int ComputeDroppedGralats(int maxDrop, int minDrop, int currentRupees, ICombatRandom rng)
    {
        if (maxDrop <= 0)
            return 0;

        int dropGralats = rng.Next(maxDrop) % maxDrop;
        dropGralats = Math.Clamp(dropGralats, minDrop, maxDrop);
        return Math.Min(dropGralats, currentRupees);
    }

    public static int ComputeDroppedArrows(byte currentArrows, ICombatRandom rng)
    {
        int droppedArrows = rng.Next(DropArrowsBombsRange) % DropArrowsBombsRange;
        int candidate = droppedArrows * 5;
        if (candidate > currentArrows)
            candidate = (currentArrows / 5) * 5;

        return candidate / 5;
    }

    public static int ComputeDroppedBombs(byte currentBombs, ICombatRandom rng)
    {
        int droppedBombs = rng.Next(DropArrowsBombsRange) % DropArrowsBombsRange;
        int candidate = droppedBombs * 5;
        if (candidate > currentBombs)
            candidate = (currentBombs / 5) * 5;

        return candidate / 5;
    }

    public static IReadOnlyList<LevelItemType> DecomposeGralats(int gralats)
    {
        if (gralats <= 0)
            return [];

        var result = new List<LevelItemType>(6);
        var remaining = gralats;

        while (remaining != 0)
        {
            if (remaining % 100 != remaining)
            {
                remaining -= 100;
                result.Add(LevelItemType.GoldRupee);
            }
            else if (remaining % 30 != remaining)
            {
                remaining -= 30;
                result.Add(LevelItemType.RedRupee);
            }
            else if (remaining % 5 != remaining)
            {
                remaining -= 5;
                result.Add(LevelItemType.BlueRupee);
            }
            else
            {
                --remaining;
                result.Add(LevelItemType.GreenRupee);
            }
        }

        return result;
    }

    public static bool TryRollBaddyDrop(ICombatRandom rng, out LevelItemType itemType)
    {
        int roll = rng.Next(BaddyDropRange) % BaddyDropRange;
        itemType = roll switch
        {
            0 => LevelItemType.GreenRupee,
            1 => LevelItemType.BlueRupee,
            2 => LevelItemType.RedRupee,
            3 => LevelItemType.Bombs,
            4 => LevelItemType.Darts,
            5 => LevelItemType.Heart,
            6 or 7 or 8 or 9 => LevelItemType.GreenRupee,
            _ => LevelItemType.Invalid
        };

        return itemType != LevelItemType.Invalid;
    }

    public static PlayerDropResult ApplyPlayerDeathDrops(
        DurablePlayerInventoryState player,
        bool dropItemsDead,
        int minDeathGralats,
        int maxDeathGralats,
        float playerX,
        float playerY,
        ICombatRandom rng)
    {
        if (!dropItemsDead)
        {
            return new PlayerDropResult(player.Rupees, player.Arrows, player.Bombs, []);
        }

        var dropGralats = ComputeDroppedGralats(maxDeathGralats, minDeathGralats, player.Rupees, rng);
        var dropArrows = ComputeDroppedArrows(player.Arrows, rng);
        var dropBombs = ComputeDroppedBombs(player.Bombs, rng);

        player.Rupees -= dropGralats;
        player.Arrows -= (byte)(dropArrows * 5);
        player.Bombs -= (byte)(dropBombs * 5);

        var packets = new List<byte[]>();

        foreach (var item in DecomposeGralats(dropGralats))
        {
            var (encodedX, encodedY) = ComputeDropCoords(playerX, playerY, rng);
            packets.Add(EntityPackets.ItemAdd(encodedX, encodedY, (byte)item));
        }

        for (var i = 0; i < dropArrows; ++i)
        {
            var (encodedX, encodedY) = ComputeDropCoords(playerX, playerY, rng);
            packets.Add(EntityPackets.ItemAdd(encodedX, encodedY, BaddyDropArrowsItem));
        }

        for (var i = 0; i < dropBombs; ++i)
        {
            var (encodedX, encodedY) = ComputeDropCoords(playerX, playerY, rng);
            packets.Add(EntityPackets.ItemAdd(encodedX, encodedY, BaddyDropBombsItem));
        }

        return new PlayerDropResult(player.Rupees, player.Arrows, player.Bombs, packets);
    }

    public static (byte EncodedX, byte EncodedY) ComputeDropCoords(float originX, float originY, ICombatRandom rng)
    {
        float x = originX + 1.5f + rng.Next(8) - 2.0f;
        float y = originY + 2.0f + rng.Next(8) - 2.0f;
        return ((byte)(x * 2), (byte)(y * 2));
    }

    public static byte[] BuildBaddyDropPacket(float x, float y, LevelItemType dropItem)
    {
        return EntityPackets.ItemAdd((byte)(x * 2), (byte)(y * 2), (byte)dropItem);
    }
}
