using Preagonal.GServer.Game;
using Preagonal.GServer.Persistence;
using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed record ClientLoginAuthResult(
    bool Accepted,
    SessionLifecycle Lifecycle,
    byte[] OutboundBytes,
    string Diagnostic = "");

public sealed record ServerListLoginResponseResult(
    ServerListAuthResponseStatus Status,
    ushort PlayerId,
    PlayerSessionType Type,
    byte[] OutboundBytes,
    IReadOnlyList<ClientSessionOutbound> Broadcasts);

public sealed record ClientSessionOutbound(ushort PlayerId, byte[] OutboundBytes);

public sealed record ClientFrameBridgeResult(
    bool ContinueSession,
    byte[] OutboundBytes,
    IReadOnlyList<ClientSessionOutbound> Broadcasts,
    string Diagnostic = "");

public sealed record ClientSessionEndResult(
    AccountSaveResult? SaveResult,
    IReadOnlyList<ClientSessionOutbound> Broadcasts);

public sealed record ListServerInfoResult(ushort PlayerId, byte[] OutboundBytes, string Diagnostic = "");

public sealed class LoginAuthBridge(
    IServerListGateway serverList,
    PreWorldAuthOptions options,
    LoginWorldEntryOptions? worldEntryOptions = null,
    RuntimeServer? runtimeServer = null)
{
    private readonly Dictionary<(ushort PlayerId, PlayerSessionType Type), ClientSessionSkeleton> _pendingSessions = [];
    private readonly Dictionary<(ushort PlayerId, PlayerSessionType Type), string> _remoteAddresses = [];
    private readonly Dictionary<ushort, ClientSessionSkeleton> _activeSessions = [];
    private readonly Dictionary<ushort, PostLoginPlayerSnapshot> _activeSnapshots = [];
    private readonly Dictionary<ushort, AccountFileData> _activeAccounts = [];
    private readonly Dictionary<ushort, RuntimePlayer> _activePlayers = [];
    private readonly Dictionary<ushort, InboundPacketDecoder> _activeDecoders = [];
    private readonly Dictionary<ushort, ClientPacketStreamFramer> _activeFramers = [];
    private readonly Dictionary<ushort, GraalFileQueue> _outboundQueues = [];
    private readonly Dictionary<string, RuntimeLevel> _levels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, string> _loginFrameDebug = [];
    private readonly Random _rng = new();

    public ClientFrameBridgeResult HandleClientFrame(
        ClientSocketSessionContext context,
        ReadOnlySpan<byte> frame)
    {
        if (!_activeSessions.ContainsKey(context.PlayerId))
        {
            var login = BeginClientLogin(context, frame);
            return new ClientFrameBridgeResult(
                login.Accepted,
                login.OutboundBytes,
                [],
                login.Diagnostic);
        }

        return HandleActiveClientFrame(context, frame);
    }

    public ClientLoginAuthResult BeginClientLogin(
        ClientSocketSessionContext context,
        ReadOnlySpan<byte> loginFrame)
    {
        if (HasPendingSession(context.PlayerId))
            return new ClientLoginAuthResult(
                Accepted: true,
                Lifecycle: SessionLifecycle.WaitingForServerListAuth,
                OutboundBytes: [],
                Diagnostic: $"login frame ignored while auth pending; {BuildFrameDebug(loginFrame)}");

        var session = new ClientSessionSkeleton(context.PlayerId);
        _loginFrameDebug[context.PlayerId] = BuildFrameDebug(loginFrame);
        if (!session.ReceiveLoginPacket(loginFrame))
            return Finish(session, accepted: false);

        var auth = new ServerListAuthBoundary(serverList, options);
        var result = auth.Begin(session);
        if (!result.Accepted)
            return Finish(session, accepted: false);

        var key = (session.Id, session.Type);
        _pendingSessions[key] = session;
        _remoteAddresses[key] = context.RemoteAddress;
        return Finish(session, accepted: true);
    }

    public ServerListLoginResponseResult HandleVerifyAccount2(ReadOnlySpan<byte> payloadWithoutPacketId)
    {
        var handler = new ServerListAuthResponseHandler(FindSession);
        var result = handler.HandleVerifyAccount2(payloadWithoutPacketId);
        var response = result.Response;
        var key = (response.PlayerId, response.Type);
        var session = FindSession(response.PlayerId, response.Type);
        var broadcasts = Array.Empty<ClientSessionOutbound>();
        if (result.Status == ServerListAuthResponseStatus.AcceptedPreWorld &&
            session is not null &&
            worldEntryOptions is not null &&
            LoginWorldEntry.Complete(session, worldEntryOptions with
            {
                AccountLoginOptions = worldEntryOptions.AccountLoginOptions with
                {
                    RemoteIp = _remoteAddresses.GetValueOrDefault(key, worldEntryOptions.AccountLoginOptions.RemoteIp)
                }
            }, out var playerAdd, out var snapshot))
        {
            broadcasts = ExchangeLoginPlayerProps(session, snapshot).ToArray();
            _activeSessions[session.Id] = session;
            _activeSnapshots[session.Id] = snapshot;
            if (snapshot.Account is not null)
                _activeAccounts[session.Id] = snapshot.Account;
            ActivateRuntimePlayer(session, snapshot);
            serverList.SendPlayerAdd(playerAdd);
        }

        var outbound = session is null ? [] : FlushOutboundBytes(session);

        if (result.Status != ServerListAuthResponseStatus.AcceptedPreWorld)
        {
            _pendingSessions.Remove(key);
            _remoteAddresses.Remove(key);
        }

        return new ServerListLoginResponseResult(
            result.Status,
            response.PlayerId,
            response.Type,
            outbound,
            broadcasts);
    }

    public ClientSessionEndResult EndClientSession(ushort playerId)
    {
        RemovePendingSession(playerId);
        _activeSessions.Remove(playerId);
        _activeSnapshots.Remove(playerId);
        _activeDecoders.Remove(playerId);
        _activeFramers.Remove(playerId);
        _outboundQueues.Remove(playerId);

        AccountSaveResult? saveResult = null;
        if (_activePlayers.Remove(playerId, out var player))
        {
            var broadcasts = BuildDisconnectBroadcasts(player).ToArray();
            if (serverList.IsConnected)
                serverList.SendPlayerRemove(ServerListAuthPackets.PlayerRemove(playerId));

            if (_activeAccounts.Remove(playerId, out var account) && worldEntryOptions is not null)
            {
                CopyRuntimeToAccount(player, account);
                saveResult = AccountSaveService.Save(account, worldEntryOptions.AccountFileSystem);
            }

            runtimeServer?.DeletePlayer(player);
            return new ClientSessionEndResult(saveResult, broadcasts);
        }

        _activeAccounts.Remove(playerId);
        return new ClientSessionEndResult(null, []);
    }

    public ListServerInfoResult HandleServerInfo(ReadOnlySpan<byte> payloadWithoutPacketId)
    {
        var reader = new GraalBinaryReader(payloadWithoutPacketId);
        var playerId = reader.ReadGShort();
        var serverPacket = reader.ReadBytes(reader.BytesLeft);
        if (!_activeSessions.TryGetValue(playerId, out var session))
            return new ListServerInfoResult(playerId, [], "serverwarp target session is not active");

        if (session.LoginPacket?.VersionId < ClientVersionId.Client21)
            return new ListServerInfoResult(playerId, [], "serverwarp reply ignored for pre-2.1 client");

        session.QueuePacket(BuildServerWarpPacket(serverPacket));
        return new ListServerInfoResult(playerId, FlushOutboundBytes(session));
    }

    public IReadOnlyList<ClientSessionOutbound> TickLevelTimedEvents()
    {
        var touched = new HashSet<ushort>();
        foreach (var level in _levels.Values)
        {
            foreach (var packet in level.TickBoardChanges())
                QueueOneLevelPacket(level, packet.Packet, touched, exclude: null);
        }

        return FlushTouchedBroadcasts(touched);
    }

    private ClientSessionSkeleton? FindSession(ushort id, PlayerSessionType type) =>
        _pendingSessions.TryGetValue((id, type), out var session) ? session : null;

    private bool HasPendingSession(ushort id) =>
        _pendingSessions.Keys.Any(key => key.PlayerId == id);

    private void RemovePendingSession(ushort playerId)
    {
        foreach (var key in _pendingSessions.Keys.Where(key => key.PlayerId == playerId).ToArray())
            _pendingSessions.Remove(key);

        foreach (var key in _remoteAddresses.Keys.Where(key => key.PlayerId == playerId).ToArray())
            _remoteAddresses.Remove(key);
    }

    private ClientFrameBridgeResult HandleActiveClientFrame(
        ClientSocketSessionContext context,
        ReadOnlySpan<byte> frame)
    {
        if (runtimeServer is null ||
            !_activeSessions.TryGetValue(context.PlayerId, out var session) ||
            !_activePlayers.TryGetValue(context.PlayerId, out var player) ||
            !_activeDecoders.TryGetValue(context.PlayerId, out var decoder) ||
            !_activeFramers.TryGetValue(context.PlayerId, out var framer))
        {
            return new ClientFrameBridgeResult(true, [], [], "active session missing runtime state");
        }

        var decoded = decoder.DecodeSocketFrame(frame);
        var touched = new HashSet<ushort>();
        var packetNames = new List<string>();
        foreach (var packet in framer.Parse(decoded.DecodedPayload))
        {
            var reader = new GraalBinaryReader(packet.Payload.Span);
            var packetId = reader.ReadGChar();
            packetNames.Add(((PlayerToServerPacketId)packetId).ToString());
            if (packetId == (byte)PlayerToServerPacketId.ServerWarp)
            {
                var serverName = System.Text.Encoding.ASCII.GetString(packet.Payload.Span[1..]).TrimEnd('\n', '\r');
                serverList.SendServerInfoForPlayer(ServerListAuthPackets.ServerInfoForPlayer(context.PlayerId, serverName));
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.BoardModify)
            {
                HandleBoardModify(packet.Payload.Span[1..], player, touched);
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.ItemAdd)
            {
                HandleItemAdd(reader, player, touched);
                continue;
            }

            if (packetId is (byte)PlayerToServerPacketId.ItemDelete or (byte)PlayerToServerPacketId.ItemTake)
            {
                HandleItemDelete(reader, player, session, takeItem: packetId == (byte)PlayerToServerPacketId.ItemTake, touched);
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.OpenChest)
            {
                HandleOpenChest(reader, context.PlayerId, player, session, touched);
                continue;
            }

            if (packetId != (byte)PlayerToServerPacketId.PlayerProps)
                continue;

            var parsed = IncomingPlayerPropsParser.Parse(packet.Payload.Span[1..], session.LoginPacket?.VersionId ?? ClientVersionId.Client21);
            if (!parsed.Success)
                continue;

            var result = LiveWorldSessionForwarder.TryApplyAndForwardConfirmedPlayerProps(
                runtimeServer,
                player,
                parsed.Updates,
                senderSupportsPreciseMovement: session.LoginPacket?.VersionId >= ClientVersionId.Client21,
                BuildSinks());

            if (result.Status == LiveWorldPlayerPropsForwardingStatus.Blocked)
                return new ClientFrameBridgeResult(false, [], [], result.Message);

            foreach (var delivery in result.Deliveries)
                touched.Add(delivery.PlayerId);
        }

        var outbound = new List<byte>();
        var broadcasts = new List<ClientSessionOutbound>();
        foreach (var playerId in touched)
        {
            if (!_activeSessions.TryGetValue(playerId, out var touchedSession))
                continue;

            var bytes = FlushOutboundBytes(touchedSession);
            if (bytes.Length == 0)
                continue;

            if (playerId == context.PlayerId)
                outbound.AddRange(bytes);
            else
                broadcasts.Add(new ClientSessionOutbound(playerId, bytes));
        }

        var warning = decoded.Warnings.Count == 0 ? "" : string.Join("; ", decoded.Warnings);
        var packetTrace = packetNames.Count == 0 ? "" : $"active packets={string.Join(",", packetNames)}";
        var diagnostic = string.IsNullOrEmpty(warning) ? packetTrace : string.Join("; ", packetTrace, warning);
        return new ClientFrameBridgeResult(true, outbound.ToArray(), broadcasts, diagnostic);
    }

    private void HandleBoardModify(ReadOnlySpan<byte> payload, RuntimePlayer player, ISet<ushort> touched)
    {
        if (runtimeServer is null || player.Level is not { } level || payload.Length < 4)
            return;

        QueueOneLevelPacket(level, BoardChangeRuntime.BuildBoardModifyPacket(payload), touched, exclude: null);

        var reader = new GraalBinaryReader(payload);
        var x = reader.ReadGChar();
        var y = reader.ReadGChar();
        var width = reader.ReadGChar();
        var height = reader.ReadGChar();
        if (x > 63 || y > 63 || width == 0 || height == 0 || worldEntryOptions is null)
            return;

        var loaded = worldEntryOptions.LevelLoader.TryLoad(level.LevelName);
        if (!loaded.Success)
            return;

        var oldTile = loaded.Level.GetTile(0, x, y);
        if (BoardChangeRuntime.ShouldRespawn(oldTile))
            level.AddBoardChange(
                BoardChangeRuntime.BuildOldTilePayload(loaded.Level, x, y, width, height),
                GetIntOption("respawntime", 15));

        var drop = BoardChangeRuntime.RollTileDrop(
            oldTile,
            GetBoolOption("bushitems", true),
            GetBoolOption("vasesdrop", true),
            GetIntOption("tiledroprate", 50),
            _rng);
        if (drop == LevelItemType.Invalid)
            return;

        var state = RuntimePlayerInventoryState.Capture(player);
        var result = LevelItemRuntime.SpawnLevelItem(level, (byte)(x * 2), (byte)(y * 2), (byte)drop, playerDrop: false, state);
        QueueLevelPacket(player, result.ForwardPacket, touched);
    }

    private void HandleItemAdd(GraalBinaryReader reader, RuntimePlayer player, ISet<ushort> touched)
    {
        if (runtimeServer is null || player.Level is not { } level)
            return;

        var encodedX = reader.ReadGChar();
        var encodedY = reader.ReadGChar();
        var itemId = reader.ReadGChar();
        var state = RuntimePlayerInventoryState.Capture(player);
        var result = LevelItemRuntime.SpawnLevelItem(level, encodedX, encodedY, itemId, playerDrop: true, state);
        RuntimePlayerInventoryState.Apply(player, state);
        QueueLevelPacket(player, result.ForwardPacket, touched);
        QueueSelfPacket(player.Id, result.SelfPacket, touched);
    }

    private void HandleItemDelete(
        GraalBinaryReader reader,
        RuntimePlayer player,
        ClientSessionSkeleton session,
        bool takeItem,
        ISet<ushort> touched)
    {
        if (runtimeServer is null || player.Level is not { } level)
            return;

        var encodedX = reader.ReadGChar();
        var encodedY = reader.ReadGChar();
        var state = RuntimePlayerInventoryState.Capture(player);
        var result = LevelItemRuntime.DeleteOrTakeLevelItem(level, encodedX, encodedY, takeItem, state);
        RuntimePlayerInventoryState.Apply(player, state);
        QueueLevelPacket(player, result.ForwardPacket, touched);

        if (!takeItem || result.ItemType == LevelItemType.Invalid || result.PlayerPropsPayload.Length == 0)
            return;

        QueueSelfPacket(player.Id, PlayerPropertySerializer.BuildPlayerPropsPacket(result.PlayerPropsPayload, appendNewline: true), touched);
    }

    private void HandleOpenChest(
        GraalBinaryReader reader,
        ushort playerId,
        RuntimePlayer player,
        ClientSessionSkeleton session,
        ISet<ushort> touched)
    {
        if (worldEntryOptions is null || !_activeAccounts.TryGetValue(playerId, out var account))
            return;

        var x = reader.ReadGChar();
        var y = reader.ReadGChar();
        var levelName = string.IsNullOrWhiteSpace(player.CurrentLevelName)
            ? "onlinestartlocal.nw"
            : player.CurrentLevelName;
        var loaded = worldEntryOptions.LevelLoader.TryLoad(levelName);
        if (!loaded.Success)
            return;

        var opened = new HashSet<string>(account.Chests, StringComparer.Ordinal);
        var result = LevelInteraction.TryOpenChest(loaded.Level, loaded.LevelName, x, y, opened);
        if (!result.Opened)
            return;

        account.Chests.Add(result.ChestKey);
        var state = RuntimePlayerInventoryState.Capture(player);
        var payload = InventoryItemRules.BuildPickupPlayerProps(result.ItemType, state);
        InventoryItemRules.ApplyPickupPlayerProps(payload, state);
        RuntimePlayerInventoryState.Apply(player, state);
        QueueSelfPacket(player.Id, result.Packet, touched);
        if (payload.Length != 0)
            QueueSelfPacket(player.Id, PlayerPropertySerializer.BuildPlayerPropsPacket(payload, appendNewline: true), touched);
    }

    private void QueueLevelPacket(RuntimePlayer player, byte[] packet, ISet<ushort> touched)
    {
        if (runtimeServer is null || packet.Length == 0)
            return;

        var deliveries = LiveWorldSessionForwarder.ForwardConfirmedLevelAreaPacket(
            runtimeServer,
            player,
            packet,
            BuildSinks(),
            new HashSet<ushort> { player.Id });
        foreach (var delivery in deliveries)
            touched.Add(delivery.PlayerId);
    }

    private void QueueOneLevelPacket(RuntimeLevel level, byte[] packet, ISet<ushort> touched, IReadOnlySet<ushort>? exclude)
    {
        if (runtimeServer is null || packet.Length == 0)
            return;

        var deliveries = LiveWorldSessionForwarder.ForwardConfirmedOneLevelPacket(
            runtimeServer,
            level,
            packet,
            BuildSinks(),
            exclude);
        foreach (var delivery in deliveries)
            touched.Add(delivery.PlayerId);
    }

    private void QueueSelfPacket(ushort playerId, byte[] packet, ISet<ushort> touched)
    {
        if (packet.Length == 0 || !_activeSessions.TryGetValue(playerId, out var session))
            return;

        session.QueuePacket(packet);
        touched.Add(playerId);
    }

    private bool GetBoolOption(string key, bool defaultValue)
    {
        if (worldEntryOptions is null)
            return defaultValue;

        var value = worldEntryOptions.AccountSettings.GetString(key, defaultValue ? "true" : "false");
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private int GetIntOption(string key, int defaultValue)
    {
        if (worldEntryOptions is null)
            return defaultValue;

        var value = worldEntryOptions.AccountSettings.GetString(key, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private IReadOnlyList<ClientSessionOutbound> FlushTouchedBroadcasts(IEnumerable<ushort> touched)
    {
        var broadcasts = new List<ClientSessionOutbound>();
        foreach (var playerId in touched)
        {
            if (!_activeSessions.TryGetValue(playerId, out var session))
                continue;

            var bytes = FlushOutboundBytes(session);
            if (bytes.Length != 0)
                broadcasts.Add(new ClientSessionOutbound(playerId, bytes));
        }

        return broadcasts;
    }

    private IReadOnlyDictionary<ushort, ILiveWorldSessionSink> BuildSinks() =>
        _activeSessions.ToDictionary(
            entry => entry.Key,
            entry => (ILiveWorldSessionSink)new ClientSessionSink(entry.Value));

    private void ActivateRuntimePlayer(ClientSessionSkeleton session, PostLoginPlayerSnapshot snapshot)
    {
        if (runtimeServer is null)
            return;

        var kind = session.Type switch
        {
            PlayerSessionType.RemoteControl or PlayerSessionType.RemoteControl2 => RuntimePlayerKind.RemoteControl,
            PlayerSessionType.NpcServer => RuntimePlayerKind.NpcServer,
            PlayerSessionType.NpcControl => RuntimePlayerKind.NpcControl,
            _ => RuntimePlayerKind.Client
        };
        var player = new RuntimePlayer(session.Id, snapshot.LoginPropertySource.AccountName, kind);
        player.InitializeFromLogin(snapshot.LoginPropertySource);
        runtimeServer.AddPlayer(player, session.Id);
        var levelName = string.IsNullOrWhiteSpace(player.CurrentLevelName)
            ? "onlinestartlocal.nw"
            : player.CurrentLevelName;
        player.JoinLevel(GetOrCreateLevel(levelName));
        _activePlayers[session.Id] = player;
        _activeDecoders[session.Id] = new InboundPacketDecoder(session.InboundEncryptionGeneration, session.LoginPacket?.EncryptionKey ?? 0);
        _activeFramers[session.Id] = new ClientPacketStreamFramer(new ClientPacketParseOptions(StripRawDataTrailingNewline: true));
    }

    private RuntimeLevel GetOrCreateLevel(string levelName)
    {
        if (_levels.TryGetValue(levelName, out var level))
            return level;

        level = new RuntimeLevel(levelName);
        _levels[levelName] = level;
        return level;
    }

    private static void CopyRuntimeToAccount(RuntimePlayer player, AccountFileData account)
    {
        account.Nickname = player.Nickname;
        account.CommunityName = player.CommunityName;
        account.LevelName = player.CurrentLevelName;
        account.PixelX = ClampShort(player.PixelX);
        account.PixelY = ClampShort(player.PixelY);
        account.PixelZ = ClampShort(player.PixelZ);
        account.MaxHitpoints = player.MaxPower;
        account.Hitpoints = player.Hitpoints;
        account.Rupees = player.Rupees;
        account.Gani = player.Gani;
        account.Arrows = player.Arrows;
        account.Bombs = player.Bombs;
        account.GlovePower = player.GlovePower;
        account.ShieldPower = player.ShieldPower;
        account.SwordPower = player.SwordPower;
        account.BowPower = player.BowPower;
        account.BowImage = player.BowImage;
        account.HeadImage = player.HeadImage;
        account.BodyImage = player.BodyImage;
        account.SwordImage = player.SwordImage;
        account.ShieldImage = player.ShieldImage;
        account.Sprite = player.Sprite;
        account.Status = player.StatusMessage;
        account.MagicPoints = player.MagicPoints;
        account.Alignment = player.Alignment;
        account.ApCounter = (byte)Math.Min(player.ApCounter, byte.MaxValue);
        account.AccountIp = player.AccountIp;
        account.Language = player.Language;
        account.EloRating = player.EloRating;
        account.EloDeviation = player.EloDeviation;

        for (var i = 0; i < Math.Min(account.Colors.Length, player.Colors.Count); i++)
            account.Colors[i] = player.Colors[i];

        for (var i = 0; i < Math.Min(account.GaniAttributes.Length, player.GaniAttributes.Count); i++)
            account.GaniAttributes[i] = player.GaniAttributes[i];
    }

    private static short ClampShort(int value) =>
        (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private IEnumerable<ClientSessionOutbound> BuildDisconnectBroadcasts(RuntimePlayer player)
    {
        if (player.Kind == RuntimePlayerKind.NpcControl)
            yield break;

        var packet = BuildOtherPlayerDisconnected(player.Id);
        foreach (var (otherId, session) in _activeSessions.ToArray())
        {
            if (otherId == player.Id || !IsClient(session.Type))
                continue;

            session.QueuePacket(packet);
            var outbound = FlushOutboundBytes(session);
            if (outbound.Length != 0)
                yield return new ClientSessionOutbound(otherId, outbound);
        }
    }

    private static byte[] BuildOtherPlayerDisconnected(ushort playerId) =>
        PlayerPropertySerializer.BuildOtherPlayerPropsPacket(
            playerId,
            [(byte)((byte)PlayerPropertyId.PlayerConnected + 32)],
            appendNewline: true);

    private static byte[] BuildServerWarpPacket(ReadOnlySpan<byte> serverPacket)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.ServerWarp);
        writer.WriteBytes(serverPacket);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private IEnumerable<ClientSessionOutbound> ExchangeLoginPlayerProps(
        ClientSessionSkeleton joiningSession,
        PostLoginPlayerSnapshot joiningSnapshot)
    {
        if (!IsClient(joiningSession.Type))
            yield break;

        foreach (var (otherId, otherSnapshot) in _activeSnapshots.ToArray())
        {
            if (otherId == joiningSession.Id || !IsClient(otherSnapshot.Type))
                continue;

            joiningSession.QueuePacket(BuildOtherPlayerProps(otherSnapshot));

            if (!_activeSessions.TryGetValue(otherId, out var otherSession))
                continue;

            otherSession.QueuePacket(BuildOtherPlayerProps(joiningSnapshot));
            var outbound = FlushOutboundBytes(otherSession);
            if (outbound.Length != 0)
                yield return new ClientSessionOutbound(otherId, outbound);
        }
    }

    private static byte[] BuildOtherPlayerProps(PostLoginPlayerSnapshot snapshot)
    {
        var payload = PlayerPropertySerializer.SerializeOtherPlayerPropsPayload(
            snapshot.LoginPropertySource,
            GetLoginPropertySet.All);
        return PlayerPropertySerializer.BuildOtherPlayerPropsPacket(snapshot.PlayerId, payload, appendNewline: true);
    }

    private static bool IsClient(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyClient) != 0;

    private sealed class ClientSessionSink(ClientSessionSkeleton session) : ILiveWorldSessionSink
    {
        public ushort PlayerId => session.Id;

        public void QueuePacket(byte[] packet)
        {
            session.QueuePacket(packet);
        }
    }

    private ClientLoginAuthResult Finish(ClientSessionSkeleton session, bool accepted)
    {
        var outbound = FlushOutboundBytes(session);
        if (!accepted)
            _outboundQueues.Remove(session.Id);

        return new ClientLoginAuthResult(accepted, session.Lifecycle, outbound, BuildDiagnostic(session, accepted));
    }

    private string BuildDiagnostic(ClientSessionSkeleton session, bool accepted)
    {
        if (session.LoginPacket is null)
            return $"login accepted={accepted}; lifecycle={session.Lifecycle}; login packet missing; {_loginFrameDebug.GetValueOrDefault(session.Id, "")}";

        return $"login accepted={accepted}; lifecycle={session.Lifecycle}; type={session.Type}; account={session.LoginPacket.AccountName}; version={session.LoginPacket.VersionToken}; versionId={session.LoginPacket.VersionId}; identity={session.LoginPacket.Identity}; {_loginFrameDebug.GetValueOrDefault(session.Id, "")}; {session.LoginPacket.DebugInfo}";
    }

    private static string BuildFrameDebug(ReadOnlySpan<byte> frame)
    {
        var previewLength = Math.Min(frame.Length, 96);
        return $"frameHex={Convert.ToHexString(frame[..previewLength])}; frameBytes={frame.Length}";
    }

    private byte[] FlushOutboundBytes(ClientSessionSkeleton session)
    {
        var raw = session.TakeOutboundBytes();
        if (raw.Length == 0)
            return [];

        var queue = GetOutboundQueue(session);

        queue.AddPacket(raw);
        return queue.FlushSocket(forceSendFiles: true);
    }

    private GraalFileQueue GetOutboundQueue(ClientSessionSkeleton session)
    {
        if (_outboundQueues.TryGetValue(session.Id, out var queue))
            return queue;

        queue = new GraalFileQueue();
        if (session.LoginPacket?.Type is PlayerSessionType.Client3 or PlayerSessionType.RemoteControl2 &&
            session.LoginPacket.EncryptionKey is { } key)
            queue.SetCodec(EncryptionGeneration.Gen5, key);
        else if (session.LoginPacket?.Type == PlayerSessionType.Web)
            queue.SetCodec(EncryptionGeneration.Gen1, key: 0);

        _outboundQueues[session.Id] = queue;
        return queue;
    }
}
