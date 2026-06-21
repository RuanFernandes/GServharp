using Preagonal.GServer.Admin;
using Preagonal.GServer.Game;
using Preagonal.GServer.Persistence;
using Preagonal.GServer.Protocol;
using Preagonal.GServer.Scripting;

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
    private sealed record NpcServerEndpoint(ushort Id, string Host, int Port);
    private sealed record ScriptCompileFeedback(bool Success, byte[] ClientBytecode);
    private sealed record DatabaseNpc(uint Id, string Name, string Type, string Owner, string LevelName, string X, string Y);

    private readonly Dictionary<(ushort PlayerId, PlayerSessionType Type), ClientSessionSkeleton> _pendingSessions = [];
    private readonly Dictionary<(ushort PlayerId, PlayerSessionType Type), string> _remoteAddresses = [];
    private readonly Dictionary<ushort, ClientSessionSkeleton> _activeSessions = [];
    private readonly Dictionary<ushort, PostLoginPlayerSnapshot> _activeSnapshots = [];
    private readonly Dictionary<ushort, AccountFileData> _activeAccounts = [];
    private readonly Dictionary<ushort, RuntimePlayer> _activePlayers = [];
    private readonly Dictionary<ushort, InboundPacketDecoder> _activeDecoders = [];
    private readonly Dictionary<ushort, ClientPacketStreamFramer> _activeFramers = [];
    private readonly Dictionary<ushort, GraalFileQueue> _outboundQueues = [];
    private readonly Gs2ServerScriptHost _serverScripts = new();
    private readonly Dictionary<uint, DatabaseNpc> _databaseNpcs = [];
    private Gs2Settings? _serverOptionsOverride;
    private Dictionary<string, string>? _serverFlags;
    private readonly Dictionary<string, RuntimeLevel> _levels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, string> _loginFrameDebug = [];
    private readonly Random _rng = new();
    private const int DisconnectRight = 0x00010;
    private const int SetRightsRight = 0x00400;
    private const int BanRight = 0x00800;
    private const int SetCommentsRight = 0x01000;
    private const int AdminMessageRight = 0x00200;
    private const int ModifyStaffAccountRight = 0x04000;
    private const int WarpToRight = 0x00001;
    private const int WarpToPlayerRight = 0x00002;
    private const int UpdateLevelRight = 0x00008;
    private const int SetAttributesRight = 0x00040;
    private const int SetSelfAttributesRight = 0x00080;
    private const int SetServerFlagsRight = 0x08000;
    private const int SetServerOptionsRight = 0x10000;
    private const int SetFolderOptionsRight = 0x20000;
    private const int NpcControlRight = 0x80000;
    private const ushort NpcServerPlayerId = 1;

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
        {
            AdvancePendingDecoder(context.PlayerId, loginFrame);
            return new ClientLoginAuthResult(
                Accepted: true,
                Lifecycle: SessionLifecycle.WaitingForServerListAuth,
                OutboundBytes: [],
                Diagnostic: $"login frame ignored while auth pending; {BuildFrameDebug(loginFrame)}");
        }

        var session = new ClientSessionSkeleton(context.PlayerId);
        _loginFrameDebug[context.PlayerId] = BuildFrameDebug(loginFrame);
        if (!session.ReceiveLoginPacket(loginFrame))
            return Finish(session, accepted: false);

        if (session.LoginPacket is not null)
            Console.WriteLine($"Client session {context.PlayerId}: login type={session.Type}; key={session.LoginPacket.EncryptionKey?.ToString() ?? "none"}; version={session.LoginPacket.VersionToken}; versionId={session.LoginPacket.VersionId}; account={session.LoginPacket.AccountName}; login={BuildFrameDebug(loginFrame)}; {session.LoginPacket.DebugInfo}");

        var auth = new ServerListAuthBoundary(serverList, options);
        var result = auth.Begin(session);
        if (!result.Accepted)
            return Finish(session, accepted: false);

        var key = (session.Id, session.Type);
        _pendingSessions[key] = session;
        _remoteAddresses[key] = context.RemoteAddress;
        EnsureDecoder(session);
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
            EnsureNpcServerPlayer() &&
            LoginWorldEntry.Complete(session, worldEntryOptions with
            {
                AccountSettings = EffectiveAccountSettings(),
                AccountLoginOptions = worldEntryOptions.AccountLoginOptions with
                {
                    ActiveSessions = BuildActiveSessions(),
                    RemoteIp = _remoteAddresses.GetValueOrDefault(key, worldEntryOptions.AccountLoginOptions.RemoteIp)
                }
            }, out var playerAdd, out var snapshot, out var duplicateDisconnects))
        {
            var loginBroadcasts = new List<ClientSessionOutbound>();
            loginBroadcasts.AddRange(EndDuplicateSessions(duplicateDisconnects, session.Id));
            loginBroadcasts.AddRange(ExchangeLoginPlayerProps(session, snapshot));
            broadcasts = loginBroadcasts.ToArray();
            _activeSessions[session.Id] = session;
            _activeSnapshots[session.Id] = snapshot;
            if (snapshot.Account is not null)
                _activeAccounts[session.Id] = snapshot.Account;
            ActivateRuntimePlayer(session, snapshot);
            if (IsClient(session.Type))
                serverList.SendPlayerAdd(playerAdd);
            QueueNpcServerAddress(session, snapshot.Account);
            broadcasts = [.. broadcasts, .. BuildControlLoginBroadcasts(session, snapshot)];
            _pendingSessions.Remove(key);
            _remoteAddresses.Remove(key);
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
            if (serverList.IsConnected && player.Kind == RuntimePlayerKind.Client)
                serverList.SendPlayerRemove(ServerListAuthPackets.PlayerRemove(playerId));

            if (player.Kind == RuntimePlayerKind.Client &&
                _activeAccounts.Remove(playerId, out var account) &&
                worldEntryOptions is not null)
            {
                CopyRuntimeToAccount(player, account);
                saveResult = AccountSaveService.Save(account, worldEntryOptions.AccountFileSystem);
            }
            else
            {
                _activeAccounts.Remove(playerId);
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
        var forceEndSessions = new HashSet<ushort>();
        var packetNames = new List<string>();
        var notes = new List<string>
        {
            $"frame={frame.Length}",
            $"comp=0x{(frame.Length == 0 ? 0 : frame[0]):X2}",
            $"decoded={decoded.DecodedPayload.Length}",
            $"hex={HexPreview(decoded.DecodedPayload, 24)}"
        };
        foreach (var packet in framer.Parse(decoded.DecodedPayload))
        {
            var reader = new GraalBinaryReader(packet.Payload.Span);
            var packetId = reader.ReadGChar();
            packetNames.Add(((PlayerToServerPacketId)packetId).ToString());
            if (IsControl(session.Type) &&
                HandleControlPacket((PlayerToServerPacketId)packetId, packet.Payload.Span[1..], session, player, touched, forceEndSessions))
            {
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.ServerWarp)
            {
                var serverName = System.Text.Encoding.ASCII.GetString(packet.Payload.Span[1..]).TrimEnd('\n', '\r');
                serverList.SendServerInfoForPlayer(ServerListAuthPackets.ServerInfoForPlayer(context.PlayerId, serverName));
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.BoardModify)
            {
                HandleBoardModify(packet.Payload.Span[1..], player, session.LoginPacket?.VersionId ?? ClientVersionId.Client21, touched);
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
                notes.Add(HandleOpenChest(reader, context.PlayerId, player, session, touched));
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.ShowImg)
            {
                HandleShowImg(packet.Payload.Span[1..], player, touched);
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.PrivateMessage)
            {
                HandlePrivateMessage(reader, player, touched);
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.WeaponAdd)
            {
                notes.Add(HandleWeaponAdd(reader, context.PlayerId));
                continue;
            }

            if (packetId == (byte)PlayerToServerPacketId.NpcWeaponDelete)
            {
                notes.Add(HandleNpcWeaponDelete(reader, context.PlayerId));
                continue;
            }

            if (packetId != (byte)PlayerToServerPacketId.PlayerProps)
                continue;

            var parsed = IncomingPlayerPropsParser.Parse(packet.Payload.Span[1..], session.LoginPacket?.VersionId ?? ClientVersionId.Client21);
            if (!parsed.Success)
            {
                notes.Add($"props=unsupported:{parsed.UnsupportedPropertyId}");
                continue;
            }

            var chatCommand = parsed.Updates.FirstOrDefault(static update => update.PropertyId == PlayerPropertyId.CurrentChat);
            if (chatCommand?.StringValue is { } chatMessage &&
                TryHandleClientChatCommand(player, chatMessage, touched))
            {
                notes.Add($"chat-command={chatMessage}");
                continue;
            }

            var result = LiveWorldSessionForwarder.TryApplyAndForwardConfirmedPlayerProps(
                runtimeServer,
                player,
                parsed.Updates,
                senderSupportsPreciseMovement: session.LoginPacket?.VersionId >= ClientVersionId.Client23,
                BuildSinks(),
                new RuntimePlayerPropsOptions(
                    session.LoginPacket?.VersionId ?? ClientVersionId.Client21,
                    NicknamePolicy: RuntimeNicknameUpdatePolicy.WordFilterAllowedNoGuild));

            if (result.Status == LiveWorldPlayerPropsForwardingStatus.Blocked)
                return new ClientFrameBridgeResult(false, [], [], result.Message);

            notes.Add($"props={parsed.Updates.Count}:deliveries={result.Deliveries.Count}");
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

        foreach (var playerId in forceEndSessions)
        {
            var end = EndClientSession(playerId);
            broadcasts.AddRange(end.Broadcasts);
        }

        var warning = decoded.Warnings.Count == 0 ? "" : string.Join("; ", decoded.Warnings);
        var packetTrace = packetNames.Count == 0 ? "" : $"active packets={string.Join(",", packetNames)}";
        var noteTrace = string.Join("; ", notes.Where(note => !string.IsNullOrWhiteSpace(note)));
        var diagnostic = string.Join("; ", new[] { packetTrace, noteTrace, warning }.Where(part => !string.IsNullOrEmpty(part)));
        return new ClientFrameBridgeResult(true, outbound.ToArray(), broadcasts, diagnostic);
    }

    private void HandleBoardModify(
        ReadOnlySpan<byte> payload,
        RuntimePlayer player,
        ClientVersionId clientVersion,
        ISet<ushort> touched)
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
        if (clientVersion <= ClientVersionId.Client512)
            QueueSelfPacket(player.Id, result.ForwardPacket, touched);
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

    private string HandleOpenChest(
        GraalBinaryReader reader,
        ushort playerId,
        RuntimePlayer player,
        ClientSessionSkeleton session,
        ISet<ushort> touched)
    {
        if (worldEntryOptions is null || !_activeAccounts.TryGetValue(playerId, out var account))
            return "chest=missing-account";

        var x = reader.ReadGChar();
        var y = reader.ReadGChar();
        var levelName = string.IsNullOrWhiteSpace(player.CurrentLevelName)
            ? "onlinestartlocal.nw"
            : player.CurrentLevelName;
        var loaded = worldEntryOptions.LevelLoader.TryLoad(levelName);
        if (!loaded.Success)
            return $"chest={x},{y}:load-failed:{levelName}";

        var opened = new HashSet<string>(account.Chests, StringComparer.Ordinal);
        var result = LevelInteraction.TryOpenChest(loaded.Level, loaded.LevelName, x, y, opened);
        if (!result.Opened)
            return $"chest={x},{y}:not-opened:{loaded.LevelName}:known={loaded.Level.Chests.Count}";

        account.Chests.Add(result.ChestKey);
        var state = RuntimePlayerInventoryState.Capture(player);
        var payload = InventoryItemRules.BuildPickupPlayerProps(result.ItemType, state);
        InventoryItemRules.ApplyPickupPlayerProps(payload, state);
        RuntimePlayerInventoryState.Apply(player, state);
        QueueSelfPacket(player.Id, result.Packet, touched);
        if (payload.Length != 0)
            QueueSelfPacket(player.Id, PlayerPropertySerializer.BuildPlayerPropsPacket(payload, appendNewline: true), touched);
        return $"chest={x},{y}:opened:{result.ItemType}";
    }

    private void HandleShowImg(
        ReadOnlySpan<byte> body,
        RuntimePlayer player,
        ISet<ushort> touched)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)ServerToPlayerPacketId.ShowImg);
        packet.WriteGShort(player.Id);
        packet.WriteBytes(body);
        packet.WriteByte((byte)'\n');
        QueueLevelPacket(player, packet.ToArray(), touched);
    }

    private void HandlePrivateMessage(
        GraalBinaryReader reader,
        RuntimePlayer sender,
        ISet<ushort> touched)
    {
        var targetCount = reader.ReadGShort();
        var targets = new List<ushort>(targetCount);
        for (var i = 0; i < targetCount; i++)
            targets.Add(reader.ReadGShort());

        var message = reader.ReadBytes(reader.BytesLeft);
        var packet = BuildPrivateMessagePacket(sender.Id, targetCount > 1, message);
        foreach (var targetId in targets)
        {
            if (targetId == sender.Id)
                continue;

            if (!_activeSessions.ContainsKey(targetId))
            {
                if (IsNpcServerTarget(targetId))
                    QueueSelfPacket(sender.Id, BuildNpcServerPrivateMessagePacket(targetId), touched);
                continue;
            }

            QueueSelfPacket(targetId, packet, touched);
        }
    }

    private bool TryHandleClientChatCommand(RuntimePlayer player, string chatMessage, ISet<ushort> touched)
    {
        var command = chatMessage.Trim();
        if (command.Length == 0)
            return false;

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        if ((parts[0].Equals("unstick", StringComparison.OrdinalIgnoreCase) ||
             parts[0].Equals("unstuck", StringComparison.OrdinalIgnoreCase)) &&
            parts.Length == 2 &&
            parts[1].Equals("me", StringComparison.OrdinalIgnoreCase))
        {
            if (IsJailedLevel(player.CurrentLevelName))
                return false;

            var level = EffectiveAccountSettings().GetString("unstickmelevel", "onlinestartlocal.nw");
            WarpClient(player, level, GetFloatOption("unstickmex", 30.0f), GetFloatOption("unstickmey", 30.5f), touched);
            QueueSelfPacket(player.Id, PlayerPropertySerializer.BuildPlayerPropsPacket(ChatProp("Warped!"), appendNewline: true), touched);
            return true;
        }

        if (parts[0].Equals("warpto", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 2)
            {
                if (!CanWarp(player.Id, WarpToPlayerRight))
                {
                    QueueSelfPacket(player.Id, PlayerPropertySerializer.BuildPlayerPropsPacket(ChatProp("(not authorized to warp)"), appendNewline: true), touched);
                    return true;
                }

                var target = FindClientByAccountOrNickname(parts[1]);
                if (target is not null && !string.IsNullOrWhiteSpace(target.CurrentLevelName))
                    WarpClient(player, target.CurrentLevelName, target.PixelX / 16.0f, target.PixelY / 16.0f, touched);
                return true;
            }

            if (parts.Length == 3 &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
            {
                if (!CanWarp(player.Id, WarpToRight))
                {
                    QueueSelfPacket(player.Id, PlayerPropertySerializer.BuildPlayerPropsPacket(ChatProp("(not authorized to warp)"), appendNewline: true), touched);
                    return true;
                }

                MoveClient(player, x, y, touched);
                return true;
            }

            if (parts.Length == 4 &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var levelX) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var levelY))
            {
                if (!CanWarp(player.Id, WarpToRight))
                {
                    QueueSelfPacket(player.Id, PlayerPropertySerializer.BuildPlayerPropsPacket(ChatProp("(not authorized to warp)"), appendNewline: true), touched);
                    return true;
                }

                WarpClient(player, parts[3], levelX, levelY, touched);
                return true;
            }
        }

        if (command.Equals("update level", StringComparison.OrdinalIgnoreCase) && HasRight(player.Id, UpdateLevelRight))
        {
            if (!string.IsNullOrWhiteSpace(player.CurrentLevelName))
                _levels.Remove(player.CurrentLevelName);
            return true;
        }

        return false;
    }

    private bool CanWarp(ushort playerId, int right) =>
        HasRight(playerId, right) || GetBoolOption("warptoforall", defaultValue: false);

    private bool IsJailedLevel(string levelName)
    {
        var jailLevels = EffectiveAccountSettings().GetString("jaillevels", "");
        return jailLevels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(level => level.Equals(levelName, StringComparison.OrdinalIgnoreCase));
    }

    private RuntimePlayer? FindClientByAccountOrNickname(string name) =>
        _activePlayers.Values.FirstOrDefault(player =>
            player.Kind == RuntimePlayerKind.Client &&
            (player.AccountName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
             player.Nickname.TrimStart('*').Equals(name.TrimStart('*'), StringComparison.OrdinalIgnoreCase)));

    private void MoveClient(RuntimePlayer player, float x, float y, ISet<ushort> touched)
    {
        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.X, (byte)(x * 2)),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Y, (byte)(y * 2))
            ]);
        QueueSelfPacket(player.Id, PlayerPropertySerializer.BuildPlayerPropsPacket(PositionProps(x, y), appendNewline: true), touched);
    }

    private void WarpClient(RuntimePlayer player, string level, float x, float y, ISet<ushort> touched)
    {
        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [
                IncomingPlayerPropertyUpdate.String(PlayerPropertyId.CurrentLevel, level),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.X, (byte)(x * 2)),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Y, (byte)(y * 2))
            ]);
        player.JoinLevel(GetOrCreateLevel(level));
        QueueSelfPacket(player.Id, AppendNewline(WarpPackets.BuildPlayerWarp(x, y, level)), touched);
    }

    private static byte[] PositionProps(float x, float y)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerPropertyId.X);
        writer.WriteGChar((byte)(x * 2));
        writer.WriteGChar((byte)PlayerPropertyId.Y);
        writer.WriteGChar((byte)(y * 2));
        return writer.ToArray();
    }

    private static byte[] ChatProp(string message)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerPropertyId.CurrentChat);
        WriteGCharString(writer, message);
        return writer.ToArray();
    }

    private bool HandleControlPacket(
        PlayerToServerPacketId packetId,
        ReadOnlySpan<byte> payload,
        ClientSessionSkeleton session,
        RuntimePlayer sender,
        ISet<ushort> touched,
        ISet<ushort> forceEndSessions)
    {
        switch (packetId)
        {
            case PlayerToServerPacketId.NcWeaponListGet:
                HandleNcWeaponListGet(session.Id, touched);
                return true;
            case PlayerToServerPacketId.NcWeaponGet:
                HandleNcWeaponGet(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.NcWeaponAdd:
                HandleNcWeaponAdd(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.NcWeaponDelete:
                HandleNcWeaponDelete(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.NcClassEdit:
                HandleNcClassEdit(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.NcClassAdd:
                HandleNcClassAdd(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.NcNpcAdd:
                HandleNcNpcAdd(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.NcClassDelete:
                HandleNcClassDelete(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.NcLevelListGet:
                HandleNcLevelListGet(session.Id, touched);
                return true;
            case PlayerToServerPacketId.RcChat:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleRcChat(session.Id, sender.AccountName, ReadAsciiPayload(payload), touched);
                return true;
            case PlayerToServerPacketId.RcAdminMessage:
                if (!IsRemoteControl(session.Type))
                    return false;
                if (!HasRight(session.Id, AdminMessageRight))
                {
                    QueueSelfPacket(session.Id, RcNcPackets.RcChat("Server: You are not authorized to send an admin message."), touched);
                    return true;
                }

                BroadcastToAllExcept(
                    session.Id,
                    RcNcPackets.RcAdminMessage($"Admin {sender.AccountName}:\u00a7{ReadAsciiPayload(payload)}"),
                    touched);
                return true;
            case PlayerToServerPacketId.RcPrivateAdminMessage:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePrivateAdminMessage(payload, session.Id, sender.AccountName, touched);
                return true;
            case PlayerToServerPacketId.RcServerOptionsGet:
                if (!IsRemoteControl(session.Type))
                    return false;
                QueueSelfPacket(session.Id, RcNcPackets.ServerOptionsGet(ReadConfigFile("serveroptions.txt")), touched);
                return true;
            case PlayerToServerPacketId.RcServerOptionsSet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleServerOptionsSet(session.Id, sender.AccountName, payload, touched);
                return true;
            case PlayerToServerPacketId.RcFolderConfigGet:
                if (!IsRemoteControl(session.Type))
                    return false;
                QueueSelfPacket(session.Id, RcNcPackets.FolderConfigGet(ReadConfigFile("foldersconfig.txt")), touched);
                return true;
            case PlayerToServerPacketId.RcFolderConfigSet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleFolderConfigSet(session.Id, sender.AccountName, payload, touched);
                return true;
            case PlayerToServerPacketId.RcServerFlagsGet:
                if (!IsRemoteControl(session.Type))
                    return false;
                QueueSelfPacket(session.Id, RcNcPackets.ServerFlagsGet(LoadServerFlags()), touched);
                return true;
            case PlayerToServerPacketId.RcServerFlagsSet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleServerFlagsSet(session.Id, sender.AccountName, payload, touched);
                return true;
            case PlayerToServerPacketId.RcAccountListGet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleAccountListGet(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcAccountAdd:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleAccountAdd(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcAccountDelete:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleAccountDelete(session.Id, ReadAsciiPayload(payload), touched);
                return true;
            case PlayerToServerPacketId.RcAccountGet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleAccountGet(session.Id, ReadAsciiPayload(payload), touched);
                return true;
            case PlayerToServerPacketId.RcAccountSet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleAccountSet(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcPlayerPropsGetById:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerPropsGetById(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcPlayerPropsGetByAccount:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerPropsGetByAccount(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcPlayerPropsSet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerPropsSetById(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcPlayerPropsSetById:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerPropsSetByAccount(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcPlayerRightsGet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerRightsGet(session.Id, ReadAsciiPayload(payload), touched);
                return true;
            case PlayerToServerPacketId.RcPlayerRightsSet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerRightsSet(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcPlayerCommentsGet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerCommentsGet(session.Id, ReadAsciiPayload(payload), touched);
                return true;
            case PlayerToServerPacketId.RcPlayerCommentsSet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerCommentsSet(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcPlayerBanGet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerBanGet(session.Id, ReadAsciiPayload(payload), touched);
                return true;
            case PlayerToServerPacketId.RcPlayerBanSet:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandlePlayerBanSet(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcDisconnectPlayer:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleDisconnectPlayer(session.Id, payload, sender.AccountName, touched, forceEndSessions);
                return true;
            case PlayerToServerPacketId.RcWarpPlayer:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleWarpPlayer(session.Id, payload, touched);
                return true;
            case PlayerToServerPacketId.RcListRemoteControls:
                if (!IsRemoteControl(session.Type))
                    return false;
                foreach (var snapshot in _activeSnapshots.Values.Where(snapshot =>
                    IsRemoteControl(snapshot.Type) || snapshot.Type == PlayerSessionType.NpcServer))
                    QueueSelfPacket(session.Id, BuildRcAddPlayer(snapshot), touched);
                return true;
            case PlayerToServerPacketId.RcFileBrowserStart:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleFileBrowserStart(session.Id, touched);
                return true;
            case PlayerToServerPacketId.RcFileBrowserChangeDirectory:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleFileBrowserChangeDirectory(session.Id, ReadAsciiPayload(payload), touched);
                return true;
            case PlayerToServerPacketId.RcFileBrowserEnd:
                if (!IsRemoteControl(session.Type))
                    return false;
                return true;
            case PlayerToServerPacketId.RcFileBrowserDownload:
                if (!IsRemoteControl(session.Type))
                    return false;
                HandleFileBrowserDownload(session.Id, ReadAsciiPayload(payload), touched);
                return true;
            default:
                return false;
        }
    }

    private void HandleRcChat(ushort playerId, string accountName, string message, ISet<ushort> touched)
    {
        if (message.Length == 0)
            return;

        if (!message.StartsWith("/", StringComparison.Ordinal))
        {
            BroadcastToRemoteControls(RcNcPackets.RcChat($"{accountName}: {message}"), touched);
            return;
        }

        var split = message.IndexOf(' ');
        var command = (split < 0 ? message : message[..split]).ToLowerInvariant();
        var argument = split < 0 ? "" : message[(split + 1)..].Trim();
        var targetAccount = argument.Length == 0 ? accountName : argument;

        switch (command)
        {
            case "/help":
                foreach (var line in ReadConfigFile("rchelp.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    QueueSelfPacket(playerId, RcNcPackets.RcChat(line.TrimEnd('\r')), touched);
                return;
            case "/version":
                QueueSelfPacket(playerId, RcNcPackets.RcChat("Preagonal.GServer-Sharp"), touched);
                return;
            case "/credits":
                QueueSelfPacket(playerId, RcNcPackets.RcChat("Programmed by Preagonal contributors"), touched);
                return;
            case "/open":
                HandlePlayerPropsGetByAccount(playerId, GCharPayload(targetAccount), touched);
                return;
            case "/openacc":
                HandleAccountGet(playerId, targetAccount, touched);
                return;
            case "/opencomments":
                HandlePlayerCommentsGet(playerId, targetAccount, touched);
                return;
            case "/openban":
                HandlePlayerBanGet(playerId, targetAccount, touched);
                return;
            case "/openrights":
                HandlePlayerRightsGet(playerId, targetAccount, touched);
                return;
            case "/reset" when argument.Length != 0:
                QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: Account reset is not implemented yet."), touched);
                return;
            default:
                QueueSelfPacket(playerId, RcNcPackets.RcChat($"Server: Unknown command: {command}"), touched);
                return;
        }
    }

    private void HandleNcWeaponListGet(ushort playerId, ISet<ushort> touched)
    {
        QueueSelfPacket(playerId, RcNcPackets.NcWeaponList(ListWeaponNames()), touched);
    }

    private void HandleNcWeaponGet(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var weaponName = ReadAsciiPayload(payload);
        if (TryLoadWeapon(weaponName, out var weapon))
        {
            QueueSelfPacket(playerId, RcNcPackets.NcWeaponGet(weapon.Name, weapon.Image, weapon.Source), touched);
            return;
        }

        BroadcastToControlClients(RcNcPackets.RcChat($"{GetAccountName(playerId)} prob: weapon {weaponName} doesn't exist"), touched);
    }

    private void HandleNcWeaponAdd(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var reader = new GraalBinaryReader(payload);
        var weaponName = ReadGCharString(reader);
        var imageName = ReadGCharString(reader);
        var source = ReadLatin1Payload(reader.ReadBytes(reader.BytesLeft)).Replace('\u00a7', '\n');
        if (weaponName.Length == 0 || IsDefaultWeaponName(weaponName))
            return;

        var existed = TryLoadWeapon(weaponName, out _);
        var compile = CompileScriptForNc(playerId, "weapon", weaponName, source, touched);
        if (!compile.Success)
            return;

        SaveWeapon(new NcWeaponSource(weaponName, imageName, source));
        RefreshWeaponForClients(weaponName, imageName, source, compile.ClientBytecode, touched);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"Weapon/GUI-script {weaponName} {(existed ? "updated" : "added")} by {GetAccountName(playerId)}"), touched);
    }

    private void HandleNcWeaponDelete(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var weaponName = ReadAsciiPayload(payload);
        var path = ResolveServerFile("weapons", WeaponFileName(weaponName));
        if (path.Length != 0 && File.Exists(path))
        {
            File.Delete(path);
            BroadcastToControlClients(RcNcPackets.RcChat($"Weapon {weaponName} deleted by {GetAccountName(playerId)}"), touched);
        }
        else
        {
            BroadcastToControlClients(RcNcPackets.RcChat($"{GetAccountName(playerId)} prob: weapon {weaponName} doesn't exist"), touched);
        }
    }

    private void HandleNcClassEdit(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var className = ReadAsciiPayload(payload);
        var path = ResolveServerFile("classes", className + ".txt");
        if (path.Length != 0 && File.Exists(path))
            QueueSelfPacket(playerId, RcNcPackets.NcClassGet(className, File.ReadAllText(path)), touched);
    }

    private void HandleNcClassAdd(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var reader = new GraalBinaryReader(payload);
        var className = ReadGCharString(reader);
        var source = GUntokenize(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(reader.BytesLeft)));
        if (className.Length == 0)
            return;

        var path = ResolveServerFile("classes", className + ".txt", createDirectory: true);
        var existed = File.Exists(path);
        var compile = CompileScriptForNc(playerId, "class", className, source, touched);
        if (!compile.Success)
            return;

        File.WriteAllText(path, source.Replace("\r", "", StringComparison.Ordinal));
        SendClientScriptBytecode(compile.ClientBytecode, touched);
        if (!existed)
            BroadcastToNpcControls(RcNcPackets.NcClassAdd(className), touched);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"Script {className} {(existed ? "updated" : "added")} by {GetAccountName(playerId)}"), touched);
    }

    private void HandleNcClassDelete(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var className = ReadAsciiPayload(payload);
        var path = ResolveServerFile("classes", className + ".txt");
        if (path.Length != 0 && File.Exists(path))
        {
            File.Delete(path);
            BroadcastToNpcControls(RcNcPackets.NcClassDelete(className), touched);
            BroadcastToControlClients(RcNcPackets.RcChat($"{GetAccountName(playerId)} has deleted class {className}"), touched);
        }
        else
        {
            BroadcastToControlClients(RcNcPackets.RcChat($"error: {className} does not exist on this server!"), touched);
        }
    }

    private void HandleNcNpcAdd(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var parts = GUntokenize(ReadAsciiPayload(payload))
            .Split('\n', StringSplitOptions.None)
            .Select(static part => part.Trim())
            .ToArray();
        if (parts.Length < 7 || parts[0].Length == 0)
            return;

        var id = uint.TryParse(parts[1], out var parsedId) && parsedId != 0
            ? parsedId
            : NextDatabaseNpcId();
        var npc = new DatabaseNpc(id, parts[0], parts[2], parts[3], parts[4], parts[5], parts[6]);
        _databaseNpcs[id] = npc;
        SaveDatabaseNpc(npc);
        BroadcastToNpcControls(RcNcPackets.NcNpcAdd(npc.Id, npc.Name, npc.Type, npc.LevelName), touched);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"NPC {npc.Name} updated by {GetAccountName(playerId)}"), touched);
    }

    private void SendDatabaseNpcList(ushort playerId, ISet<ushort> touched)
    {
        HydrateDatabaseNpcs();
        foreach (var npc in _databaseNpcs.Values.OrderBy(static npc => npc.Id))
            QueueSelfPacket(playerId, RcNcPackets.NcNpcAdd(npc.Id, npc.Name, npc.Type, npc.LevelName), touched);
    }

    private void HandleNcLevelListGet(ushort playerId, ISet<ushort> touched)
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        if (root is null)
            return;

        var world = Path.Combine(root, "world");
        var levels = Directory.Exists(world)
            ? Directory.EnumerateFiles(world, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".nw", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".graal", StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetRelativePath(world, path).Replace('\\', '/'))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        QueueSelfPacket(playerId, RcNcPackets.NcLevelList(levels), touched);
    }

    private ScriptCompileFeedback CompileScriptForNc(ushort playerId, string type, string name, string source, ISet<ushort> touched)
    {
        var slices = SourceCodeSlices.Parse(source, gs2Default: true, serverSideVm: true);
        try
        {
            var compiler = new Gs2CompilerAdapter();
            var origin = CompilerOrigin(type, name);
            if (!string.IsNullOrWhiteSpace(slices.ServerSide))
            {
                if (!TryPreflightGs2(slices.ServerSide, out var serverPreflightError))
                {
                    SendCompilerOutputToNc($"{origin} server-side", "error", serverPreflightError, touched);
                    return new ScriptCompileFeedback(false, []);
                }

                var serverResult = compiler.Compile(Gs2ServerScriptHost.NormalizeServerSource(slices.ServerSide), type, name);
                if (!serverResult.Success)
                {
                    SendCompilerOutputToNc($"{origin} server-side", "error", serverResult.Error, touched);
                    return new ScriptCompileFeedback(false, []);
                }

                var load = _serverScripts.LoadWeapon(name, serverResult.Bytecode);
                if (!load.Success)
                {
                    SendCompilerOutputToNc($"{origin} server-side", "error", load.Error, touched);
                    return new ScriptCompileFeedback(false, []);
                }

                var run = _serverScripts.Call(name, "onCreated").GetAwaiter().GetResult();
                foreach (var line in run.Output)
                    BroadcastToRemoteControls(RcNcPackets.RcChat(FormatScriptOutput(name, line)), touched);

                if (!run.Success)
                {
                    SendCompilerOutputToNc($"{origin} server-side", "error", run.Error, touched);
                    return new ScriptCompileFeedback(false, []);
                }
            }

            if (string.IsNullOrWhiteSpace(slices.ClientGs2))
                return new ScriptCompileFeedback(true, []);

            if (LooksLikeGs1ClientScript(slices.ClientGs2))
            {
                SendCompilerOutputToNc(origin, "error", "client-side GS1 is not compiled by the GS2 compiler", touched);
                return new ScriptCompileFeedback(false, []);
            }

            if (!TryPreflightGs2(slices.ClientGs2, out var clientPreflightError))
            {
                SendCompilerOutputToNc(origin, "error", clientPreflightError, touched);
                return new ScriptCompileFeedback(false, []);
            }

            var clientResult = compiler.Compile(slices.ClientGs2, type, name);
            if (!clientResult.Success || clientResult.Bytecode.Length == 0)
            {
                SendCompilerOutputToNc(origin, "error", clientResult.Success ? "compiler did not write bytecode" : clientResult.Error, touched);
                return new ScriptCompileFeedback(false, []);
            }

            return new ScriptCompileFeedback(true, clientResult.Bytecode);
        }
        catch (Exception ex)
        {
            SendCompilerOutputToNc(CompilerOrigin(type, name), "error", ex.Message, touched);
            return new ScriptCompileFeedback(false, []);
        }
    }

    private void SendClientScriptBytecode(ReadOnlySpan<byte> bytecode, ISet<ushort> touched)
    {
        if (!bytecode.IsEmpty)
            BroadcastToClients(EntityPackets.NpcWeaponScriptRawData(bytecode), touched);
    }

    private void RefreshWeaponForClients(string weaponName, string imageName, string source, ReadOnlySpan<byte> bytecode, ISet<ushort> touched)
    {
        var addPacket = EntityPackets.NpcWeaponAdd(weaponName, imageName, source.Replace('\n', '\u00a7'));
        var deletePacket = EntityPackets.NpcWeaponDelete(weaponName);
        var bytecodePacket = bytecode.IsEmpty ? [] : EntityPackets.NpcWeaponScriptRawData(bytecode);
        foreach (var (playerId, session) in _activeSessions)
        {
            if (!IsClient(session.Type))
                continue;

            if (_activeAccounts.TryGetValue(playerId, out var account) &&
                !account.Weapons.Contains(weaponName, StringComparer.OrdinalIgnoreCase))
                continue;

            QueueSelfPacket(playerId, deletePacket, touched);
            QueueSelfPacket(playerId, addPacket, touched);
            if (bytecodePacket.Length != 0)
                QueueSelfPacket(playerId, bytecodePacket, touched);
        }
    }

    private void SendCompilerOutputToNc(string origin, string level, string text, ISet<ushort> touched)
    {
        BroadcastToNpcControls(RcNcPackets.RcChat($"Script compiler output for {origin}:"), touched);
        var wroteLine = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = NormalizeCompilerOutputLine(rawLine);
            if (line.Length == 0)
                continue;

            BroadcastToNpcControls(RcNcPackets.RcChat($"{level}: {line}"), touched);
            wroteLine = true;
        }

        if (!wroteLine)
            BroadcastToNpcControls(RcNcPackets.RcChat($"{level}: compiler failed"), touched);
    }

    private static string CompilerOrigin(string type, string name) =>
        $"{char.ToUpperInvariant(type[0])}{type[1..]} {name}";

    private static bool TryPreflightGs2(string source, out string error)
    {
        var inString = false;
        var escaping = false;
        var inLineComment = false;
        var inBlockComment = false;
        var line = 1;

        var lineStart = 0;
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (ch == '\n')
            {
                if (inString)
                {
                    error = $"line {line}: unterminated string literal";
                    return false;
                }

                inLineComment = false;
                if (!ValidateGs2StatementLine(source[lineStart..i], line, out error))
                    return false;

                lineStart = i + 1;
                line++;
                continue;
            }

            if (inLineComment)
                continue;

            if (inBlockComment)
            {
                if (ch == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (inString)
            {
                if (escaping)
                {
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (ch == '"')
                    inString = false;

                continue;
            }

            if (ch == '/' && next == '/')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (ch == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (ch == '"')
                inString = true;
        }

        if (inString)
        {
            error = $"line {line}: unterminated string literal";
            return false;
        }

        if (inBlockComment)
        {
            error = $"line {line}: unterminated block comment";
            return false;
        }

        return ValidateGs2StatementLine(source[lineStart..], line, out error);
    }

    private static bool ValidateGs2StatementLine(string lineText, int line, out string error)
    {
        var trimmed = StripGs2LineComment(lineText).Trim();
        if (trimmed.Length == 0 ||
            trimmed.StartsWith("//#", StringComparison.Ordinal) ||
            trimmed.StartsWith("function ", StringComparison.Ordinal) ||
            trimmed.StartsWith("if ", StringComparison.Ordinal) ||
            trimmed.StartsWith("else", StringComparison.Ordinal) ||
            trimmed.StartsWith("for ", StringComparison.Ordinal) ||
            trimmed.StartsWith("while ", StringComparison.Ordinal) ||
            trimmed.StartsWith("switch ", StringComparison.Ordinal) ||
            trimmed is "{" or "}")
        {
            error = "";
            return true;
        }

        if (trimmed.EndsWith(';') ||
            trimmed.EndsWith('{') ||
            trimmed.EndsWith('}') ||
            trimmed.EndsWith(':'))
        {
            error = "";
            return true;
        }

        if (LooksLikeGs2Statement(trimmed))
        {
            error = $"line {line}: expected ';' before end of line";
            return false;
        }

        error = "";
        return true;
    }

    private static string StripGs2LineComment(string lineText)
    {
        var inString = false;
        var escaping = false;
        for (var i = 0; i < lineText.Length - 1; i++)
        {
            var ch = lineText[i];
            if (escaping)
            {
                escaping = false;
                continue;
            }

            if (ch == '\\')
            {
                escaping = true;
                continue;
            }

            if (ch == '"')
                inString = !inString;

            if (!inString && ch == '/' && lineText[i + 1] == '/')
                return lineText[..i];
        }

        return lineText;
    }

    private static bool LooksLikeGs2Statement(string trimmed) =>
        trimmed.Contains('=', StringComparison.Ordinal) ||
        trimmed.StartsWith("return ", StringComparison.Ordinal) ||
        trimmed.Contains("(", StringComparison.Ordinal);

    private static string NormalizeCompilerOutputLine(string line)
    {
        line = line.Trim();
        if (line.StartsWith("->", StringComparison.Ordinal))
            line = line[2..].Trim();

        while (true)
        {
            var lower = line.ToLowerInvariant();
            if (lower.StartsWith("[error]", StringComparison.Ordinal))
                line = line["[error]".Length..].Trim();
            else if (lower.StartsWith("error:", StringComparison.Ordinal))
                line = line["error:".Length..].Trim();
            else if (lower.StartsWith("[warning]", StringComparison.Ordinal))
                line = line["[warning]".Length..].Trim();
            else if (lower.StartsWith("warning:", StringComparison.Ordinal))
                line = line["warning:".Length..].Trim();
            else if (lower.StartsWith("[info]", StringComparison.Ordinal))
                line = line["[info]".Length..].Trim();
            else if (lower.StartsWith("info:", StringComparison.Ordinal))
                line = line["info:".Length..].Trim();
            else
                return line;
        }
    }

    private IReadOnlyList<string> ListWeaponNames()
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        if (root is null)
            return [];

        var folder = Path.Combine(root, "weapons");
        if (!Directory.Exists(folder))
            return [];

        return Directory.EnumerateFiles(folder, "weapon-*.txt")
            .Select(path => TryLoadWeaponFromPath(path, out var weapon)
                ? weapon.Name
                : Path.GetFileNameWithoutExtension(path)["weapon-".Length..])
            .Where(name => !IsDefaultWeaponName(name))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool TryLoadWeapon(string weaponName, out NcWeaponSource weapon)
    {
        weapon = new NcWeaponSource(weaponName, "", "");
        var path = ResolveServerFile("weapons", WeaponFileName(weaponName));
        if (path.Length == 0 || !File.Exists(path))
            return false;

        var realName = weaponName;
        var image = "";
        var source = new System.Text.StringBuilder();
        var inScript = false;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Replace("\r", "", StringComparison.Ordinal);
            if (line.Equals("SCRIPT", StringComparison.Ordinal))
            {
                inScript = true;
                continue;
            }

            if (line.Equals("SCRIPTEND", StringComparison.Ordinal))
            {
                inScript = false;
                continue;
            }

            if (inScript)
            {
                source.AppendLine(line);
                continue;
            }

            if (line.StartsWith("IMAGE ", StringComparison.Ordinal))
                image = line["IMAGE ".Length..].Trim();
            else if (line.StartsWith("REALNAME ", StringComparison.Ordinal))
                realName = line["REALNAME ".Length..].Trim();
        }

        weapon = new NcWeaponSource(realName, image, source.ToString().TrimEnd('\r', '\n'));
        return true;
    }

    private bool TryLoadWeaponFromPath(string path, out NcWeaponSource weapon)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith("weapon-", StringComparison.OrdinalIgnoreCase))
        {
            weapon = new NcWeaponSource("", "", "");
            return false;
        }

        return TryLoadWeapon(name["weapon-".Length..], out weapon);
    }

    private void SaveWeapon(NcWeaponSource weapon)
    {
        var path = ResolveServerFile("weapons", WeaponFileName(weapon.Name), createDirectory: true);
        var text = string.Join(
            '\n',
            [
                "GRAWP001",
                $"REALNAME {weapon.Name}",
                $"IMAGE {weapon.Image}",
                "SCRIPT",
                weapon.Source.Replace("\r", "", StringComparison.Ordinal),
                "SCRIPTEND",
                ""
            ]);
        File.WriteAllText(path, text);
    }

    private uint NextDatabaseNpcId() =>
        _databaseNpcs.Count == 0 ? 10000u : Math.Max(10000u, _databaseNpcs.Keys.Max() + 1);

    private void HydrateDatabaseNpcs()
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        if (root is null)
            return;

        var folder = Path.Combine(root, "npcs");
        if (!Directory.Exists(folder))
            return;

        foreach (var path in Directory.EnumerateFiles(folder, "npc*.txt"))
        {
            if (TryLoadDatabaseNpc(path, out var npc))
                _databaseNpcs[npc.Id] = npc;
        }
    }

    private static bool TryLoadDatabaseNpc(string path, out DatabaseNpc npc)
    {
        var name = "";
        var type = "";
        var owner = "";
        var level = "";
        var x = "";
        var y = "";
        var id = 0u;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.TrimEnd('\r');
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var key = space < 0 ? line : line[..space];
            var value = space < 0 ? "" : line[(space + 1)..].Trim();
            switch (key.ToUpperInvariant())
            {
                case "NAME":
                    name = value;
                    break;
                case "ID":
                    uint.TryParse(value, out id);
                    break;
                case "TYPE":
                    type = value;
                    break;
                case "OWNER":
                case "SCRIPTER":
                    owner = value;
                    break;
                case "STARTLEVEL":
                    level = value;
                    break;
                case "STARTX":
                    x = value;
                    break;
                case "STARTY":
                    y = value;
                    break;
                case "NPCSCRIPT":
                    npc = new DatabaseNpc(id, name, type, owner, level, x, y);
                    return id != 0 && name.Length != 0;
            }
        }

        npc = new DatabaseNpc(id, name, type, owner, level, x, y);
        return id != 0 && name.Length != 0;
    }

    private void SaveDatabaseNpc(DatabaseNpc npc)
    {
        var safeName = Path.GetFileName(npc.Name.Replace('\\', '/'));
        if (safeName.Length == 0)
            return;

        var path = ResolveServerFile("npcs", "npc" + safeName + ".txt", createDirectory: true);
        var text = string.Join(
            '\n',
            [
                "GRNPC001",
                $"NAME {npc.Name}",
                $"ID {npc.Id}",
                $"TYPE {npc.Type}",
                $"OWNER {npc.Owner}",
                $"STARTLEVEL {npc.LevelName}",
                $"STARTX {npc.X}",
                $"STARTY {npc.Y}",
                "NPCSCRIPT",
                "NPCSCRIPTEND",
                ""
            ]);
        File.WriteAllText(path, text);
    }

    private string ResolveServerFile(string folder, string fileName, bool createDirectory = false)
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        if (root is null)
            return "";

        var safeFolder = folder.Replace('\\', '/').Trim('/');
        var safeFile = Path.GetFileName(fileName.Replace('\\', '/'));
        if (safeFolder.Length == 0 || safeFile.Length == 0)
            return "";

        var directory = Path.Combine(root, safeFolder);
        if (createDirectory)
            Directory.CreateDirectory(directory);
        return Path.Combine(directory, safeFile);
    }

    private static string WeaponFileName(string weaponName)
    {
        var safe = Path.GetFileName(weaponName.Replace('\\', '/'));
        return safe.StartsWith("-", StringComparison.Ordinal) ? "weapon" + safe + ".txt" : "weapon-" + safe + ".txt";
    }

    private static bool IsDefaultWeaponName(string weaponName) =>
        weaponName.Equals("Bomb", StringComparison.OrdinalIgnoreCase) ||
        weaponName.Equals("Bow", StringComparison.OrdinalIgnoreCase);

    private void HandleAccountListGet(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var reader = new GraalBinaryReader(payload);
        var name = ReadGCharString(reader).Replace("%", "*", StringComparison.Ordinal);
        if (name.Length == 0)
            name = "*";
        _ = ReadGCharString(reader);

        var accountsPath = AccountsPath();
        if (accountsPath is null || !Directory.Exists(accountsPath))
            return;

        var names = Directory.EnumerateFiles(accountsPath, "*.txt")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Select(account => account!.Replace('_', ':'))
            .Where(account => WildcardMatch(account, name))
            .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        QueueSelfPacket(playerId, RcNcPackets.AccountListGet(names), touched);
    }

    private void HandleAccountAdd(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(playerId, ModifyStaffAccountRight) || worldEntryOptions is null)
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: You are not authorized to create new accounts."), touched);
            return;
        }

        var reader = new GraalBinaryReader(payload);
        var accountName = CleanAccountName(ReadGCharString(reader));
        _ = ReadGCharString(reader);
        var email = ReadGCharString(reader);
        var banned = reader.ReadGChar() != 0;
        var loadOnly = reader.ReadGChar() != 0;
        _ = reader.ReadGChar();
        if (accountName.Length == 0)
            return;

        var account = new AccountFileData
        {
            AccountName = accountName,
            CommunityName = accountName,
            Email = email,
            IsBanned = banned,
            IsLoadOnly = loadOnly
        };
        SaveAccount(account);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{GetAccountName(playerId)} has created a new account: {accountName}"), touched);
    }

    private void HandleAccountDelete(ushort playerId, string accountName, ISet<ushort> touched)
    {
        if (!HasRight(playerId, ModifyStaffAccountRight))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: You are not authorized to delete accounts."), touched);
            return;
        }

        var clean = CleanAccountName(accountName);
        if (string.Equals(clean, "defaultaccount", StringComparison.OrdinalIgnoreCase))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: You are not allowed to delete the default account."), touched);
            return;
        }

        var path = worldEntryOptions?.AccountFileSystem.FindCaseInsensitive(clean + ".txt");
        if (path is null)
            return;

        File.Delete(path);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{GetAccountName(playerId)} has deleted the account: {clean}"), touched);
    }

    private void HandleAccountGet(ushort playerId, string accountName, ISet<ushort> touched)
    {
        if (!TryGetAccount(CleanAccountName(accountName), out var account))
            return;

        QueueSelfPacket(
            playerId,
            RcNcPackets.AccountGet(new AccountView(
                account.AccountName,
                account.Email,
                account.IsBanned,
                account.IsLoadOnly,
                account.BanLength,
                account.BanReason)),
            touched);
    }

    private void HandleAccountSet(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(playerId, ModifyStaffAccountRight))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: You are not authorized to edit accounts.\n"), touched);
            return;
        }

        var reader = new GraalBinaryReader(payload);
        var accountName = CleanAccountName(ReadGCharString(reader));
        _ = ReadGCharString(reader);
        var email = ReadGCharString(reader);
        var banned = reader.ReadGChar() != 0;
        var loadOnly = reader.ReadGChar() != 0;
        _ = reader.ReadGChar();
        _ = ReadGCharString(reader);
        var banReason = ReadAsciiPayload(reader.ReadBytes(reader.BytesLeft));
        if (!TryGetAccount(accountName, out var account))
            return;

        account.Email = email;
        account.IsLoadOnly = loadOnly;
        if (HasRight(playerId, BanRight))
        {
            account.IsBanned = banned;
            account.BanReason = banReason;
        }

        SaveAccount(account);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{GetAccountName(playerId)} has modified the account: {accountName}"), touched);
    }

    private void HandlePlayerPropsGetById(ushort rcId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (payload.Length < 2)
            return;

        var reader = new GraalBinaryReader(payload);
        var targetId = reader.ReadGShort();
        if (!_activeSnapshots.TryGetValue(targetId, out var snapshot))
            return;

        QueueSelfPacket(rcId, RcNcPackets.PlayerPropsGet(targetId, BuildRcPlayerProps(snapshot)), touched);
    }

    private void HandlePlayerPropsGetByAccount(ushort rcId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var reader = new GraalBinaryReader(payload);
        var accountName = CleanAccountName(ReadGCharString(reader));
        if (accountName.Length == 0)
            return;

        var active = _activeSnapshots.Values.FirstOrDefault(snapshot =>
            string.Equals(snapshot.LoginPropertySource.AccountName, accountName, StringComparison.OrdinalIgnoreCase));
        if (active is not null)
        {
            QueueSelfPacket(rcId, RcNcPackets.PlayerPropsGet(active.PlayerId, BuildRcPlayerProps(active)), touched);
            return;
        }

        if (!TryGetAccount(accountName, out var account))
            return;

        QueueSelfPacket(rcId, RcNcPackets.PlayerPropsGet(0, BuildRcPlayerProps(account)), touched);
    }

    private void HandlePlayerPropsSetById(ushort rcId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (payload.Length < 2)
            return;

        var reader = new GraalBinaryReader(payload);
        var targetId = reader.ReadGShort();
        if (!_activePlayers.TryGetValue(targetId, out var player))
            return;

        HandlePlayerPropsSet(rcId, player.AccountName, player, reader.ReadBytes(reader.BytesLeft), touched);
    }

    private void HandlePlayerPropsSetByAccount(ushort rcId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        var reader = new GraalBinaryReader(payload);
        var accountName = CleanAccountName(ReadGCharString(reader));
        if (accountName.Length == 0)
            return;

        var activePlayer = _activePlayers.Values.FirstOrDefault(player =>
            string.Equals(player.AccountName, accountName, StringComparison.OrdinalIgnoreCase));
        HandlePlayerPropsSet(rcId, accountName, activePlayer, reader.ReadBytes(reader.BytesLeft), touched);
    }

    private void HandlePlayerPropsSet(
        ushort rcId,
        string accountName,
        RuntimePlayer? activePlayer,
        ReadOnlySpan<byte> payload,
        ISet<ushort> touched)
    {
        if (!TryGetAccount(accountName, out var account))
            return;

        var editingSelf = string.Equals(GetAccountName(rcId), account.AccountName, StringComparison.OrdinalIgnoreCase);
        if ((editingSelf && !HasRight(rcId, SetSelfAttributesRight)) ||
            (!editingSelf && !HasRight(rcId, SetAttributesRight)))
        {
            QueueSelfPacket(rcId, RcNcPackets.RcChat($"Server: {GetAccountName(rcId)} is not authorized to set the properties of {account.AccountName}"), touched);
            return;
        }

        if (!TryReadRcProps(payload, out var propPayload))
            return;

        var parsed = IncomingPlayerPropsParser.Parse(propPayload, ClientVersionId.Client21);
        if (!parsed.Success)
            return;

        if (activePlayer is not null && runtimeServer is not null)
        {
            var result = LiveWorldSessionForwarder.TryApplyAndForwardConfirmedPlayerProps(
                runtimeServer,
                activePlayer,
                parsed.Updates,
                senderSupportsPreciseMovement: true,
                BuildSinks(),
                RuntimePlayerPropsOptions.Default with
                {
                    NicknamePolicy = RuntimeNicknameUpdatePolicy.WordFilterAllowedNoGuild
                });

            foreach (var delivery in result.Deliveries)
                touched.Add(delivery.PlayerId);

            CopyRuntimeToAccount(activePlayer, account);
        }
        else if (activePlayer is not null)
        {
            RuntimePlayerPropsApplier.ApplyConfirmed(
                activePlayer,
                parsed.Updates,
                RuntimePlayerPropsOptions.Default with
                {
                    NicknamePolicy = RuntimeNicknameUpdatePolicy.WordFilterAllowedNoGuild
                });
            CopyRuntimeToAccount(activePlayer, account);
        }
        else
        {
            ApplyOfflineRcProps(account, parsed.Updates);
        }

        SaveAccount(account);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{GetAccountName(rcId)} set the attributes of player {account.AccountName}"), touched);
    }

    private void HandlePlayerRightsGet(ushort playerId, string accountName, ISet<ushort> touched)
    {
        if (!TryGetAccount(CleanAccountName(accountName), out var account))
            return;

        QueueSelfPacket(
            playerId,
            RcNcPackets.PlayerRightsGet(new AccountRightsView(
                account.AccountName,
                account.AdminRights,
                account.AdminIp,
                account.FolderRights)),
            touched);
    }

    private void HandlePlayerRightsSet(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(playerId, SetRightsRight))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: You are not authorized to set player rights."), touched);
            return;
        }

        var reader = new GraalBinaryReader(payload);
        var accountName = CleanAccountName(ReadGCharString(reader));
        if (!TryGetAccount(accountName, out var account))
            return;

        var rights = unchecked((int)reader.ReadGInt5());
        if (!HasRight(playerId, ModifyStaffAccountRight) && _activeAccounts.TryGetValue(playerId, out var rcAccount))
            rights &= rcAccount.AdminRights;

        account.AdminRights = rights;
        account.AdminIp = ReadGCharString(reader);
        var folderLength = reader.ReadGShort();
        var folderText = GUntokenize(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(folderLength)));
        account.FolderRights.Clear();
        foreach (var folder in folderText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (folder.Contains(':', StringComparison.Ordinal) ||
                folder.Contains("..", StringComparison.Ordinal) ||
                folder.Contains(" /*", StringComparison.Ordinal))
                continue;
            account.FolderRights.Add(folder);
        }

        SaveAccount(account);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{GetAccountName(playerId)} has set the rights of {accountName}"), touched);
    }

    private void HandlePlayerCommentsGet(ushort playerId, string accountName, ISet<ushort> touched)
    {
        if (!TryGetAccount(CleanAccountName(accountName), out var account))
            return;

        QueueSelfPacket(playerId, RcNcPackets.PlayerCommentsGet(account.AccountName, account.Comments), touched);
    }

    private void HandlePlayerCommentsSet(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(playerId, SetCommentsRight))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: You are not authorized to set player comments."), touched);
            return;
        }

        var reader = new GraalBinaryReader(payload);
        var accountName = CleanAccountName(ReadGCharString(reader));
        if (!TryGetAccount(accountName, out var account))
            return;

        account.Comments = ReadAsciiPayload(reader.ReadBytes(reader.BytesLeft));
        SaveAccount(account);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{GetAccountName(playerId)} has set the comments of {accountName}"), touched);
    }

    private void HandlePlayerBanGet(ushort playerId, string accountName, ISet<ushort> touched)
    {
        if (!TryGetAccount(CleanAccountName(accountName), out var account))
            return;

        QueueSelfPacket(playerId, RcNcPackets.PlayerBanGet(account.AccountName, account.IsBanned, account.BanReason), touched);
    }

    private void HandlePlayerBanSet(ushort playerId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(playerId, BanRight))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: You are not authorized to set player bans."), touched);
            return;
        }

        var reader = new GraalBinaryReader(payload);
        var accountName = CleanAccountName(ReadGCharString(reader));
        if (!TryGetAccount(accountName, out var account))
            return;

        account.IsBanned = reader.ReadGChar() != 0;
        account.BanReason = ReadAsciiPayload(reader.ReadBytes(reader.BytesLeft));
        SaveAccount(account);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{GetAccountName(playerId)} has set the ban of {accountName}"), touched);
    }

    private void HandleDisconnectPlayer(
        ushort rcId,
        ReadOnlySpan<byte> payload,
        string rcAccountName,
        ISet<ushort> touched,
        ISet<ushort> forceEndSessions)
    {
        if (!HasRight(rcId, DisconnectRight))
        {
            QueueSelfPacket(rcId, RcNcPackets.RcChat("Server: You are not authorized to disconnect players."), touched);
            return;
        }

        var reader = new GraalBinaryReader(payload);
        var targetId = reader.ReadGShort();
        var reason = ReadAsciiPayload(reader.ReadBytes(reader.BytesLeft));
        var message = $"One of the server administrators, {rcAccountName}, has disconnected you";
        message += reason.Length == 0 ? "." : $" for the following reason: {reason}";
        QueueSelfPacket(targetId, OutboundLoginPackets.DisconnectMessage(message, appendNewline: true), touched);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{rcAccountName} disconnected {GetAccountName(targetId)}"), touched);
        forceEndSessions.Add(targetId);
    }

    private void HandleWarpPlayer(ushort rcId, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(rcId, WarpToPlayerRight))
        {
            QueueSelfPacket(rcId, RcNcPackets.RcChat("Server: You are not authorized to warp players.\n"), touched);
            return;
        }

        var reader = new GraalBinaryReader(payload);
        var targetId = reader.ReadGShort();
        var x = reader.ReadGChar() / 2.0f;
        var y = reader.ReadGChar() / 2.0f;
        var level = ReadAsciiPayload(reader.ReadBytes(reader.BytesLeft));
        if (!_activePlayers.TryGetValue(targetId, out var player))
            return;

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [
                IncomingPlayerPropertyUpdate.String(PlayerPropertyId.CurrentLevel, level),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.X, (byte)(x * 2)),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Y, (byte)(y * 2))
            ]);
        player.JoinLevel(GetOrCreateLevel(level));
        QueueSelfPacket(targetId, AppendNewline(WarpPackets.BuildPlayerWarp(x, y, level)), touched);
    }

    private void HandlePrivateAdminMessage(
        ReadOnlySpan<byte> payload,
        ushort senderId,
        string senderAccount,
        ISet<ushort> touched)
    {
        if (!HasRight(senderId, AdminMessageRight))
        {
            QueueSelfPacket(senderId, RcNcPackets.RcChat("Server: You are not authorized to send an admin message."), touched);
            return;
        }

        if (payload.Length < 2)
            return;

        var reader = new GraalBinaryReader(payload);
        var targetId = reader.ReadGShort();
        var message = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(reader.BytesLeft)).TrimEnd('\r', '\n');
        QueueSelfPacket(targetId, RcNcPackets.RcAdminMessage($"Admin {senderAccount}:\u00a7{message}"), touched);
    }

    private void HandleFileBrowserStart(ushort playerId, ISet<ushort> touched)
    {
        if (!_activeAccounts.TryGetValue(playerId, out var account) || account.FolderRights.Count == 0)
            return;

        var folders = account.FolderRights
            .Select(ParseFolderRight)
            .Select(static entry => entry.Folder)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        QueueSelfPacket(playerId, RcNcPackets.FileBrowserDirList(string.Join('\n', account.FolderRights) + "\n"), touched);
        QueueSelfPacket(playerId, RcNcPackets.FileBrowserMessage("Welcome to the File Browser."), touched);
        QueueSelfPacket(playerId, BuildFileBrowserDir(account, account.LastFolder.Length == 0 ? folders[0] : account.LastFolder), touched);
    }

    private void HandleFileBrowserChangeDirectory(ushort playerId, string folder, ISet<ushort> touched)
    {
        if (!_activeAccounts.TryGetValue(playerId, out var account))
            return;

        var normalized = NormalizeFolder(folder);
        var allowed = account.FolderRights
            .Select(ParseFolderRight)
            .Any(entry => string.Equals(entry.Folder, normalized, StringComparison.Ordinal));
        if (!allowed)
            return;

        account.LastFolder = normalized;
        QueueSelfPacket(playerId, RcNcPackets.FileBrowserMessage($"Folder changed to {normalized}"), touched);
        QueueSelfPacket(playerId, BuildFileBrowserDir(account, normalized), touched);
    }

    private byte[] BuildRcPlayerProps(PostLoginPlayerSnapshot snapshot) =>
        BuildRcPlayerProps(
            snapshot.PlayerId,
            snapshot.LoginPropertySource,
            snapshot.PlayerFlags,
            snapshot.Account?.Chests ?? [],
            snapshot.Account?.Weapons ?? []);

    private byte[] BuildRcPlayerProps(AccountFileData account) =>
        BuildRcPlayerProps(
            0,
            BuildPropertySource(account),
            account.Flags.Select(flag => new LoginFlag(flag.Key, flag.Value)).ToArray(),
            account.Chests,
            account.Weapons);

    private static byte[] BuildRcPlayerProps(
        ushort playerId,
        PlayerPropertySource source,
        IReadOnlyList<LoginFlag> flags,
        IReadOnlyList<string> chests,
        IReadOnlyList<string> weapons)
    {
        var writer = new GraalBinaryWriter();
        WriteGCharString(writer, source.AccountName);
        WriteGCharString(writer, "main");

        var props = PlayerPropertySerializer.SerializeConfirmedLoginSubset(source with { PlayerId = playerId }, RcPlayerPropertyIds);
        writer.WriteGChar((byte)props.Length);
        writer.WriteBytes(props);

        writer.WriteGShort((ushort)flags.Count);
        foreach (var flag in flags)
        {
            var text = string.IsNullOrEmpty(flag.Value) ? flag.Name : $"{flag.Name}={flag.Value}";
            if (text.Length > 0xDF)
                text = text[..0xDF];
            WriteGCharString(writer, text);
        }

        writer.WriteGShort((ushort)chests.Count);
        foreach (var chest in chests)
        {
            var parts = chest.Split(':', 3);
            if (parts.Length != 3)
                continue;

            var chestData = new GraalBinaryWriter();
            chestData.WriteGChar(byte.TryParse(parts[0], out var x) ? x : (byte)0);
            chestData.WriteGChar(byte.TryParse(parts[1], out var y) ? y : (byte)0);
            chestData.WriteBytes(System.Text.Encoding.ASCII.GetBytes(parts[2]));
            var chestBytes = chestData.ToArray();
            writer.WriteGChar((byte)chestBytes.Length);
            writer.WriteBytes(chestBytes);
        }

        writer.WriteGChar((byte)weapons.Count);
        foreach (var weapon in weapons)
            WriteGCharString(writer, weapon);

        return writer.ToArray();
    }

    private bool TryGetAccount(string accountName, out AccountFileData account)
    {
        if (_activeAccounts.Values.FirstOrDefault(active =>
                string.Equals(active.AccountName, accountName, StringComparison.OrdinalIgnoreCase)) is { } activeAccount)
        {
            account = activeAccount;
            return true;
        }

        account = new AccountFileData();
        if (worldEntryOptions is null || accountName.Length == 0)
            return false;

        if (worldEntryOptions.AccountFileSystem.FindCaseInsensitive(accountName + ".txt") is null)
            return false;

        var load = AccountLoadService.Load(
            accountName,
            worldEntryOptions.AccountFileSystem,
            EffectiveAccountSettings(),
            ignoreNickname: false);
        if (!load.Success || load.Account is null)
            return false;

        account = load.Account;
        return true;
    }

    private string? AccountsPath()
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        return root is null ? null : Path.Combine(root, "accounts");
    }

    private void SaveAccount(AccountFileData account)
    {
        if (worldEntryOptions is null)
            return;

        AccountSaveService.Save(account, worldEntryOptions.AccountFileSystem);
        foreach (var active in _activeAccounts.Where(entry =>
                     string.Equals(entry.Value.AccountName, account.AccountName, StringComparison.OrdinalIgnoreCase)).ToArray())
            _activeAccounts[active.Key] = account;
    }

    private static bool TryReadRcProps(ReadOnlySpan<byte> payload, out byte[] props)
    {
        props = [];
        if (payload.Length < 2)
            return false;

        var reader = new GraalBinaryReader(payload);
        _ = ReadGCharString(reader);
        if (reader.BytesLeft <= 0)
            return false;

        var propLength = reader.ReadGChar();
        if (propLength > reader.BytesLeft)
            return false;

        props = reader.ReadBytes(propLength);
        return true;
    }

    private static void ApplyOfflineRcProps(
        AccountFileData account,
        IReadOnlyList<IncomingPlayerPropertyUpdate> updates)
    {
        foreach (var update in updates)
        {
            if (update.PropertyId == PlayerPropertyId.Nickname && update.StringValue is { } nickname)
                account.Nickname = string.IsNullOrWhiteSpace(nickname) ? "unknown" : nickname;
        }
    }

    private byte[] BuildFileBrowserDir(AccountFileData account, string folder)
    {
        var normalized = NormalizeFolder(folder);
        var entries = new List<RcFileBrowserEntry>();
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        var path = root is null ? "" : Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar));
        var rights = account.FolderRights
            .Select(ParseFolderRight)
            .FirstOrDefault(entry => string.Equals(entry.Folder, normalized, StringComparison.Ordinal))?.Rights ?? "r";
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                var info = new FileInfo(file);
                if (info.Name.StartsWith(".", StringComparison.Ordinal))
                    continue;

                entries.Add(new RcFileBrowserEntry(
                    info.Name,
                    rights,
                    unchecked((uint)Math.Min(info.Length, uint.MaxValue)),
                    unchecked((uint)new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds())));
            }
        }

        return RcNcPackets.FileBrowserDir(normalized, entries);
    }

    private void HandleFileBrowserDownload(ushort playerId, string fileName, ISet<ushort> touched)
    {
        if (!_activeAccounts.TryGetValue(playerId, out var account) ||
            !_activeSessions.TryGetValue(playerId, out var session) ||
            fileName.Length == 0)
        {
            return;
        }

        var folder = NormalizeFolder(account.LastFolder.Length == 0 ? "accounts/" : account.LastFolder);
        var safeFileName = Path.GetFileName(fileName.Replace('\\', Path.DirectorySeparatorChar));
        if (safeFileName.Length == 0)
            return;

        var relativePath = NormalizeFolder(folder + safeFileName);
        var decision = RcProtectedFiles.EvaluateDownload(relativePath, (AdminRight)account.AdminRights);
        if (!decision.Allowed)
        {
            QueueSelfPacket(playerId, RcNcPackets.FileBrowserMessage(decision.Message ?? $"Insufficient rights to download/view {relativePath}"), touched);
            return;
        }

        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        var path = root is null ? "" : Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            Console.WriteLine($"RC file download failed: missing path={path}; relative={relativePath}; player={playerId}");
            QueueSelfPacket(playerId, FileTransferPackets.FileSendFailed(safeFileName), touched);
            return;
        }

        var data = File.ReadAllBytes(path);
        if (data.Length == 0)
        {
            Console.WriteLine($"RC file download failed: empty path={path}; relative={relativePath}; player={playerId}");
            QueueSelfPacket(playerId, FileTransferPackets.FileSendFailed(safeFileName), touched);
            return;
        }

        var modTime = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
        var isBigFile = data.Length > FileTransferPackets.ChunkSize;
        if (isBigFile)
        {
            QueueSelfPacket(playerId, FileTransferPackets.LargeFileStart(safeFileName), touched);
            QueueSelfPacket(playerId, FileTransferPackets.LargeFileSize(data.Length), touched);
        }

        var offset = 0;
        while (offset < data.Length)
        {
            var sendSize = Math.Min(FileTransferPackets.ChunkSize, data.Length - offset);
            QueueSelfPacket(
                playerId,
                FileTransferPackets.BuildFileChunk(safeFileName, data.AsSpan(offset, sendSize), modTime, includeModTime: true),
                touched);
            offset += sendSize;
        }

        if (isBigFile)
            QueueSelfPacket(playerId, FileTransferPackets.LargeFileEnd(safeFileName), touched);

        QueueSelfPacket(playerId, RcNcPackets.FileBrowserMessage($"Downloaded file {safeFileName}"), touched);
    }

    private string ReadConfigFile(string fileName)
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        if (root is null)
            return "";

        var path = Path.Combine(root, "config", fileName);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r", "", StringComparison.Ordinal) : "";
    }

    private void HandleServerOptionsSet(ushort playerId, string accountName, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(playerId, SetServerOptionsRight))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat($"Server: {accountName} is not authorized to change the server options."), touched);
            return;
        }

        var options = NormalizeConfigText(GUntokenize(ReadAsciiPayload(payload)));
        WriteConfigFile("serveroptions.txt", options);
        _serverOptionsOverride = LoadServerOptions();
        RefreshNpcServerPlayer(touched);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{accountName} has updated the server options."), touched);
        BroadcastNpcAddresses(touched);
    }

    private void HandleFolderConfigSet(ushort playerId, string accountName, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(playerId, SetFolderOptionsRight))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat($"Server: {accountName} is not authorized to change the folder config."), touched);
            return;
        }

        var folders = NormalizeConfigText(GUntokenize(ReadAsciiPayload(payload)));
        WriteConfigFile("foldersconfig.txt", folders);
        BroadcastToRemoteControls(RcNcPackets.RcChat($"{accountName} updated the folder config."), touched);
    }

    private void HandleServerFlagsSet(ushort playerId, string accountName, ReadOnlySpan<byte> payload, ISet<ushort> touched)
    {
        if (!HasRight(playerId, SetServerFlagsRight))
        {
            QueueSelfPacket(playerId, RcNcPackets.RcChat("Server: You are not authorized to set the server flags."), touched);
            return;
        }

        var previous = LoadServerFlags().ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        var reader = new GraalBinaryReader(payload);
        var count = reader.ReadGShort();
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < count && reader.BytesLeft > 0; i++)
        {
            var flag = ReadGCharString(reader);
            var equals = flag.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
                flags[flag] = "";
            else
                flags[flag[..equals]] = flag[(equals + 1)..];
        }

        _serverFlags = flags;
        WriteServerFlags(flags);
        foreach (var flag in flags)
        {
            if (previous.TryGetValue(flag.Key, out var oldValue) && oldValue == flag.Value)
                continue;

            BroadcastToClients(BuildFlagSetPacket(new LoginFlag(flag.Key, flag.Value)), touched);
        }

        BroadcastToRemoteControls(RcNcPackets.RcChat($"{accountName} has updated the server flags."), touched);
    }

    private void WriteConfigFile(string fileName, string text)
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        if (root is null)
            return;

        var path = Path.Combine(root, "config", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
    }

    private IReadOnlyList<KeyValuePair<string, string>> LoadServerFlags()
    {
        if (_serverFlags is not null)
            return _serverFlags.ToArray();

        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = ServerFilePath("serverflags.txt");
        if (path is not null && File.Exists(path))
        {
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                var equals = line.IndexOf('=', StringComparison.Ordinal);
                if (equals < 0)
                    flags[line] = "";
                else
                    flags[line[..equals]] = line[(equals + 1)..];
            }
        }

        _serverFlags = flags;
        return flags.ToArray();
    }

    private void WriteServerFlags(IReadOnlyDictionary<string, string> flags)
    {
        var path = ServerFilePath("serverflags.txt");
        if (path is null)
            return;

        var lines = flags.Select(static flag => flag.Value.Length == 0 ? flag.Key : $"{flag.Key}={flag.Value}");
        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private string? ServerFilePath(string fileName)
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        return root is null ? null : Path.Combine(root, fileName);
    }

    private static string NormalizeConfigText(string text)
    {
        var normalized = text.Replace("\r", "", StringComparison.Ordinal).TrimEnd('\n');
        return normalized.Length == 0 ? "" : normalized + "\n";
    }

    private void BroadcastNpcAddresses(ISet<ushort> touched)
    {
        foreach (var (playerId, session) in _activeSessions)
        {
            if (!IsRemoteControl(session.Type) || !_activeAccounts.TryGetValue(playerId, out var account))
                continue;

            QueueNpcServerAddress(session, account, touched);
        }
    }

    private IAccountLoadSettings EffectiveAccountSettings() =>
        _serverOptionsOverride is null
            ? worldEntryOptions?.AccountSettings ?? AccountLoadSettings.Empty
            : new OverlayAccountLoadSettings(_serverOptionsOverride, worldEntryOptions?.AccountSettings);

    private Gs2Settings? LoadServerOptions()
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        if (root is null)
            return null;

        return Gs2Settings.LoadFile(Path.Combine(root, "config", "serveroptions.txt"));
    }

    private void QueueNpcServerAddress(ClientSessionSkeleton session, AccountFileData? account, ISet<ushort>? touched = null)
    {
        if (!IsRemoteControl(session.Type) || account is null || (account.AdminRights & NpcControlRight) == 0)
            return;

        var endpoint = LoadNpcServerEndpoint();
        if (endpoint is null)
            return;

        QueueSelfPacket(session.Id, RcNcPackets.NpcServerAddress(endpoint.Id, endpoint.Host, endpoint.Port), touched ?? new HashSet<ushort>());
    }

    private NpcServerEndpoint? LoadNpcServerEndpoint()
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        var settings = EffectiveAccountSettings();
        if (root is null || settings is null)
            return null;

        var config = Gs2Settings.LoadFile(Path.Combine(root, "config", "npcserver.txt"));
        var enabled = config.IsOpened
            ? config.GetBool("enabled", GetBool(settings, "serverside", false))
            : GetBool(settings, "serverside", false);
        if (!enabled)
            return null;

        var id = (ushort)Math.Clamp(config.GetInt("id", NpcServerPlayerId), ushort.MinValue, ushort.MaxValue);
        var host = config.GetString("host", "AUTO");
        if (IsAuto(host))
            host = config.GetString("ns_ip", settings.GetString("ns_ip", "AUTO"));
        if (IsAuto(host))
            host = settings.GetString("localip", "AUTO");
        if (IsAuto(host))
            host = settings.GetString("serverip", "AUTO");
        if (IsAuto(host))
            host = "127.0.0.1";

        var portText = config.GetString("port", "AUTO");
        if (IsAuto(portText))
            portText = settings.GetString("ncport", "AUTO");
        if (IsAuto(portText))
            portText = settings.GetString("serverport", "0");
        return int.TryParse(portText, out var port) && port > 0
            ? new NpcServerEndpoint(id, host, port)
            : null;
    }

    private static bool GetBool(IAccountLoadSettings settings, string key, bool defaultValue)
    {
        if (!settings.Exists(key))
            return defaultValue;

        var value = settings.GetString(key, defaultValue ? "true" : "false");
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    private static bool IsAuto(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("AUTO", StringComparison.OrdinalIgnoreCase);

    private bool HasRight(ushort playerId, int right) =>
        _activeAccounts.TryGetValue(playerId, out var account) &&
        (account.AdminRights & right) != 0;

    private void BroadcastToRemoteControls(byte[] packet, ISet<ushort> touched)
    {
        foreach (var (playerId, session) in _activeSessions)
        {
            if (!IsRemoteControl(session.Type))
                continue;

            QueueSelfPacket(playerId, packet, touched);
        }
    }

    private void BroadcastToNpcControls(byte[] packet, ISet<ushort> touched)
    {
        foreach (var (playerId, session) in _activeSessions)
        {
            if ((session.Type & PlayerSessionType.AnyNpcControl) == 0)
                continue;

            QueueSelfPacket(playerId, packet, touched);
        }
    }

    private void BroadcastToControlClients(byte[] packet, ISet<ushort> touched)
    {
        foreach (var (playerId, session) in _activeSessions)
        {
            if (!IsControl(session.Type))
                continue;

            QueueSelfPacket(playerId, packet, touched);
        }
    }

    private void BroadcastToClients(byte[] packet, ISet<ushort> touched)
    {
        foreach (var (playerId, session) in _activeSessions)
        {
            if (!IsClient(session.Type))
                continue;

            QueueSelfPacket(playerId, packet, touched);
        }
    }

    private void BroadcastToAllExcept(ushort senderId, byte[] packet, ISet<ushort> touched)
    {
        foreach (var playerId in _activeSessions.Keys)
        {
            if (playerId == senderId)
                continue;

            QueueSelfPacket(playerId, packet, touched);
        }
    }

    private static string ReadAsciiPayload(ReadOnlySpan<byte> payload) =>
        System.Text.Encoding.ASCII.GetString(payload).TrimEnd('\r', '\n');

    private string GetAccountName(ushort playerId) =>
        _activeAccounts.TryGetValue(playerId, out var account)
            ? account.AccountName
            : _activePlayers.TryGetValue(playerId, out var player)
                ? player.AccountName
                : playerId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string ReadGCharString(GraalBinaryReader reader)
    {
        var length = reader.ReadGChar();
        return System.Text.Encoding.ASCII.GetString(reader.ReadBytes(length));
    }

    private static byte[] GCharPayload(string value)
    {
        var writer = new GraalBinaryWriter();
        WriteGCharString(writer, value);
        return writer.ToArray();
    }

    private static void WriteGCharString(GraalBinaryWriter writer, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        writer.WriteGChar((byte)bytes.Length);
        writer.WriteBytes(bytes);
    }

    private static string CleanAccountName(string accountName)
    {
        var clean = accountName.Trim().TrimEnd('\r', '\n');
        var slash = Math.Max(clean.LastIndexOf('/'), clean.LastIndexOf('\\'));
        return slash >= 0 ? clean[(slash + 1)..] : clean;
    }

    private static bool WildcardMatch(string value, string pattern)
    {
        var v = 0;
        var p = 0;
        var star = -1;
        var match = 0;
        while (v < value.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(value[v])))
            {
                v++;
                p++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                match = v;
            }
            else if (star != -1)
            {
                p = star + 1;
                v = ++match;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
            p++;
        return p == pattern.Length;
    }

    private static string GUntokenize(string value)
    {
        var lines = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < value.Length && value[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else if (ch == '\\' && i + 1 < value.Length)
                {
                    current.Append(value[++i]);
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                quoted = true;
            }
            else if (ch == ',')
            {
                lines.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        lines.Add(current.ToString());
        return string.Join('\n', lines);
    }

    private static string ReadLatin1Payload(ReadOnlySpan<byte> payload) =>
        System.Text.Encoding.Latin1.GetString(payload).TrimEnd('\r', '\n');

    private static byte[] AppendNewline(byte[] packet) =>
        packet.Length > 0 && packet[^1] == (byte)'\n'
            ? packet
            : [.. packet, (byte)'\n'];

    private static string NormalizeFolder(string folder)
    {
        var normalized = folder.Replace('\\', '/').Trim();
        if (normalized.Length != 0 && !normalized.EndsWith("/", StringComparison.Ordinal))
            normalized += "/";
        return normalized;
    }

    private static RcFolderRight ParseFolderRight(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return new RcFolderRight("r", "*", "*");

        var splitAt = trimmed.IndexOf(' ');
        var rights = splitAt < 0 ? trimmed : trimmed[..splitAt];
        var folder = splitAt < 0 ? "*" : trimmed[(splitAt + 1)..].Trim();
        rights = rights.Trim().ToLowerInvariant();
        folder = folder.Replace('\\', '/');

        var wildcard = "*";
        if (!folder.EndsWith("/", StringComparison.Ordinal))
        {
            var lastSlash = folder.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                wildcard = folder[(lastSlash + 1)..];
                folder = folder[..(lastSlash + 1)];
            }
        }

        return new RcFolderRight(rights, folder, wildcard);
    }

    private sealed record RcFolderRight(string Rights, string Folder, string Wildcard);

    private static readonly IReadOnlyList<PlayerPropertyId> RcPlayerPropertyIds =
    [
        PlayerPropertyId.Nickname,
        PlayerPropertyId.MaxPower,
        PlayerPropertyId.CurrentPower,
        PlayerPropertyId.RupeesCount,
        PlayerPropertyId.ArrowsCount,
        PlayerPropertyId.BombsCount,
        PlayerPropertyId.GlovePower,
        PlayerPropertyId.SwordPower,
        PlayerPropertyId.ShieldPower,
        PlayerPropertyId.Gani,
        PlayerPropertyId.HeadGif,
        PlayerPropertyId.Colors,
        PlayerPropertyId.X,
        PlayerPropertyId.Y,
        PlayerPropertyId.Status,
        PlayerPropertyId.CurrentLevel,
        PlayerPropertyId.ApCounter,
        PlayerPropertyId.MagicPoints,
        PlayerPropertyId.KillsCount,
        PlayerPropertyId.DeathsCount,
        PlayerPropertyId.OnlineSeconds,
        PlayerPropertyId.Alignment,
        PlayerPropertyId.AccountName,
        PlayerPropertyId.BodyImage,
        PlayerPropertyId.Rating,
        PlayerPropertyId.GAttrib1,
        PlayerPropertyId.GAttrib2,
        PlayerPropertyId.GAttrib3,
        PlayerPropertyId.GAttrib4,
        PlayerPropertyId.GAttrib5
    ];

    private static PlayerPropertySource BuildPropertySource(AccountFileData account) =>
        new(
            Nickname: account.Nickname,
            MaxPower: account.MaxHitpoints,
            Hitpoints: account.Hitpoints,
            Rupees: account.Rupees,
            Arrows: account.Arrows,
            Bombs: account.Bombs,
            GlovePower: account.GlovePower,
            SwordPower: account.SwordPower,
            SwordImage: account.SwordImage,
            ShieldPower: account.ShieldPower,
            ShieldImage: account.ShieldImage,
            Gani: account.Gani,
            HeadImage: account.HeadImage,
            ChatMessage: "",
            Colors: account.Colors,
            PlayerId: 0,
            X: account.PixelX,
            Y: account.PixelY,
            Sprite: account.Sprite,
            Status: (byte)Math.Clamp(account.Status, 0, byte.MaxValue),
            CarrySprite: 0,
            CurrentLevel: account.LevelName,
            HorseImage: "",
            HorseBombCount: 0,
            CarryNpcId: 0,
            ApCounter: account.ApCounter,
            MagicPoints: account.MagicPoints,
            Kills: unchecked((int)account.Kills),
            Deaths: unchecked((int)account.Deaths),
            OnlineSeconds: account.OnlineSeconds,
            AccountIp: account.AccountIp,
            Alignment: account.Alignment,
            AdditionalFlags: 0,
            AccountName: account.AccountName,
            BodyImage: account.BodyImage,
            EloRating: (int)account.EloRating,
            EloDeviation: (int)account.EloDeviation,
            GaniAttributes: account.GaniAttributes
                .Select((value, index) => (value, index: index + 1))
                .Where(entry => !string.IsNullOrEmpty(entry.value))
                .ToDictionary(entry => entry.index, entry => entry.value),
            Os: "",
            TextCodePage: 1252,
            CommunityName: account.CommunityName,
            Z: account.PixelZ,
            BowPower: account.BowPower,
            BowImage: account.BowImage,
            Language: account.Language);

    private string HandleWeaponAdd(GraalBinaryReader reader, ushort playerId)
    {
        if (!_activeAccounts.TryGetValue(playerId, out var account))
            return "weaponadd=missing-account";

        var type = reader.ReadGChar();
        if (type != 0)
            return "weaponadd=npc-unported";

        var itemType = LevelItemCatalog.GetItemId(reader.ReadGChar());
        var weaponName = LevelItemCatalog.GetItemName(itemType);
        if (string.IsNullOrEmpty(weaponName))
            return "weaponadd=invalid-default";

        if (!account.Weapons.Contains(weaponName, StringComparer.Ordinal))
            account.Weapons.Add(weaponName);

        return $"weaponadd={weaponName}";
    }

    private string HandleNpcWeaponDelete(GraalBinaryReader reader, ushort playerId)
    {
        if (!_activeAccounts.TryGetValue(playerId, out var account))
            return "weapondel=missing-account";

        var weaponName = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(reader.BytesLeft));
        account.Weapons.RemoveAll(weapon => weapon == weaponName);
        return $"weapondel={weaponName}";
    }

    private static string HexPreview(ReadOnlySpan<byte> bytes, int maxBytes)
    {
        if (bytes.IsEmpty)
            return "";

        var length = Math.Min(bytes.Length, maxBytes);
        return Convert.ToHexString(bytes[..length]);
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

        var value = EffectiveAccountSettings().GetString(key, defaultValue ? "true" : "false");
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private int GetIntOption(string key, int defaultValue)
    {
        if (worldEntryOptions is null)
            return defaultValue;

        var value = EffectiveAccountSettings().GetString(key, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private float GetFloatOption(string key, float defaultValue)
    {
        if (worldEntryOptions is null)
            return defaultValue;

        var value = EffectiveAccountSettings().GetString(key, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
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

    private bool EnsureNpcServerPlayer()
    {
        if (worldEntryOptions is null || !GetBoolOption("serverside", defaultValue: false))
            return true;

        if (_activeSnapshots.ContainsKey(NpcServerPlayerId))
            return true;

        var settings = EffectiveAccountSettings();
        var nickname = BuildNpcServerNickname(settings);
        var source = new PlayerPropertySource(
            Nickname: nickname,
            MaxPower: 3,
            Hitpoints: 3,
            Rupees: 0,
            Arrows: 0,
            Bombs: 0,
            GlovePower: 0,
            SwordPower: 0,
            SwordImage: "",
            ShieldPower: 0,
            ShieldImage: "",
            Gani: "idle",
            HeadImage: settings.GetString("staffhead", "head25.png"),
            ChatMessage: "",
            Colors: [0, 0, 0, 0, 0],
            PlayerId: NpcServerPlayerId,
            X: 112 * 16,
            Y: 112 * 16,
            Sprite: 0,
            Status: 0,
            CarrySprite: 0,
            CurrentLevel: " ",
            HorseImage: "",
            HorseBombCount: 0,
            CarryNpcId: 0,
            ApCounter: 0,
            MagicPoints: 0,
            Kills: 0,
            Deaths: 0,
            OnlineSeconds: 0,
            AccountIp: 0,
            Alignment: 50,
            AdditionalFlags: 0,
            AccountName: "(npcserver)",
            BodyImage: "body.png",
            EloRating: 1500,
            EloDeviation: 350,
            GaniAttributes: new Dictionary<int, string>(),
            Os: "",
            TextCodePage: 1252,
            CommunityName: "(npcserver)",
            Z: 0,
            BowPower: 0,
            BowImage: "",
            Language: "English");
        var snapshot = new PostLoginPlayerSnapshot(
            NpcServerPlayerId,
            PlayerSessionType.NpcServer,
            GCharString("(npcserver)"),
            GCharString(nickname),
            GCharString(" "),
            GCharString("112"),
            GCharString("112"),
            GCharString("50"),
            GInt5(0),
            source,
            [],
            [],
            []);
        _activeSnapshots[NpcServerPlayerId] = snapshot;
        if (runtimeServer is not null)
        {
            var player = new RuntimePlayer(NpcServerPlayerId, "(npcserver)", RuntimePlayerKind.NpcServer);
            player.InitializeFromLogin(source);
            runtimeServer.AddPlayer(player, NpcServerPlayerId);
            _activePlayers[NpcServerPlayerId] = player;
        }

        serverList.SendPlayerAdd(PostLoginWorldEntryBoundary.BuildServerListAddPlayerPacket(snapshot));
        return true;
    }

    private void RefreshNpcServerPlayer(ISet<ushort> touched)
    {
        if (!_activeSnapshots.TryGetValue(NpcServerPlayerId, out var snapshot) ||
            snapshot.Type != PlayerSessionType.NpcServer)
            return;

        var settings = EffectiveAccountSettings();
        var source = snapshot.LoginPropertySource with
        {
            Nickname = BuildNpcServerNickname(settings),
            HeadImage = settings.GetString("staffhead", "head25.png")
        };
        var updated = snapshot with
        {
            NicknameProperty = GCharString(source.Nickname),
            LoginPropertySource = source
        };
        _activeSnapshots[NpcServerPlayerId] = updated;
        BroadcastToRemoteControls(BuildRcAddPlayer(updated), touched);
    }

    private static string BuildNpcServerNickname(IAccountLoadSettings settings)
    {
        var nicknameBase = settings.GetString("nickname", "NPC-Server");
        if (string.IsNullOrWhiteSpace(nicknameBase))
            nicknameBase = "NPC-Server";
        return nicknameBase.Trim() + " (Server)";
    }

    private IReadOnlyList<ActivePlayerSession> BuildActiveSessions() =>
        _activePlayers.Values
            .Select(player => new ActivePlayerSession(
                player.Id,
                player.AccountName,
                player.Kind switch
                {
                    RuntimePlayerKind.RemoteControl => PlayerSessionType.RemoteControl2,
                    RuntimePlayerKind.NpcControl => PlayerSessionType.NpcControl,
                    _ => PlayerSessionType.Client3
                },
                TimeSpan.Zero))
            .ToArray();

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
        player.ClientVersion = session.LoginPacket?.VersionId ?? ClientVersionId.Client21;
        player.InitializeFromLogin(snapshot.LoginPropertySource);
        runtimeServer.AddPlayer(player, session.Id);
        var levelName = string.IsNullOrWhiteSpace(player.CurrentLevelName)
            ? "onlinestartlocal.nw"
            : player.CurrentLevelName;
        player.JoinLevel(GetOrCreateLevel(levelName));
        _activePlayers[session.Id] = player;
        EnsureDecoder(session);
        _activeFramers[session.Id] = new ClientPacketStreamFramer(new ClientPacketParseOptions(StripRawDataTrailingNewline: true));
    }

    private void EnsureDecoder(ClientSessionSkeleton session)
    {
        if (_activeDecoders.ContainsKey(session.Id))
            return;

        _activeDecoders[session.Id] = new InboundPacketDecoder(session.InboundEncryptionGeneration, session.LoginPacket?.EncryptionKey ?? 0);
    }

    private void AdvancePendingDecoder(ushort playerId, ReadOnlySpan<byte> frame)
    {
        if (!_activeDecoders.TryGetValue(playerId, out var decoder))
            return;

        var decoded = decoder.DecodeSocketFrame(frame);
        var warning = decoded.Warnings.Count == 0 ? "" : $"; {string.Join("; ", decoded.Warnings)}";
        Console.WriteLine($"Client session {playerId}: pending frame consumed; frame={frame.Length}; comp=0x{(frame.Length == 0 ? 0 : frame[0]):X2}; decoded={decoded.DecodedPayload.Length}; hex={HexPreview(decoded.DecodedPayload, 24)}{warning}");
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
        account.Status = (int)player.Status;
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
        if (player.Kind == RuntimePlayerKind.NpcServer)
            yield break;

        var clientPacket = BuildOtherPlayerDisconnected(player.Id);
        var controlPacket = RcNcPackets.DeletePlayer(player.Id);
        foreach (var (otherId, session) in _activeSessions.ToArray())
        {
            if (otherId == player.Id)
                continue;

            if (IsRemoteControl(session.Type))
                session.QueuePacket(controlPacket);
            else if (IsClient(session.Type) && player.Kind == RuntimePlayerKind.Client)
                session.QueuePacket(clientPacket);
            else
                continue;

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

    private static byte[] BuildFlagSetPacket(LoginFlag flag)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.FlagSet);
        writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes(flag.Value.Length == 0 ? flag.Name : $"{flag.Name}={flag.Value}"));
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static byte[] BuildServerWarpPacket(ReadOnlySpan<byte> serverPacket)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.ServerWarp);
        writer.WriteBytes(serverPacket);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static byte[] GCharString(string value)
    {
        var writer = new GraalBinaryWriter();
        WriteGCharString(writer, value);
        return writer.ToArray();
    }

    private static byte[] GInt5(uint value)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGInt5(value);
        return writer.ToArray();
    }

    private static byte[] BuildPrivateMessagePacket(ushort senderId, bool massMessage, ReadOnlySpan<byte> message)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.PrivateMessage);
        writer.WriteGShort(senderId);
        writer.WriteBytes("\"\","u8);
        writer.WriteBytes(massMessage ? "\"Mass message:\","u8 : "\"Private message:\","u8);
        writer.WriteBytes(message);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private bool IsNpcServerTarget(ushort targetId) =>
        targetId == NpcServerPlayerId || LoadNpcServerEndpoint()?.Id == targetId;

    private static byte[] BuildNpcServerPrivateMessagePacket(ushort npcServerId)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.PrivateMessage);
        writer.WriteGShort(npcServerId);
        writer.WriteBytes("\"\","u8);
        writer.WriteBytes("\"I am the npcserver for\",\"this game server. Almost\",\"all npc actions are controlled\",\"by me.\""u8);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private IEnumerable<ClientSessionOutbound> ExchangeLoginPlayerProps(
        ClientSessionSkeleton joiningSession,
        PostLoginPlayerSnapshot joiningSnapshot)
    {
        if (IsRemoteControl(joiningSession.Type))
        {
            joiningSession.QueuePacket(BuildRcAddPlayer(joiningSnapshot));
            foreach (var (otherId, otherSnapshot) in _activeSnapshots.ToArray())
            {
                if (otherId == joiningSession.Id || otherSnapshot.Type == PlayerSessionType.NpcControl)
                    continue;

                joiningSession.QueuePacket(BuildRcAddPlayer(otherSnapshot));

                if (!_activeSessions.TryGetValue(otherId, out var otherSession) || !IsRemoteControl(otherSession.Type))
                    continue;

                otherSession.QueuePacket(BuildRcAddPlayer(joiningSnapshot));
                var outbound = FlushOutboundBytes(otherSession);
                if (outbound.Length != 0)
                    yield return new ClientSessionOutbound(otherId, outbound);
            }

            yield break;
        }

        foreach (var (otherId, otherSnapshot) in _activeSnapshots.ToArray())
        {
            if (otherId == joiningSession.Id)
                continue;

            if (!_activeSessions.TryGetValue(otherId, out var otherSession))
                continue;

            if (IsClient(otherSnapshot.Type))
                joiningSession.QueuePacket(BuildOtherPlayerProps(otherSnapshot));

            if (!IsClient(joiningSnapshot.Type) && !IsRemoteControl(otherSession.Type))
                continue;

            otherSession.QueuePacket(IsRemoteControl(otherSession.Type)
                ? BuildRcAddPlayer(joiningSnapshot)
                : BuildOtherPlayerProps(joiningSnapshot));
            var outbound = FlushOutboundBytes(otherSession);
            if (outbound.Length != 0)
                yield return new ClientSessionOutbound(otherId, outbound);
        }
    }

    private IEnumerable<ClientSessionOutbound> BuildControlLoginBroadcasts(
        ClientSessionSkeleton joiningSession,
        PostLoginPlayerSnapshot joiningSnapshot)
    {
        if (IsRemoteControl(joiningSession.Type))
        {
            foreach (var (playerId, session) in _activeSessions.ToArray())
            {
                if (!IsRemoteControl(session.Type))
                    continue;

                session.QueuePacket(RcNcPackets.RcChat($"New RC: {joiningSnapshot.LoginPropertySource.AccountName}"));
                if (playerId == joiningSession.Id)
                    continue;

                var outbound = FlushOutboundBytes(session);
                if (outbound.Length != 0)
                    yield return new ClientSessionOutbound(playerId, outbound);
            }

            yield break;
        }

        if ((joiningSession.Type & PlayerSessionType.AnyNpcControl) == 0)
            yield break;

        joiningSession.QueuePacket(RcNcPackets.RcChat($"Welcome to the NPC-Server for {worldEntryOptions?.AccountLoginOptions.ServerName ?? "GServer"}"));

        var touched = new HashSet<ushort>();
        SendDatabaseNpcList(joiningSession.Id, touched);

        foreach (var (otherId, otherSnapshot) in _activeSnapshots.ToArray())
        {
            if (otherId == joiningSession.Id || (otherSnapshot.Type & PlayerSessionType.AnyNpcControl) == 0)
                continue;

            joiningSession.QueuePacket(RcNcPackets.RcChat($"New NC: {otherSnapshot.LoginPropertySource.AccountName}"));
        }

        foreach (var (playerId, session) in _activeSessions.ToArray())
        {
            if (playerId == joiningSession.Id || (session.Type & PlayerSessionType.AnyNpcControl) == 0)
                continue;

            session.QueuePacket(RcNcPackets.RcChat($"New NC: {joiningSnapshot.LoginPropertySource.AccountName}"));
            var outbound = FlushOutboundBytes(session);
            if (outbound.Length != 0)
                yield return new ClientSessionOutbound(playerId, outbound);
        }
    }

    private IEnumerable<ClientSessionOutbound> EndDuplicateSessions(
        IReadOnlyList<DuplicateSessionDisconnect> duplicates,
        ushort joiningPlayerId)
    {
        foreach (var duplicate in duplicates)
        {
            if (duplicate.SessionId == joiningPlayerId)
                continue;

            if (_activeSessions.TryGetValue(duplicate.SessionId, out var duplicateSession))
            {
                duplicateSession.QueueDisconnect(duplicate.Message);
                var outbound = FlushOutboundBytes(duplicateSession);
                if (outbound.Length != 0)
                    yield return new ClientSessionOutbound(duplicate.SessionId, outbound);
            }

            foreach (var broadcast in EndClientSession(duplicate.SessionId).Broadcasts)
                yield return broadcast;
        }
    }

    private static byte[] BuildOtherPlayerProps(PostLoginPlayerSnapshot snapshot)
    {
        var payload = PlayerPropertySerializer.SerializeOtherPlayerPropsPayload(
            snapshot.LoginPropertySource,
            GetLoginPropertySet.All);
        return PlayerPropertySerializer.BuildOtherPlayerPropsPacket(snapshot.PlayerId, payload, appendNewline: true);
    }

    private static byte[] BuildRcAddPlayer(PostLoginPlayerSnapshot snapshot) =>
        RcNcPackets.AddPlayer(
            snapshot.PlayerId,
            snapshot.LoginPropertySource.AccountName,
            snapshot.LoginPropertySource.CurrentLevel,
            snapshot.LoginPropertySource.StatusMessage,
            snapshot.LoginPropertySource.Nickname,
            snapshot.LoginPropertySource.CommunityName,
            IsControl(snapshot.Type) || snapshot.Type == PlayerSessionType.NpcServer ? (byte)1 : null);

    private string FormatScriptOutput(string scriptName, string line) =>
        EffectiveAccountSettings().GetString("scriptcall", "echo").Trim().Equals("debug", StringComparison.OrdinalIgnoreCase)
            ? $"GS2 {scriptName}: {line}"
            : line;

    private static bool LooksLikeGs1ClientScript(string source) =>
        source.Contains("setplayerprop #", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("setstring ", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("timeout=", StringComparison.OrdinalIgnoreCase);

    private static bool IsClient(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyClient) != 0;

    private static bool IsRemoteControl(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyRemoteControl) != 0;

    private static bool IsControl(PlayerSessionType type) =>
        (type & (PlayerSessionType.AnyRemoteControl | PlayerSessionType.AnyNpcControl)) != 0;

    private sealed record NcWeaponSource(string Name, string Image, string Source);

    private sealed class ClientSessionSink(ClientSessionSkeleton session) : ILiveWorldSessionSink
    {
        public ushort PlayerId => session.Id;

        public void QueuePacket(byte[] packet)
        {
            session.QueuePacket(packet);
        }
    }

    private sealed class OverlayAccountLoadSettings(
        IAccountLoadSettings current,
        IAccountLoadSettings? fallback) : IAccountLoadSettings
    {
        public bool Exists(string key) =>
            current.Exists(key) || fallback?.Exists(key) == true;

        public string GetString(string key, string defaultValue) =>
            current.Exists(key)
                ? current.GetString(key, defaultValue)
                : fallback?.GetString(key, defaultValue) ?? defaultValue;

        public float GetFloat(string key, float defaultValue) =>
            current.Exists(key)
                ? current.GetFloat(key, defaultValue)
                : fallback?.GetFloat(key, defaultValue) ?? defaultValue;
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
        else if (session.LoginPacket?.Type is PlayerSessionType.NpcControl or PlayerSessionType.RemoteControl)
            queue.SetCodec(EncryptionGeneration.Gen3, key: 0);
        else if (session.LoginPacket?.Type == PlayerSessionType.Web)
            queue.SetCodec(EncryptionGeneration.Gen1, key: 0);

        _outboundQueues[session.Id] = queue;
        return queue;
    }
}
