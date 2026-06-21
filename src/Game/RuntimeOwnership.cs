using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Game;

public enum RuntimePlayerKind
{
    Client,
    RemoteControl,
    NpcServer,
    NpcControl,
    External
}

public sealed class RuntimePlayer
{
    public RuntimePlayer(ushort id, string accountName, RuntimePlayerKind kind)
    {
        Id = id;
        AccountName = accountName;
        Kind = kind;
    }

    public ushort Id { get; internal set; }
    public string AccountName { get; }
    public string Nickname { get; internal set; } = string.Empty;
    public string Guild { get; internal set; } = string.Empty;
    public uint AccountIp { get; set; }
    public string CommunityName { get; set; } = string.Empty;
    public int EloRating { get; set; } = 1500;
    public int EloDeviation { get; set; } = 350;
    public Preagonal.GServer.Protocol.ClientVersionId ClientVersion { get; set; } =
        Preagonal.GServer.Protocol.ClientVersionId.Client21;
    public RuntimePlayerKind Kind { get; }
    public RuntimeLevel? Level { get; private set; }
    public string? Group { get; set; }
    public bool IsHiddenClient { get; set; }
    public int MapX { get; set; }
    public int MapY { get; set; }
    public int PixelX { get; internal set; }
    public int PixelY { get; internal set; }
    public int PixelZ { get; internal set; }
    public byte Sprite { get; internal set; }
    public PlayerStatus Status { get; internal set; }
    public byte HeartLimit { get; set; } = 3;
    public byte MaxPower { get; set; }
    public float Hitpoints { get; set; }
    public int Rupees { get; internal set; }
    public byte Arrows { get; internal set; }
    public byte Bombs { get; internal set; }
    public byte GlovePower { get; internal set; }
    public byte BombPower { get; internal set; }
    public byte SwordPower { get; set; }
    public string SwordImage { get; set; } = "sword1.png";
    public byte ShieldPower { get; set; }
    public string ShieldImage { get; set; } = "shield1.png";
    public byte BowPower { get; set; } = 1;
    public string BowImage { get; set; } = "bow1.png";
    public ushort ApCounter { get; internal set; }
    public byte MagicPoints { get; internal set; }
    public byte Alignment { get; set; }
    public byte AdditionalFlags { get; internal set; }
    public byte CarrySprite { get; internal set; }
    public byte HorseBombCount { get; internal set; }
    public uint UdpPort { get; internal set; }
    public byte StatusMessage { get; internal set; }
    public uint AttachedNpcId { get; internal set; }
    public string CurrentLevelName { get; internal set; } = string.Empty;
    public string Gani { get; internal set; } = string.Empty;
    public string HeadImage { get; internal set; } = "head0.png";
    public string HorseImage { get; internal set; } = string.Empty;
    public string BodyImage { get; internal set; } = string.Empty;
    public string ChatMessage { get; internal set; } = string.Empty;
    public string Language { get; internal set; } = string.Empty;
    public string Os { get; internal set; } = string.Empty;
    public uint TextCodePage { get; internal set; }
    public IReadOnlyList<byte> Colors => _colors;
    public IReadOnlyList<string> GaniAttributes => _ganiAttributes;
    public bool MovementUpdated { get; internal set; }
    public bool TouchTestRequested { get; internal set; }

    private readonly byte[] _colors = new byte[5];
    private readonly string[] _ganiAttributes = Enumerable.Repeat(string.Empty, 30).ToArray();

    public bool IsClient => Kind == RuntimePlayerKind.Client;

    internal void SetGaniAttribute(int index, string value) =>
        _ganiAttributes[index] = value;

    internal void SetColor(int index, byte value) =>
        _colors[index] = value;

    public void JoinLevel(RuntimeLevel level)
    {
        if (Level == level)
            return;

        LeaveLevel();
        Level = level;
        level.AddPlayer(Id);
    }

    public void LeaveLevel()
    {
        if (Level is not { } level)
            return;

        level.RemovePlayer(Id);
        Level = null;
    }

    public void InitializeFromLogin(Preagonal.GServer.Protocol.PlayerPropertySource source)
    {
        Nickname = source.Nickname;
        CommunityName = source.CommunityName;
        AccountIp = source.AccountIp;
        EloRating = source.EloRating;
        EloDeviation = source.EloDeviation;
        PixelX = source.X;
        PixelY = source.Y;
        PixelZ = source.Z;
        Sprite = source.Sprite;
        Status = (PlayerStatus)source.Status;
        MaxPower = source.MaxPower;
        Hitpoints = source.Hitpoints;
        Rupees = source.Rupees;
        Arrows = source.Arrows;
        Bombs = source.Bombs;
        GlovePower = source.GlovePower;
        SwordPower = source.SwordPower;
        SwordImage = source.SwordImage;
        ShieldPower = source.ShieldPower;
        ShieldImage = source.ShieldImage;
        BowPower = source.BowPower;
        BowImage = source.BowImage;
        ApCounter = source.ApCounter;
        MagicPoints = source.MagicPoints;
        Alignment = source.Alignment;
        AdditionalFlags = source.AdditionalFlags;
        CarrySprite = source.CarrySprite;
        HorseBombCount = source.HorseBombCount;
        UdpPort = source.UdpPort;
        StatusMessage = source.StatusMessage;
        AttachedNpcId = unchecked((uint)source.CarryNpcId);
        CurrentLevelName = source.CurrentLevel;
        Gani = source.Gani;
        HeadImage = source.HeadImage;
        HorseImage = source.HorseImage;
        BodyImage = source.BodyImage;
        ChatMessage = source.ChatMessage;
        Language = source.Language;
        Os = source.Os;
        TextCodePage = source.TextCodePage;

        for (var i = 0; i < Math.Min(_colors.Length, source.Colors.Count); i++)
            _colors[i] = source.Colors[i];

        foreach (var (index, value) in source.GaniAttributes)
        {
            if (index is >= 1 and <= 30)
                _ganiAttributes[index - 1] = value;
        }
    }
}

public static class RuntimePlayerPropsApplier
{
    public static void ApplyConfirmed(
        RuntimePlayer player,
        IEnumerable<Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate> updates,
        RuntimePlayerPropsOptions? options = null)
    {
        options ??= RuntimePlayerPropsOptions.Default;
        foreach (var update in updates)
        {
            switch (update.PropertyId)
            {
                case Preagonal.GServer.Protocol.PlayerPropertyId.MaxPower:
                    player.MaxPower = (byte)Math.Clamp(
                        (int)update.GCharValue.GetValueOrDefault(),
                        0,
                        Math.Min((int)player.HeartLimit, 20));
                    player.Hitpoints = player.MaxPower;
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.CurrentPower:
                    var power = update.GCharValue.GetValueOrDefault() / 2.0f;
                    if (player.Alignment >= 40 || power <= player.Hitpoints)
                        player.Hitpoints = Math.Clamp(power, 0.0f, player.MaxPower);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.RupeesCount:
                    player.Rupees = Math.Clamp(update.GIntValue.GetValueOrDefault(), 0, 9_999_999);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.X:
                    player.PixelX = update.GCharValue.GetValueOrDefault() * 8;
                    MarkMovement(player);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Y:
                    player.PixelY = update.GCharValue.GetValueOrDefault() * 8;
                    MarkMovement(player);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Z:
                    player.PixelZ = (update.GCharValue.GetValueOrDefault() - 50) * 8;
                    MarkMovement(player);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Sprite:
                    player.Sprite = update.GCharValue.GetValueOrDefault();
                    player.TouchTestRequested = true;
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Status:
                    player.Status = (PlayerStatus)update.GCharValue.GetValueOrDefault();
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.ArrowsCount:
                    player.Arrows = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)99);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.BombsCount:
                    player.Bombs = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)99);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.GlovePower:
                    player.GlovePower = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)3);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.BombPower:
                    player.BombPower = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)3);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.SwordPower:
                    ApplySwordPower(player, update, options);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.ShieldPower:
                    ApplyShieldPower(player, update, options);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.ApCounter:
                    player.ApCounter = update.GShortValue.GetValueOrDefault();
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.MagicPoints:
                    player.MagicPoints = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)100);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Alignment:
                    player.Alignment = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)100);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.AdditionalFlags:
                    player.AdditionalFlags = update.GCharValue.GetValueOrDefault();
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.CarrySprite:
                    player.CarrySprite = update.GCharValue.GetValueOrDefault();
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.HorseBushes:
                    player.HorseBombCount = update.GCharValue.GetValueOrDefault();
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Nickname:
                    ApplyNickname(player, update, options);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.PlayerStatusMessage:
                    player.StatusMessage = update.GCharValue.GetValueOrDefault();
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.UdpPort:
                    player.UdpPort = unchecked((uint)update.GIntValue.GetValueOrDefault());
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.AttachNpc:
                    player.AttachedNpcId = GetUnsignedInt(update);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.CurrentLevel:
                    player.CurrentLevelName = update.StringValue ?? string.Empty;
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Gani:
                    if (options.ClientVersion < Preagonal.GServer.Protocol.ClientVersionId.Client21)
                    {
                        ApplyLegacyBowGani(player, update);
                        break;
                    }

                    player.Gani = update.StringValue ?? string.Empty;
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.HeadGif:
                    if (update.StringValue is not null)
                        player.HeadImage = LimitString(update.StringValue, 123);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.HorseGif:
                    player.HorseImage = update.StringValue ?? string.Empty;
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.CurrentChat:
                    player.ChatMessage = LimitString(update.StringValue ?? string.Empty, 223);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.BodyImage:
                    player.BodyImage = LimitString(update.StringValue ?? string.Empty, 223);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Colors:
                    ApplyColors(player, update.BytesValue ?? []);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.PlayerLanguage:
                    player.Language = update.StringValue ?? string.Empty;
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.OsType:
                    player.Os = update.StringValue ?? string.Empty;
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.TextCodePage:
                    player.TextCodePage = unchecked((uint)update.GIntValue.GetValueOrDefault());
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.X2:
                    player.PixelX = DecodePreciseCoordinate(update.GShortValue.GetValueOrDefault());
                    MarkMovement(player);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Y2:
                    player.PixelY = DecodePreciseCoordinate(update.GShortValue.GetValueOrDefault());
                    MarkMovement(player);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Z2:
                    player.PixelZ = DecodePreciseCoordinate(update.GShortValue.GetValueOrDefault());
                    MarkMovement(player);
                    break;

                case Preagonal.GServer.Protocol.PlayerPropertyId.Id:
                case Preagonal.GServer.Protocol.PlayerPropertyId.KillsCount:
                case Preagonal.GServer.Protocol.PlayerPropertyId.DeathsCount:
                case Preagonal.GServer.Protocol.PlayerPropertyId.OnlineSeconds:
                case Preagonal.GServer.Protocol.PlayerPropertyId.IpAddress:
                case Preagonal.GServer.Protocol.PlayerPropertyId.AccountName:
                case Preagonal.GServer.Protocol.PlayerPropertyId.Rating:
                case Preagonal.GServer.Protocol.PlayerPropertyId.JoinLeaveLevel:
                case Preagonal.GServer.Protocol.PlayerPropertyId.PlayerConnected:
                case Preagonal.GServer.Protocol.PlayerPropertyId.Unknown81:
                case Preagonal.GServer.Protocol.PlayerPropertyId.CommunityName:
                    break;

                default:
                    if (TryGetGaniAttributeIndex(update.PropertyId, out var index))
                    {
                        player.SetGaniAttribute(index, update.StringValue ?? string.Empty);
                        break;
                    }

                    throw new NotSupportedException($"Runtime mutation for player prop {(byte)update.PropertyId} is not source-confirmed.");
            }
        }
    }

    private static void MarkMovement(RuntimePlayer player)
    {
        player.Status &= ~PlayerStatus.Paused;
        player.MovementUpdated = true;
        player.TouchTestRequested = true;
    }

    private static void ApplyColors(RuntimePlayer player, IReadOnlyList<byte> colors)
    {
        for (var i = 0; i < 5 && i < colors.Count; i++)
            player.SetColor(i, colors[i]);
    }

    private static void ApplySwordPower(
        RuntimePlayer player,
        Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate update,
        RuntimePlayerPropsOptions options)
    {
        if (update.GCharValue is not { } raw)
            return;

        int power;
        string image;
        if (raw <= 4)
        {
            power = Math.Clamp(raw, 0, options.SwordLimit);
            image = "sword" + power + (options.ClientVersion < Preagonal.GServer.Protocol.ClientVersionId.Client21 ? ".gif" : ".png");
        }
        else
        {
            power = raw - 30;
            image = update.StringValue ?? string.Empty;
        }

        player.SwordPower = (byte)Math.Clamp(power, 0, options.SwordLimit);
        player.SwordImage = LimitString(image, 223);
    }

    private static void ApplyShieldPower(
        RuntimePlayer player,
        Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate update,
        RuntimePlayerPropsOptions options)
    {
        if (update.GCharValue is not { } raw)
            return;

        int power;
        string image;
        if (raw <= 3)
        {
            power = Math.Clamp(raw, 0, options.ShieldLimit);
            image = "shield" + power + (options.ClientVersion < Preagonal.GServer.Protocol.ClientVersionId.Client21 ? ".gif" : ".png");
        }
        else
        {
            power = raw - 10;
            if (power < 0)
                return;
            image = update.StringValue ?? string.Empty;
        }

        player.ShieldPower = (byte)Math.Clamp(power, 0, options.ShieldLimit);
        player.ShieldImage = LimitString(image, 223);
    }

    private static void ApplyLegacyBowGani(
        RuntimePlayer player,
        Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate update)
    {
        if (update.StringValue is { Length: > 0 } image)
        {
            player.BowPower = 10;
            player.BowImage = image;
            return;
        }

        player.BowPower = update.GCharValue.GetValueOrDefault();
        player.BowImage = string.Empty;
    }

    private static void ApplyNickname(
        RuntimePlayer player,
        Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate update,
        RuntimePlayerPropsOptions options)
    {
        if (options.NicknamePolicy != RuntimeNicknameUpdatePolicy.WordFilterAllowedNoGuild)
            throw new NotSupportedException("PLPROP_NICKNAME is blocked until the word filter boundary explicitly allows the nickname update.");

        var nickname = LimitString(update.StringValue ?? string.Empty, 223);
        if (nickname.Contains('(', StringComparison.Ordinal))
            throw new NotSupportedException("PLPROP_NICKNAME guild validation is blocked until guild filesystem/list-server behavior is ported.");

        var nick = nickname.Trim();
        while (nick.Length > 0 && nick[0] == '*')
            nick = nick[1..];

        if (nick.Length == 0)
            nick = "unknown";

        player.Nickname = nick == player.AccountName ? "*" + nick : nick;
        player.Guild = string.Empty;
    }

    private static string LimitString(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private static int DecodePreciseCoordinate(ushort encoded)
    {
        var value = encoded >> 1;
        return (encoded & 0x0001) != 0 ? -value : value;
    }

    private static uint GetUnsignedInt(Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate update) =>
        update.GUIntValue ?? unchecked((uint)update.GIntValue.GetValueOrDefault());

    private static bool TryGetGaniAttributeIndex(Preagonal.GServer.Protocol.PlayerPropertyId propertyId, out int index)
    {
        var raw = (byte)propertyId;
        index = raw switch
        {
            >= 37 and <= 41 => raw - 37,
            >= 46 and <= 49 => raw - 41,
            >= 54 and <= 74 => raw - 45,
            _ => -1
        };

        return index >= 0;
    }
}

public sealed record RuntimePlayerPropsOptions(
    Preagonal.GServer.Protocol.ClientVersionId ClientVersion = Preagonal.GServer.Protocol.ClientVersionId.Client21,
    int SwordLimit = 3,
    int ShieldLimit = 3,
    RuntimeNicknameUpdatePolicy NicknamePolicy = RuntimeNicknameUpdatePolicy.Blocked)
{
    public static RuntimePlayerPropsOptions Default { get; } = new();
}

public enum RuntimeNicknameUpdatePolicy
{
    Blocked,
    WordFilterAllowedNoGuild
}

public sealed record RuntimeLevelBaddyTimedPacket(ushort RecipientId, byte[] Packet);

public sealed record RuntimeLevelBaddyTimedResult(
    IReadOnlyList<RuntimeLevelBaddyTimedPacket> Packets,
    IReadOnlyList<RuntimeBaddy.BaddyDropPacket> DropPackets);

public sealed class RuntimeLevel
{
    private readonly List<ushort> _playerIds = [];
    private readonly List<RuntimeLevelItem> _items = [];
    private readonly List<RuntimeHorse> _horses = [];
    private readonly List<RuntimeBoardChange> _boardChanges = [];
    private readonly SortedSet<uint> _npcIds = [];
    private readonly Dictionary<byte, RuntimeBaddy> _baddies = [];
    private readonly RuntimeByteIdGenerator _baddyIds = new(1);

    public RuntimeLevel(string levelName)
    {
        LevelName = levelName;
    }

    public string LevelName { get; }
    public bool IsSingleplayer { get; set; }
    public RuntimeMap? Map { get; set; }
    public int MapX { get; private set; }
    public int MapY { get; private set; }
    public IReadOnlyList<ushort> PlayerIds => _playerIds;
    public IReadOnlyList<RuntimeLevelItem> Items => _items;
    public IReadOnlyList<RuntimeHorse> Horses => _horses;
    public IReadOnlyList<uint> NpcIds => _npcIds.ToArray();
    public IReadOnlyCollection<RuntimeBaddy> Baddies => _baddies.Values;
    public bool HasPlayers => _playerIds.Count != 0;

    public int AddPlayer(ushort id)
    {
        _playerIds.Add(id);
        return _playerIds.Count - 1;
    }

    public void RemovePlayer(ushort id)
    {
        _playerIds.RemoveAll(playerId => playerId == id);
    }

    public bool IsPlayerLeader(ushort id) =>
        _playerIds.Count != 0 && _playerIds[0] == id;

    public RuntimeLevelBaddyTimedResult TickBaddyTimeouts(
        int clientVersion = 217,
        int baddyRespawnTime = 60)
    {
        var packets = new List<RuntimeLevelBaddyTimedPacket>();
        var allDrops = new List<RuntimeBaddy.BaddyDropPacket>();
        var setDead = new HashSet<RuntimeBaddy>();
        var playerIdsSnapshot = _playerIds.ToArray();

        foreach (var baddy in _baddies.Values.ToArray())
        {
            allDrops.AddRange(baddy.PopDroppedPackets());

            if (baddy.Timeout.DoTimeout() != 0)
                continue;

            if (baddy.Type == 4 && baddy.Mode == (byte)BaddyMode.Hurt)
            {
                baddy.SetProps(
                    baddy.BuildModeProps((byte)BaddyMode.SwampShot),
                    baddyItemsEnabled: false,
                    baddyRespawnTime: 0,
                    rng: null,
                    out _);

                var modePacket = BuildBaddyModePacket(baddy.Id, (byte)BaddyMode.SwampShot);
                foreach (var recipientId in GetNonLeaderRecipients(playerIdsSnapshot))
                    packets.Add(new RuntimeLevelBaddyTimedPacket(recipientId, modePacket));
                continue;
            }

            if (baddy.Mode == (byte)BaddyMode.Die)
            {
                var modePacket = BuildBaddyModePacket(baddy.Id, (byte)BaddyMode.Dead);
                foreach (var recipientId in GetNonLeaderRecipients(playerIdsSnapshot))
                    packets.Add(new RuntimeLevelBaddyTimedPacket(recipientId, modePacket));

                setDead.Add(baddy);
                continue;
            }

            baddy.Reset(clientVersion);
            var fullProps = EntityRuntimePackets.BaddyProps(baddy, clientVersion);
            foreach (var recipientId in playerIdsSnapshot)
                packets.Add(new RuntimeLevelBaddyTimedPacket(recipientId, fullProps));
        }

        foreach (var baddy in setDead.ToArray())
        {
            var shouldRemove = baddy.SetProps(
                baddy.BuildModeProps((byte)BaddyMode.Dead),
                baddyItemsEnabled: false,
                baddyRespawnTime,
                rng: null,
                out _);

            if (shouldRemove)
                RemoveBaddy(baddy.Id);
            else
                allDrops.AddRange(baddy.PopDroppedPackets());
        }

        return new RuntimeLevelBaddyTimedResult(packets, allDrops);
    }

    private static IEnumerable<ushort> GetNonLeaderRecipients(IReadOnlyList<ushort> playerIds)
    {
        for (var i = 1; i < playerIds.Count; i++)
            yield return playerIds[i];
    }

    private static byte[] BuildBaddyModePacket(byte baddyId, byte mode)
    {
        var props = new GraalBinaryWriter();
        props.WriteGChar((byte)5);
        props.WriteGChar(mode);

        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.BaddyProps);
        writer.WriteGChar(baddyId);
        writer.WriteBytes(props.ToArray());
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    public bool AddItem(float x, float y, LevelItemType itemType)
    {
        _items.Add(new RuntimeLevelItem(x, y, itemType));
        return true;
    }

    public void AddBoardChange(byte[] respawnPayload, int respawnTime)
    {
        _boardChanges.Add(new RuntimeBoardChange(respawnPayload, respawnTime));
    }

    public IReadOnlyList<RuntimeBoardChangePacket> TickBoardChanges()
    {
        var packets = new List<RuntimeBoardChangePacket>();
        foreach (var change in _boardChanges)
        {
            if (change.Tick())
                packets.Add(new RuntimeBoardChangePacket(BoardChangeRuntime.BuildBoardModifyPacket(change.RespawnPayload)));
        }

        return packets;
    }

    public LevelItemType RemoveItem(float x, float y)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.X == x && item.Y == y)
            {
                _items.RemoveAt(i);
                return item.ItemType;
            }
        }

        return LevelItemType.Invalid;
    }

    public bool AddHorse(string image, float x, float y, byte direction, byte bushes)
    {
        _horses.Add(new RuntimeHorse(image, x, y, direction, bushes));
        return true;
    }

    public void RemoveHorse(float x, float y)
    {
        for (var i = 0; i < _horses.Count; i++)
        {
            var horse = _horses[i];
            if (horse.X == x && horse.Y == y)
            {
                _horses.RemoveAt(i);
                return;
            }
        }
    }

    public bool AddNpc(uint npcId) => _npcIds.Add(npcId);

    public void RemoveNpc(uint npcId) => _npcIds.Remove(npcId);

    public RuntimeBaddy? AddBaddy(float x, float y, byte type)
    {
        if (_baddies.Count > 50)
            return null;

        var id = _baddyIds.GetAvailableId();
        var baddy = RuntimeBaddy.Create(id, x, y, type);
        _baddies[id] = baddy;
        return baddy;
    }

    public void RemoveBaddy(byte id)
    {
        if (id is < 1 or > 50)
            return;

        if (_baddies.Remove(id))
            _baddyIds.FreeId(id);
    }

    public void SetMap(RuntimeMap? map, int mapX = 0, int mapY = 0)
    {
        Map = map;
        MapX = map is null ? 0 : mapX;
        MapY = map is null ? 0 : mapY;
    }
}

public enum RuntimeMapType
{
    BigMap = 0,
    Gmap = 1
}

public sealed record RuntimeMap(string Name, RuntimeMapType Type, bool IsGroupMap = false);

public sealed class RuntimeServer
{
    private readonly Dictionary<ushort, RuntimePlayer> _players = [];
    private readonly HashSet<RuntimePlayer> _deletedPlayers = [];
    private readonly RuntimeUShortIdGenerator _playerIds = new(2);
    private const ushort PlayerIdStart = 2;

    public IReadOnlyList<ushort> PlayerIds => _players.Keys.ToArray();
    public IReadOnlyCollection<RuntimePlayer> Players => _players.Values;

    public bool AddPlayer(RuntimePlayer player, ushort id = ushort.MaxValue)
    {
        if (id == ushort.MaxValue)
            id = _playerIds.GetAvailableId();

        player.Id = id;
        _players[id] = player;
        return true;
    }

    public bool DeletePlayer(RuntimePlayer? player)
    {
        if (player is null)
            return true;

        _deletedPlayers.Add(player);
        return true;
    }

    public RuntimePlayer? GetPlayer(ushort id) =>
        _players.GetValueOrDefault(id);

    public void CleanupDeletedPlayers()
    {
        CleanupDeletedPlayers(
            isScriptObjectReferenced: null,
            onScriptObjectReferenced: null,
            onBeforeDelete: null);
    }

    public void CleanupDeletedPlayers(
        Func<RuntimePlayer, bool>? isScriptObjectReferenced,
        Action<RuntimePlayer>? onScriptObjectReferenced = null,
        Action<RuntimePlayer>? onBeforeDelete = null)
    {
        foreach (var player in _deletedPlayers.ToArray())
        {
            if (isScriptObjectReferenced?.Invoke(player) == true)
            {
                onScriptObjectReferenced?.Invoke(player);
                continue;
            }

            onBeforeDelete?.Invoke(player);
            player.LeaveLevel();
            _players.Remove(player.Id);
            _playerIds.FreeId(player.Id);
            _deletedPlayers.Remove(player);
        }
    }

    public void CleanupForShutdown(Action<RuntimePlayer>? cleanupPlayer = null)
    {
        foreach (var player in _players.Values.ToArray())
            cleanupPlayer?.Invoke(player);

        foreach (var player in _players.Values.ToArray())
            player.LeaveLevel();

        _deletedPlayers.Clear();
        foreach (var player in _players.Values.ToArray())
            _deletedPlayers.Add(player);

        CleanupDeletedPlayers();
        _deletedPlayers.Clear();
        _playerIds.ResetAndSetNext(PlayerIdStart);
    }
}

public sealed class RuntimeUShortIdGenerator
{
    private readonly SortedSet<ushort> _freeIds = [];
    private ushort _nextId;

    public RuntimeUShortIdGenerator(ushort startId)
    {
        _nextId = startId;
    }

    public ushort GetAvailableId()
    {
        if (_freeIds.Count != 0)
        {
            var id = _freeIds.Min;
            _freeIds.Remove(id);
            return id;
        }

        var next = _nextId;
        _nextId++;
        return next;
    }

    public void FreeId(ushort id) =>
        _freeIds.Add(id);

    public void ResetAndSetNext(ushort startId)
    {
        _freeIds.Clear();
        _nextId = startId;
    }
}

public sealed class RuntimeByteIdGenerator
{
    private readonly SortedSet<byte> _freeIds = [];
    private byte _nextId;

    public RuntimeByteIdGenerator(byte startId)
    {
        _nextId = startId;
    }

    public byte GetAvailableId()
    {
        if (_freeIds.Count != 0)
        {
            var id = _freeIds.Min;
            _freeIds.Remove(id);
            return id;
        }

        var next = _nextId;
        _nextId++;
        return next;
    }

    public void FreeId(byte id) => _freeIds.Add(id);

    public void ResetAndSetNext(byte startId)
    {
        _freeIds.Clear();
        _nextId = startId;
    }
}

public sealed record LevelEntryVisibilitySelection(
    IReadOnlyList<ushort> BroadcastSelfPropsToPlayerIds,
    IReadOnlyList<ushort> SendOtherPropsFromPlayerIds);

public static class LevelEntryVisibilitySelector
{
    public static LevelEntryVisibilitySelection Select(RuntimeServer server, RuntimePlayer joiningPlayer)
    {
        if (joiningPlayer.Level is not { } level || level.IsSingleplayer)
            return new LevelEntryVisibilitySelection([], []);

        return level.Map is null
            ? SelectWithoutMap(server, joiningPlayer, level)
            : SelectWithMap(server, joiningPlayer, level.Map);
    }

    private static LevelEntryVisibilitySelection SelectWithoutMap(
        RuntimeServer server,
        RuntimePlayer joiningPlayer,
        RuntimeLevel level)
    {
        var broadcasts = new List<ushort>();
        var received = new List<ushort>();

        foreach (var playerId in level.PlayerIds)
        {
            if (playerId == joiningPlayer.Id)
                continue;

            var other = server.GetPlayer(playerId);
            if (other is null)
                continue;

            if (other.IsClient)
                broadcasts.Add(playerId);

            received.Add(playerId);
        }

        return new LevelEntryVisibilitySelection(broadcasts, received);
    }

    private static LevelEntryVisibilitySelection SelectWithMap(
        RuntimeServer server,
        RuntimePlayer joiningPlayer,
        RuntimeMap map)
    {
        var broadcasts = new List<ushort>();
        var received = new List<ushort>();

        foreach (var other in server.Players)
        {
            if (other.Id == joiningPlayer.Id)
                continue;
            if (!other.IsClient)
                continue;
            if (other.Level?.Map != map)
                continue;
            if (map.IsGroupMap && joiningPlayer.Group != other.Group)
                continue;
            if (Math.Abs(other.MapX - joiningPlayer.MapX) >= 2 ||
                Math.Abs(other.MapY - joiningPlayer.MapY) >= 2)
                continue;

            broadcasts.Add(other.Id);
            received.Add(other.Id);
        }

        return new LevelEntryVisibilitySelection(broadcasts, received);
    }
}

public static class LiveWorldForwardingSelector
{
    public static IReadOnlyList<ushort> SelectOneLevelRecipients(
        RuntimeServer server,
        RuntimeLevel level,
        IReadOnlySet<ushort>? exclude = null)
    {
        var recipients = new List<ushort>();
        foreach (var playerId in level.PlayerIds)
        {
            if (exclude?.Contains(playerId) == true)
                continue;

            var other = server.GetPlayer(playerId);
            if (other is { IsClient: true })
                recipients.Add(playerId);
        }

        return recipients;
    }

    public static IReadOnlyList<ushort> SelectLevelAreaRecipients(
        RuntimeServer server,
        RuntimePlayer sender,
        IReadOnlySet<ushort>? exclude = null)
    {
        if (sender.Level is not { } level)
            return [];

        return level.Map is null
            ? SelectOneLevelRecipients(server, level, exclude)
            : SelectWithMap(server, sender, level.Map, exclude);
    }

    private static IReadOnlyList<ushort> SelectWithMap(
        RuntimeServer server,
        RuntimePlayer sender,
        RuntimeMap map,
        IReadOnlySet<ushort>? exclude)
    {
        var recipients = new List<ushort>();
        foreach (var other in server.Players)
        {
            if (exclude?.Contains(other.Id) == true)
                continue;
            if (!other.IsClient)
                continue;
            if (other.Level?.Map != map)
                continue;
            if (map.IsGroupMap && sender.Group != other.Group)
                continue;
            if (Math.Abs(other.MapX - sender.MapX) >= 2 ||
                Math.Abs(other.MapY - sender.MapY) >= 2)
                continue;

            recipients.Add(other.Id);
        }

        return recipients;
    }
}
