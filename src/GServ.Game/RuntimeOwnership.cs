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

public sealed class RuntimeLevel
{
    private readonly List<ushort> _playerIds = [];

    public RuntimeLevel(string levelName)
    {
        LevelName = levelName;
    }

    public string LevelName { get; }
    public bool IsSingleplayer { get; set; }
    public RuntimeMap? Map { get; init; }
    public IReadOnlyList<ushort> PlayerIds => _playerIds;
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

    public IReadOnlyList<ushort> PlayerIds => _players.Keys.ToArray();
    public IReadOnlyCollection<RuntimePlayer> Players => _players.Values;

    public bool AddPlayer(RuntimePlayer player, ushort id)
    {
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
        }

        _deletedPlayers.Clear();
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
