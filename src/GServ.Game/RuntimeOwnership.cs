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
    public int MapX { get; set; }
    public int MapY { get; set; }
    public int PixelX { get; internal set; }
    public int PixelY { get; internal set; }
    public int PixelZ { get; internal set; }
    public byte Sprite { get; internal set; }
    public string CurrentLevelName { get; internal set; } = string.Empty;
    public string Gani { get; internal set; } = string.Empty;
    public bool MovementUpdated { get; internal set; }
    public bool TouchTestRequested { get; internal set; }

    public bool IsClient => Kind == RuntimePlayerKind.Client;

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
        IEnumerable<GServ.Protocol.IncomingPlayerPropertyUpdate> updates)
    {
        foreach (var update in updates)
        {
            switch (update.PropertyId)
            {
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

                case GServ.Protocol.PlayerPropertyId.CurrentLevel:
                    player.CurrentLevelName = update.StringValue ?? string.Empty;
                    break;

                case GServ.Protocol.PlayerPropertyId.Gani:
                    player.Gani = update.StringValue ?? string.Empty;
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

                default:
                    throw new NotSupportedException($"Runtime mutation for player prop {(byte)update.PropertyId} is not source-confirmed.");
            }
        }
    }

    private static void MarkMovement(RuntimePlayer player)
    {
        player.MovementUpdated = true;
        player.TouchTestRequested = true;
    }

    private static int DecodePreciseCoordinate(ushort encoded)
    {
        var value = encoded >> 1;
        return (encoded & 0x0001) != 0 ? -value : value;
    }
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
    public static IReadOnlyList<ushort> SelectLevelAreaRecipients(
        RuntimeServer server,
        RuntimePlayer sender,
        IReadOnlySet<ushort>? exclude = null)
    {
        if (sender.Level is not { } level)
            return [];

        return level.Map is null
            ? SelectWithoutMap(server, level, exclude)
            : SelectWithMap(server, sender, level.Map, exclude);
    }

    private static IReadOnlyList<ushort> SelectWithoutMap(
        RuntimeServer server,
        RuntimeLevel level,
        IReadOnlySet<ushort>? exclude)
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
