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
    private const int AdminMessageRight = 0x00200;

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
            LoginWorldEntry.Complete(session, worldEntryOptions with
            {
                AccountLoginOptions = worldEntryOptions.AccountLoginOptions with
                {
                    ActiveSessions = BuildActiveSessions(),
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
            if (IsRemoteControl(session.Type) &&
                HandleRemoteControlPacket((PlayerToServerPacketId)packetId, packet.Payload.Span[1..], session, player, touched))
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
                continue;

            QueueSelfPacket(targetId, packet, touched);
        }
    }

    private bool HandleRemoteControlPacket(
        PlayerToServerPacketId packetId,
        ReadOnlySpan<byte> payload,
        ClientSessionSkeleton session,
        RuntimePlayer sender,
        ISet<ushort> touched)
    {
        switch (packetId)
        {
            case PlayerToServerPacketId.RcChat:
                BroadcastToRemoteControls(RcNcPackets.RcChat($"{sender.AccountName}: {ReadAsciiPayload(payload)}"), touched);
                return true;
            case PlayerToServerPacketId.RcAdminMessage:
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
                HandlePrivateAdminMessage(payload, session.Id, sender.AccountName, touched);
                return true;
            case PlayerToServerPacketId.RcServerOptionsGet:
                QueueSelfPacket(session.Id, RcNcPackets.ServerOptionsGet(ReadConfigFile("serveroptions.txt")), touched);
                return true;
            case PlayerToServerPacketId.RcFolderConfigGet:
                QueueSelfPacket(session.Id, RcNcPackets.FolderConfigGet(ReadConfigFile("foldersconfig.txt")), touched);
                return true;
            case PlayerToServerPacketId.RcServerFlagsGet:
                QueueSelfPacket(session.Id, RcNcPackets.ServerFlagsGet([]), touched);
                return true;
            case PlayerToServerPacketId.RcFileBrowserStart:
                HandleFileBrowserStart(session.Id, touched);
                return true;
            case PlayerToServerPacketId.RcFileBrowserChangeDirectory:
                HandleFileBrowserChangeDirectory(session.Id, ReadAsciiPayload(payload), touched);
                return true;
            case PlayerToServerPacketId.RcFileBrowserEnd:
                return true;
            default:
                return false;
        }
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
        QueueSelfPacket(playerId, RcNcPackets.FileBrowserDirList(string.Join(",", folders)), touched);
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
                entries.Add(new RcFileBrowserEntry(
                    info.Name,
                    rights,
                    unchecked((uint)Math.Min(info.Length, uint.MaxValue)),
                    unchecked((uint)new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds())));
            }
        }

        return RcNcPackets.FileBrowserDir(normalized, entries);
    }

    private string ReadConfigFile(string fileName)
    {
        var root = worldEntryOptions?.AccountFileSystem.ServerPath;
        if (root is null)
            return "";

        var path = Path.Combine(root, "config", fileName);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r", "", StringComparison.Ordinal) : "";
    }

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

    private IReadOnlyList<ActivePlayerSession> BuildActiveSessions() =>
        _activePlayers.Values
            .Where(player => player.IsClient)
            .Select(player => new ActivePlayerSession(
                player.Id,
                player.AccountName,
                PlayerSessionType.Client3,
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

    private IEnumerable<ClientSessionOutbound> ExchangeLoginPlayerProps(
        ClientSessionSkeleton joiningSession,
        PostLoginPlayerSnapshot joiningSnapshot)
    {
        if (IsRemoteControl(joiningSession.Type))
        {
            foreach (var (otherId, otherSnapshot) in _activeSnapshots.ToArray())
            {
                if (otherId == joiningSession.Id || otherSnapshot.Type == PlayerSessionType.NpcControl)
                    continue;

                joiningSession.QueuePacket(BuildRcAddPlayer(otherSnapshot));

                if (!_activeSessions.TryGetValue(otherId, out var otherSession) || !IsRemoteControl(otherSession.Type))
                    continue;

                otherSession.QueuePacket(BuildRcAddPlayer(joiningSnapshot));
                otherSession.QueuePacket(RcNcPackets.RcChat($"New RC: {joiningSnapshot.LoginPropertySource.AccountName}"));
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

            otherSession.QueuePacket(IsRemoteControl(otherSession.Type)
                ? BuildRcAddPlayer(joiningSnapshot)
                : BuildOtherPlayerProps(joiningSnapshot));
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

    private static byte[] BuildRcAddPlayer(PostLoginPlayerSnapshot snapshot) =>
        RcNcPackets.AddPlayer(
            snapshot.PlayerId,
            snapshot.LoginPropertySource.AccountName,
            snapshot.LoginPropertySource.CurrentLevel,
            snapshot.LoginPropertySource.StatusMessage,
            snapshot.LoginPropertySource.Nickname,
            snapshot.LoginPropertySource.CommunityName);

    private static bool IsClient(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyClient) != 0;

    private static bool IsRemoteControl(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyRemoteControl) != 0;

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
