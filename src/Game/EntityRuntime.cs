using System.Text;
using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Game;

public sealed record RuntimeLevelItem(float X, float Y, LevelItemType ItemType);

public sealed record RuntimeHorse(string Image, float X, float Y, byte Direction, byte Bushes);

public sealed record RuntimeBoardChangePacket(byte[] Packet);

internal sealed class RuntimeBoardChange(byte[] respawnPayload, int respawnTime)
{
    private readonly RuntimeTimeoutCounter _timeout = new(respawnTime);

    public byte[] RespawnPayload { get; } = respawnPayload;

    public bool Tick() => respawnTime >= 0 && _timeout.DoTimeout() == 0;
}

public sealed record LevelItemRuntimeResult(
    bool ChangedLevel,
    byte[] ForwardPacket,
    byte[] SelfPacket,
    byte[] PlayerPropsPayload,
    LevelItemType ItemType = LevelItemType.Invalid);

public static class RuntimePlayerInventoryState
{
    public static DurablePlayerInventoryState Capture(RuntimePlayer player) =>
        new()
        {
            Rupees = player.Rupees,
            Bombs = player.Bombs,
            Arrows = player.Arrows,
            Hitpoints = player.Hitpoints,
            MaxPower = player.MaxPower,
            GlovePower = player.GlovePower,
            ShieldPower = player.ShieldPower,
            SwordPower = player.SwordPower,
            Status = player.Status
        };

    public static void Apply(RuntimePlayer player, DurablePlayerInventoryState state)
    {
        player.Rupees = state.Rupees;
        player.Bombs = state.Bombs;
        player.Arrows = state.Arrows;
        player.Hitpoints = state.Hitpoints;
        player.MaxPower = state.MaxPower;
        player.GlovePower = state.GlovePower;
        player.ShieldPower = state.ShieldPower;
        player.SwordPower = state.SwordPower;
        player.Status = state.Status;
    }
}

public static class LevelItemRuntime
{
    public static LevelItemRuntimeResult SpawnLevelItem(
        RuntimeLevel level,
        byte encodedX,
        byte encodedY,
        byte itemId,
        bool playerDrop,
        DurablePlayerInventoryState player)
    {
        var itemType = LevelItemCatalog.GetItemId(itemId);
        if (itemType == LevelItemType.Invalid)
            return NoChange();

        if (playerDrop && !InventoryItemRules.TryRemoveForPlayerDrop(itemType, player))
            return NoChange();

        var x = encodedX / 2.0f;
        var y = encodedY / 2.0f;
        if (level.AddItem(x, y, itemType))
            return new LevelItemRuntimeResult(true, EntityPackets.ItemAdd(encodedX, encodedY, itemId), [], [], itemType);

        return new LevelItemRuntimeResult(false, [], EntityPackets.ItemDelete(encodedX, encodedY), [], itemType);
    }

    public static LevelItemRuntimeResult DeleteOrTakeLevelItem(
        RuntimeLevel level,
        byte encodedX,
        byte encodedY,
        bool takeItem,
        DurablePlayerInventoryState player)
    {
        var forwardPacket = EntityPackets.ItemDelete(encodedX, encodedY);
        var itemType = level.RemoveItem(encodedX / 2.0f, encodedY / 2.0f);
        if (itemType == LevelItemType.Invalid)
            return new LevelItemRuntimeResult(false, forwardPacket, [], []);

        var playerPropsPayload = Array.Empty<byte>();
        if (takeItem)
        {
            playerPropsPayload = InventoryItemRules.BuildPickupPlayerProps(itemType, player);
            InventoryItemRules.ApplyPickupPlayerProps(playerPropsPayload, player);
        }

        return new LevelItemRuntimeResult(true, forwardPacket, [], playerPropsPayload, itemType);
    }

    private static LevelItemRuntimeResult NoChange() =>
        new(false, [], [], []);
}

public static class BoardChangeRuntime
{
    private static readonly HashSet<ushort> RespawningTiles =
    [
        0x1ff, 0x3ff, 0x2ac, 0x002, 0x200, 0x022, 0x3de, 0x1a4, 0x14a, 0x674, 0x72a
    ];

    public static byte[] BuildBoardModifyPacket(ReadOnlySpan<byte> payload)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.BoardModify);
        writer.WriteBytes(payload);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    public static byte[] BuildPayload(byte x, byte y, byte width, byte height, ReadOnlySpan<byte> tiles)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar(x);
        writer.WriteGChar(y);
        writer.WriteGChar(width);
        writer.WriteGChar(height);
        writer.WriteBytes(tiles);
        return writer.ToArray();
    }

    public static bool ShouldRespawn(ushort tile) => RespawningTiles.Contains(tile);

    public static byte[] BuildOldTilePayload(NwLevelSnapshot level, byte x, byte y, byte width, byte height)
    {
        var tiles = new GraalBinaryWriter();
        for (var row = y; row < y + height && row < 64; row++)
        {
            for (var column = x; column < x + width && column < 64; column++)
                tiles.WriteGShort(level.GetTile(0, column, row));
        }

        return BuildPayload(x, y, width, height, tiles.ToArray());
    }

    public static LevelItemType RollTileDrop(ushort oldTile, bool bushItems, bool vasesDrop, int tileDropRate, Random rng)
    {
        if ((oldTile is 2 or 0x1a4 or 0x1ff or 0x3ff) && bushItems && tileDropRate > 0 && rng.Next(100) < tileDropRate)
            return LevelItemCatalog.GetItemId(rng.Next(6));

        return oldTile == 0x2ac && vasesDrop
            ? LevelItemType.Heart
            : LevelItemType.Invalid;
    }
}

public enum BaddyMode : byte
{
    Walk = 0,
    Look = 1,
    Hunt = 2,
    Hurt = 3,
    Bumped = 4,
    Die = 5,
    SwampShot = 6,
    HareJump = 7,
    OctoShot = 8,
    Dead = 9
}

public sealed class RuntimeBaddy
{
    public const int BaddyDropRange = 12;
    private const byte BaddyVersionFallbackClient = 217;

    private static readonly string[] Images =
    [
        "baddygray.png", "baddyblue.png", "baddyred.png", "baddyblue.png", "baddygray.png",
        "baddyhare.png", "baddyoctopus.png", "baddygold.png", "baddylizardon.png", "baddydragon.png"
    ];

    private static readonly byte[] StartModes = [0, 0, 0, 0, 6, 7, 0, 0, 0, 0];
    private static readonly byte[] Powers = [2, 3, 4, 3, 2, 1, 1, 6, 12, 8];
    private readonly List<string> _verses = ["", "", ""];
    private bool _hasCustomImage;
    private readonly float _startX;
    private readonly float _startY;

    private RuntimeBaddy(byte id, float x, float y, byte type)
    {
        Id = id;
        X = x;
        Y = y;
        Type = type > Images.Length ? (byte)0 : type;
        _startX = x;
        _startY = y;
        Power = Powers[Math.Min(Type, (byte)(Powers.Length - 1))];
        Image = Images[Type];
        Mode = StartModes[Type];
        Direction = (2 << 2) | 2;
        Timeout = new RuntimeTimeoutCounter();
    }

    public byte Id { get; private set; }
    public float X { get; private set; }
    public float Y { get; private set; }
    public byte Type { get; private set; }
    public byte Power { get; private set; }
    public string Image { get; private set; }
    public byte Mode { get; private set; }
    public byte Ani { get; private set; }
    public byte Direction { get; private set; }
    public IReadOnlyList<string> Verses => _verses;
    public RuntimeTimeoutCounter Timeout { get; }
    public bool CanRespawn { get; private set; } = true;

    public sealed record BaddyDropPacket(byte[] Packet, float X, float Y, LevelItemType ItemType);

    public static RuntimeBaddy Create(byte id, float x, float y, byte type) => new(id, x, y, type);

    public void SetRespawn(bool respawn)
    {
        CanRespawn = respawn;
    }

    public void Reset(int clientVersion = BaddyVersionFallbackClient)
    {
        Type = (byte)Math.Min(Type, (byte)(Images.Length - 1));
        Power = Powers[Math.Min(Type, (byte)(Powers.Length - 1))];
        Image = Images[Math.Min(Type, (byte)(Images.Length - 1))];
        Direction = (2 << 2) | 2;
        Ani = 0;
        Mode = StartModes[Math.Min(Type, (byte)(StartModes.Length - 1))];
        X = _startX;
        Y = _startY;
        _hasCustomImage = false;
        for (var i = 0; i < 3; i++)
            _verses[i] = string.Empty;
    }

    public bool SetProps(
        ReadOnlySpan<byte> props,
        bool baddyItemsEnabled,
        int baddyRespawnTime,
        ICombatRandom? rng,
        out byte? droppedItemPacketBaddyId)
    {
        droppedItemPacketBaddyId = null;
        if (props.IsEmpty)
            return false;

        var reader = new GraalBinaryReader(props.ToArray());
        var removeRequested = false;

        while (reader.BytesLeft > 0)
        {
            var propId = reader.ReadGChar();
            switch (propId)
            {
                case (byte)BaddyPropId.Id:
                    Id = reader.ReadGChar();
                    break;
                case (byte)BaddyPropId.X:
                    X = reader.ReadGChar() / 2.0f;
                    X = Math.Clamp(X, 0.0f, 63.5f);
                    break;
                case (byte)BaddyPropId.Y:
                    Y = reader.ReadGChar() / 2.0f;
                    Y = Math.Clamp(Y, 0.0f, 63.5f);
                    break;
                case (byte)BaddyPropId.Type:
                    Type = reader.ReadGChar();
                    break;
                case (byte)BaddyPropId.PowerImage:
                    Power = reader.ReadGChar();
                    if (reader.BytesLeft != 0)
                    {
                        var imageLength = reader.ReadByte();
                        if (imageLength == 0)
                        {
                            Image = Images[Math.Min(Type, (byte)(Images.Length - 1))];
                        }
                        else if (!_hasCustomImage)
                        {
                            _hasCustomImage = true;
                            Image = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(imageLength));
                        }
                    }
                    break;
                case (byte)BaddyPropId.Mode:
                    Mode = reader.ReadGChar();
                    if (Type == 4 && Mode == (byte)BaddyMode.Hurt)
                    {
                        Timeout.SetTimeout(2);
                    }
                    else if (Mode == (byte)BaddyMode.Dead)
                    {
                        if (CanRespawn)
                        {
                            Timeout.SetTimeout(Math.Max(baddyRespawnTime, 0));
                        }
                        else
                        {
                            removeRequested = true;
                        }
                    }
                    else if (Mode == (byte)BaddyMode.Die)
                    {
                        Timeout.SetTimeout(2);
                        if (baddyItemsEnabled)
                        {
                            if (rng is not null && TryRollDrop(rng, out var droppedPacket))
                            {
                                droppedItemPacketBaddyId = Id;
                                _droppedPackets.Add(droppedPacket);
                            }
                        }
                    }
                    break;
                case (byte)BaddyPropId.Ani:
                    Ani = reader.ReadGChar();
                    break;
                case (byte)BaddyPropId.Dir:
                    Direction = reader.ReadGChar();
                    break;
                case (byte)BaddyPropId.VerseSight:
                case (byte)BaddyPropId.VerseHurt:
                case (byte)BaddyPropId.VerseAttack:
                    {
                        var len = reader.ReadByte();
                        var verseId = propId - (byte)BaddyPropId.VerseSight;
                        if (verseId < _verses.Count)
                            _verses[verseId] = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(len));
                    }
                    break;
            }
        }

        return removeRequested;
    }

    public IReadOnlyList<BaddyDropPacket> PopDroppedPackets()
    {
        if (_droppedPackets.Count == 0)
            return [];

        var packets = _droppedPackets.ToArray();
        _droppedPackets.Clear();
        return packets;
    }

    public byte[] BuildModeProps(byte mode)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)BaddyPropId.Mode);
        writer.WriteGChar(mode);
        return writer.ToArray();
    }

    public byte[] GetBaddyProps(int clientVersion = BaddyVersionFallbackClient)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteBytes(BuildBaddyRuntimeProps(clientVersion));
        return writer.ToArray();
    }

    private bool TryRollDrop(ICombatRandom rng, out BaddyDropPacket packet)
    {
        if (CombatDropRuntime.TryRollBaddyDrop(rng, out var itemType) && itemType != LevelItemType.Invalid)
        {
            packet = new BaddyDropPacket(CombatDropRuntime.BuildBaddyDropPacket(X, Y, itemType), X, Y, itemType);
            _droppedPackets.Add(packet);
            return true;
        }

        packet = new BaddyDropPacket([], X, Y, LevelItemType.Invalid);
        return false;
    }

    private byte[] BuildBaddyRuntimeProps(int clientVersion)
    {
        var props = new GraalBinaryWriter();
        props.WriteGChar((byte)BaddyPropId.X);
        props.WriteGChar((byte)(X * 2));
        props.WriteGChar((byte)BaddyPropId.Y);
        props.WriteGChar((byte)(Y * 2));
        props.WriteGChar((byte)BaddyPropId.Type);
        props.WriteGChar(Type);
        props.WriteGChar((byte)BaddyPropId.PowerImage);
        props.WriteGChar(Power);
        var image = clientVersion < 210 ? Image.Replace(".png", ".gif", StringComparison.Ordinal) : Image;
        var imageBytes = System.Text.Encoding.ASCII.GetBytes(image);
        props.WriteGChar((byte)imageBytes.Length);
        props.WriteBytes(imageBytes);
        props.WriteGChar((byte)BaddyPropId.Mode);
        props.WriteGChar(Mode);
        props.WriteGChar((byte)BaddyPropId.Ani);
        props.WriteGChar(Ani);
        props.WriteGChar((byte)BaddyPropId.Dir);
        props.WriteGChar(Direction);
        for (byte propId = (byte)BaddyPropId.VerseSight; propId <= (byte)BaddyPropId.VerseAttack; propId++)
        {
            props.WriteGChar(propId);
            props.WriteGChar(0);
        }

        return props.ToArray();
    }

    private readonly List<BaddyDropPacket> _droppedPackets = [];

    private enum BaddyPropId : byte
    {
        Id = 0,
        X = 1,
        Y = 2,
        Type = 3,
        PowerImage = 4,
        Mode = 5,
        Ani = 6,
        Dir = 7,
        VerseSight = 8,
        VerseHurt = 9,
        VerseAttack = 10
    }
}

public static class EntityRuntimePackets
{
    public static byte[] BaddyProps(RuntimeBaddy baddy, int clientVersion)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.BaddyProps);
        writer.WriteGChar(baddy.Id);
        WriteBaddyProps(writer, baddy, clientVersion);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static void WriteBaddyProps(GraalBinaryWriter writer, RuntimeBaddy baddy, int clientVersion)
    {
        writer.WriteGChar(1);
        writer.WriteGChar((byte)(baddy.X * 2));
        writer.WriteGChar(2);
        writer.WriteGChar((byte)(baddy.Y * 2));
        writer.WriteGChar(3);
        writer.WriteGChar(baddy.Type);
        writer.WriteGChar(4);
        writer.WriteGChar(baddy.Power);
        var image = clientVersion < 210 ? baddy.Image.Replace(".png", ".gif", StringComparison.Ordinal) : baddy.Image;
        var imageBytes = Encoding.ASCII.GetBytes(image);
        writer.WriteGChar((byte)imageBytes.Length);
        writer.WriteBytes(imageBytes);
        writer.WriteGChar(5);
        writer.WriteGChar(baddy.Mode);
        writer.WriteGChar(6);
        writer.WriteGChar(baddy.Ani);
        writer.WriteGChar(7);
        writer.WriteGChar(baddy.Direction);

        for (byte propId = 8; propId <= 10; propId++)
        {
            writer.WriteGChar(propId);
            writer.WriteGChar(0);
        }
    }
}
