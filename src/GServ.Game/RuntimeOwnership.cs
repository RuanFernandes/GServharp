namespace GServ.Game;

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
}

public static class RuntimePlayerPropsApplier
{
    public static void ApplyConfirmed(
        RuntimePlayer player,
        IEnumerable<GServ.Protocol.IncomingPlayerPropertyUpdate> updates,
        RuntimePlayerPropsOptions? options = null)
    {
        options ??= RuntimePlayerPropsOptions.Default;
        foreach (var update in updates)
        {
            switch (update.PropertyId)
            {
                case GServ.Protocol.PlayerPropertyId.MaxPower:
                    player.MaxPower = (byte)Math.Clamp(
                        (int)update.GCharValue.GetValueOrDefault(),
                        0,
                        Math.Min((int)player.HeartLimit, 20));
                    player.Hitpoints = player.MaxPower;
                    break;

                case GServ.Protocol.PlayerPropertyId.CurrentPower:
                    var power = update.GCharValue.GetValueOrDefault() / 2.0f;
                    if (player.Alignment >= 40 || power <= player.Hitpoints)
                        player.Hitpoints = Math.Clamp(power, 0.0f, player.MaxPower);
                    break;

                case GServ.Protocol.PlayerPropertyId.RupeesCount:
                    player.Rupees = Math.Clamp(update.GIntValue.GetValueOrDefault(), 0, 9_999_999);
                    break;

                case GServ.Protocol.PlayerPropertyId.X:
                    player.PixelX = update.GCharValue.GetValueOrDefault() * 8;
                    MarkMovement(player);
                    break;

                case GServ.Protocol.PlayerPropertyId.Y:
                    player.PixelY = update.GCharValue.GetValueOrDefault() * 8;
                    MarkMovement(player);
                    break;

                case GServ.Protocol.PlayerPropertyId.Z:
                    player.PixelZ = (update.GCharValue.GetValueOrDefault() - 50) * 8;
                    MarkMovement(player);
                    break;

                case GServ.Protocol.PlayerPropertyId.Sprite:
                    player.Sprite = update.GCharValue.GetValueOrDefault();
                    player.TouchTestRequested = true;
                    break;

                case GServ.Protocol.PlayerPropertyId.ArrowsCount:
                    player.Arrows = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)99);
                    break;

                case GServ.Protocol.PlayerPropertyId.BombsCount:
                    player.Bombs = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)99);
                    break;

                case GServ.Protocol.PlayerPropertyId.GlovePower:
                    player.GlovePower = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)3);
                    break;

                case GServ.Protocol.PlayerPropertyId.BombPower:
                    player.BombPower = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)3);
                    break;

                case GServ.Protocol.PlayerPropertyId.SwordPower:
                    ApplySwordPower(player, update, options);
                    break;

                case GServ.Protocol.PlayerPropertyId.ShieldPower:
                    ApplyShieldPower(player, update, options);
                    break;

                case GServ.Protocol.PlayerPropertyId.ApCounter:
                    player.ApCounter = update.GShortValue.GetValueOrDefault();
                    break;

                case GServ.Protocol.PlayerPropertyId.MagicPoints:
                    player.MagicPoints = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)100);
                    break;

                case GServ.Protocol.PlayerPropertyId.Alignment:
                    player.Alignment = Math.Min(update.GCharValue.GetValueOrDefault(), (byte)100);
                    break;

                case GServ.Protocol.PlayerPropertyId.AdditionalFlags:
                    player.AdditionalFlags = update.GCharValue.GetValueOrDefault();
                    break;

                case GServ.Protocol.PlayerPropertyId.CarrySprite:
                    player.CarrySprite = update.GCharValue.GetValueOrDefault();
                    break;

                case GServ.Protocol.PlayerPropertyId.HorseBushes:
                    player.HorseBombCount = update.GCharValue.GetValueOrDefault();
                    break;

                case GServ.Protocol.PlayerPropertyId.PlayerStatusMessage:
                    player.StatusMessage = update.GCharValue.GetValueOrDefault();
                    break;

                case GServ.Protocol.PlayerPropertyId.UdpPort:
                    player.UdpPort = unchecked((uint)update.GIntValue.GetValueOrDefault());
                    break;

                case GServ.Protocol.PlayerPropertyId.AttachNpc:
                    player.AttachedNpcId = unchecked((uint)update.GIntValue.GetValueOrDefault());
                    break;

                case GServ.Protocol.PlayerPropertyId.CurrentLevel:
                    player.CurrentLevelName = update.StringValue ?? string.Empty;
                    break;

                case GServ.Protocol.PlayerPropertyId.Gani:
                    player.Gani = update.StringValue ?? string.Empty;
                    break;

                case GServ.Protocol.PlayerPropertyId.HeadGif:
                    if (update.StringValue is not null)
                        player.HeadImage = LimitString(update.StringValue, 123);
                    break;

                case GServ.Protocol.PlayerPropertyId.HorseGif:
                    player.HorseImage = update.StringValue ?? string.Empty;
                    break;

                case GServ.Protocol.PlayerPropertyId.CurrentChat:
                    player.ChatMessage = LimitString(update.StringValue ?? string.Empty, 223);
                    break;

                case GServ.Protocol.PlayerPropertyId.BodyImage:
                    player.BodyImage = LimitString(update.StringValue ?? string.Empty, 223);
                    break;

                case GServ.Protocol.PlayerPropertyId.Colors:
                    ApplyColors(player, update.BytesValue ?? []);
                    break;

                case GServ.Protocol.PlayerPropertyId.PlayerLanguage:
                    player.Language = update.StringValue ?? string.Empty;
                    break;

                case GServ.Protocol.PlayerPropertyId.OsType:
                    player.Os = update.StringValue ?? string.Empty;
                    break;

                case GServ.Protocol.PlayerPropertyId.TextCodePage:
                    player.TextCodePage = unchecked((uint)update.GIntValue.GetValueOrDefault());
                    break;

                case GServ.Protocol.PlayerPropertyId.X2:
                    player.PixelX = DecodePreciseCoordinate(update.GShortValue.GetValueOrDefault());
                    MarkMovement(player);
                    break;

                case GServ.Protocol.PlayerPropertyId.Y2:
                    player.PixelY = DecodePreciseCoordinate(update.GShortValue.GetValueOrDefault());
                    MarkMovement(player);
                    break;

                case GServ.Protocol.PlayerPropertyId.Z2:
                    player.PixelZ = DecodePreciseCoordinate(update.GShortValue.GetValueOrDefault());
                    MarkMovement(player);
                    break;

                case GServ.Protocol.PlayerPropertyId.Id:
                case GServ.Protocol.PlayerPropertyId.KillsCount:
                case GServ.Protocol.PlayerPropertyId.DeathsCount:
                case GServ.Protocol.PlayerPropertyId.OnlineSeconds:
                case GServ.Protocol.PlayerPropertyId.IpAddress:
                case GServ.Protocol.PlayerPropertyId.AccountName:
                case GServ.Protocol.PlayerPropertyId.Rating:
                case GServ.Protocol.PlayerPropertyId.JoinLeaveLevel:
                case GServ.Protocol.PlayerPropertyId.PlayerConnected:
                case GServ.Protocol.PlayerPropertyId.Unknown81:
                case GServ.Protocol.PlayerPropertyId.CommunityName:
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
        GServ.Protocol.IncomingPlayerPropertyUpdate update,
        RuntimePlayerPropsOptions options)
    {
        if (update.GCharValue is not { } raw)
            return;

        int power;
        string image;
        if (raw <= 4)
        {
            power = Math.Clamp(raw, 0, options.SwordLimit);
            image = "sword" + power + (options.ClientVersion < GServ.Protocol.ClientVersionId.Client21 ? ".gif" : ".png");
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
        GServ.Protocol.IncomingPlayerPropertyUpdate update,
        RuntimePlayerPropsOptions options)
    {
        if (update.GCharValue is not { } raw)
            return;

        int power;
        string image;
        if (raw <= 3)
        {
            power = Math.Clamp(raw, 0, options.ShieldLimit);
            image = "shield" + power + (options.ClientVersion < GServ.Protocol.ClientVersionId.Client21 ? ".gif" : ".png");
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

    private static string LimitString(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private static int DecodePreciseCoordinate(ushort encoded)
    {
        var value = encoded >> 1;
        return (encoded & 0x0001) != 0 ? -value : value;
    }

    private static bool TryGetGaniAttributeIndex(GServ.Protocol.PlayerPropertyId propertyId, out int index)
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
    GServ.Protocol.ClientVersionId ClientVersion = GServ.Protocol.ClientVersionId.Client21,
    int SwordLimit = 3,
    int ShieldLimit = 3)
{
    public static RuntimePlayerPropsOptions Default { get; } = new();
}

public sealed class RuntimeLevel
{
    private readonly List<ushort> _playerIds = [];
    private readonly List<RuntimeLevelItem> _items = [];
    private readonly List<RuntimeHorse> _horses = [];
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

    public bool AddItem(float x, float y, LevelItemType itemType)
    {
        _items.Add(new RuntimeLevelItem(x, y, itemType));
        return true;
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
        foreach (var player in _deletedPlayers)
        {
            player.LeaveLevel();
            _players.Remove(player.Id);
            _playerIds.FreeId(player.Id);
        }

        _deletedPlayers.Clear();
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
