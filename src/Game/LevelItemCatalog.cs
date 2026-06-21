namespace Preagonal.GServer.Game;

public enum LevelItemType
{
    Invalid = -1,
    GreenRupee = 0,
    BlueRupee = 1,
    RedRupee = 2,
    Bombs = 3,
    Darts = 4,
    Heart = 5,
    Glove1 = 6,
    Bow = 7,
    Bomb = 8,
    Shield = 9,
    Sword = 10,
    FullHeart = 11,
    SuperBomb = 12,
    BattleAxe = 13,
    GoldenSword = 14,
    MirrorShield = 15,
    Glove2 = 16,
    LizardShield = 17,
    LizardSword = 18,
    GoldRupee = 19,
    Fireball = 20,
    Fireblast = 21,
    Nukeshot = 22,
    JoltBomb = 23,
    SpinAttack = 24
}

public static class LevelItemCatalog
{
    private static readonly string[] ItemNames =
    [
        "greenrupee",
        "bluerupee",
        "redrupee",
        "bombs",
        "darts",
        "heart",
        "glove1",
        "bow",
        "bomb",
        "shield",
        "sword",
        "fullheart",
        "superbomb",
        "battleaxe",
        "goldensword",
        "mirrorshield",
        "glove2",
        "lizardshield",
        "lizardsword",
        "goldrupee",
        "fireball",
        "fireblast",
        "nukeshot",
        "joltbomb",
        "spinattack"
    ];

    public static LevelItemType GetItemId(string itemName)
    {
        for (var i = 0; i < ItemNames.Length; i++)
        {
            if (ItemNames[i] == itemName)
            {
                return (LevelItemType)i;
            }
        }

        return LevelItemType.Invalid;
    }

    public static LevelItemType GetItemId(int itemId)
    {
        return itemId < 0 || itemId >= ItemNames.Length
            ? LevelItemType.Invalid
            : (LevelItemType)itemId;
    }

    public static string GetItemName(LevelItemType itemId)
    {
        var id = (int)itemId;
        return id < 0 || id >= ItemNames.Length
            ? string.Empty
            : ItemNames[id];
    }

    public static ushort GetRupeeCount(LevelItemType itemType) =>
        itemType switch
        {
            LevelItemType.GreenRupee => 1,
            LevelItemType.BlueRupee => 5,
            LevelItemType.RedRupee => 30,
            LevelItemType.GoldRupee => 100,
            _ => 0
        };
}
