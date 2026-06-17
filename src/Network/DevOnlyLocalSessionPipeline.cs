using GServ.Game;
using GServ.Protocol;

namespace GServ.Network;

public sealed record DevOnlyLocalServerOptions(
    bool EnableDevOnlyAuth,
    string LevelName,
    ushort PlayerId = 7,
    string ServerName = "GServharp Dev Only",
    float StartX = 30.0f,
    float StartY = 30.0f,
    float StartZ = 0.0f,
    IReadOnlyList<string>? AllowedVersions = null,
    string AllowedVersionText = "6.037")
{
    public IReadOnlyList<string> EffectiveAllowedVersions =>
        AllowedVersions ?? ["G3D0311C"];
}

public enum DevOnlyLocalStopPoint
{
    BeforeRuntimeWorldSimulation,
    MissingLevel,
    Rejected
}

public sealed record DevOnlyLocalSessionResult(
    bool Accepted,
    SessionLifecycle Lifecycle,
    DevOnlyLocalStopPoint StopPoint,
    byte[] OutboundBytes,
    IReadOnlyList<string> Log);

public sealed class DevOnlyLocalSessionPipeline(
    DevOnlyLocalServerOptions options,
    NwLevelFileLoader levelLoader)
{
    public DevOnlyLocalSessionResult ProcessLengthPrefixedInput(ReadOnlySpan<byte> input)
    {
        var connection = CreateConnection();
        return connection.ProcessLengthPrefixedInput(input);
    }

    public DevOnlyLocalSessionConnection CreateConnection() =>
        new(this, options.PlayerId);

    internal DevOnlyLocalSessionResult ProcessConnectionInput(
        ClientSessionSkeleton session,
        RuntimePlayer runtimePlayer,
        InboundPacketDecoder? inboundDecoder,
        ClientPacketStreamFramer? inboundFramer,
        List<string> log,
        ReadOnlySpan<byte> input)
    {
        if (!options.EnableDevOnlyAuth)
            throw new InvalidOperationException("Dev-only auth must be explicitly enabled.");

        foreach (var frame in PacketFramer.ReadLengthPrefixedFrames(input))
        {
            if (session.LoginPacket is null)
            {
                if (!session.ReceiveLoginPacket(frame.Span))
                    return Finish(session, false, DevOnlyLocalStopPoint.Rejected, log);

                RunDevOnlyLoginFlow(session, log);
                return Finish(session, session.Lifecycle == SessionLifecycle.DynamicLevelPayloadSent, ToStopPoint(session), log);
            }

            IReadOnlyList<FramedPacket> packets;
            try
            {
                if (inboundDecoder is null)
                    throw new NotSupportedException("Inbound decoder is not initialized for this post-login frame.");

                if (session.InboundEncryptionGeneration == EncryptionGeneration.Gen3)
                {
                    var decoded = inboundDecoder.DecodeSocketFramePayload(frame.Span);
                    packets = decoded.Packets.Select(packet => new FramedPacket(null, packet)).ToArray();
                }
                else
                {
                    if (inboundFramer is null)
                        throw new NotSupportedException("Inbound client packet framer is not initialized for this post-login frame.");

                    var decoded = inboundDecoder.DecodeSocketFrame(frame.Span);
                    foreach (var warning in decoded.Warnings)
                        log.Add(warning);
                    packets = inboundFramer.Parse(decoded.DecodedPayload);
                }
                log.Add($"Decoded inbound {session.InboundEncryptionGeneration.ToString().ToLowerInvariant()} frame into {packets.Count} packet(s).");
            }
            catch (NotSupportedException ex)
            {
                log.Add($"Inbound post-login frame decode blocked: {ex.Message}");
                log.Add("Unsupported post-login frame received by dev-only shell; continuous loop stopped before gameplay/runtime packet handling.");
                return Finish(session, session.Lifecycle == SessionLifecycle.DynamicLevelPayloadSent, ToStopPoint(session), log);
            }

            foreach (var packet in packets)
            {
                if (TryProcessDecodedPostLoginPacket(session, runtimePlayer, log, packet.Payload.Span))
                    continue;

                log.Add("Unsupported post-login frame received by dev-only shell; continuous loop stopped before gameplay/runtime packet handling.");
                return Finish(session, session.Lifecycle == SessionLifecycle.DynamicLevelPayloadSent, ToStopPoint(session), log);
            }

            return Finish(session, session.Lifecycle == SessionLifecycle.DynamicLevelPayloadSent, ToStopPoint(session), log);
        }

        return Finish(session, false, DevOnlyLocalStopPoint.Rejected, log);
    }

    private void RunDevOnlyLoginFlow(ClientSessionSkeleton session, List<string> log)
    {
        var preWorld = new PreWorldAuthBoundary(new PreWorldAuthOptions(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: true,
            AllowedVersions: options.EffectiveAllowedVersions,
            AllowedVersionText: options.AllowedVersionText));

        var preWorldResult = preWorld.Begin(session);
        if (!preWorldResult.Accepted)
            return;

        var accountName = session.LoginPacket!.AccountName.StartsWith("pc:", StringComparison.Ordinal)
            ? session.LoginPacket.AccountName
            : $"pc:{session.LoginPacket.AccountName}";

        log.Add($"DEV-ONLY auth accepted for {accountName}; this is not production server-list behavior.");
        session.ReceiveServerListAuthResponse(new ServerListVerifyAccount2Response(
            accountName,
            session.Id,
            session.Type,
            "SUCCESS"));

        var account = DevOnlyLocalAccount.FromLogin(session, accountName, options);
        var continuation = PlayerSendLoginContinuation.Begin(
            session,
            new PlayerSendLoginAccount(
                account.AccountName,
                IsBanned: false,
                BanReason: string.Empty,
                HasModifyStaffAccountRight: false,
                IsStaff: false,
                IsAdminIp: true,
                AdminIps: ["0.0.0.0"],
                IsGuest: false),
            new PlayerSendLoginOptions(OnlyStaff: false, options.ServerName, ActiveSessions: []));
        if (!continuation.Accepted)
            return;

        PostLoginWorldEntryBoundary.BeginClient(session, account.ToPostLoginSnapshot());

        var loaded = levelLoader.TryLoad(options.LevelName);
        if (!loaded.Success)
        {
            WarpWorldEntryBoundary.BeginSetLevel(
                session,
                new DevOnlyLevelLookup(null),
                new LevelWarpRequest(
                    options.LevelName,
                    options.StartX,
                    options.StartY,
                    options.StartZ,
                    session.LoginPacket.VersionId,
                    ModTime: 0));
            log.Add($"Level {options.LevelName} was not loaded: {loaded.Status}.");
            return;
        }

        log.Add($"Loaded .nw level {loaded.LevelName} with modTime {loaded.ModTime}.");
        WarpWorldEntryBoundary.BeginSetLevel(
            session,
            new DevOnlyLevelLookup(new LevelEntrySnapshot(loaded.LevelName)),
            new LevelWarpRequest(
                loaded.LevelName,
                options.StartX,
                options.StartY,
                options.StartZ,
                session.LoginPacket.VersionId,
                ModTime: 0));

        if (session.Lifecycle != SessionLifecycle.ReadyForLevelRuntime)
            return;

        var staticPayload = loaded.ToModernStaticPayload(chest => account.Chests.Contains(chest, StringComparer.Ordinal));
        SendLevelBoundary.BeginModern(
            session,
            ModernLevelPayload.FromNwStatic(staticPayload),
            new SendLevelRequest(RequestedModTime: loaded.ModTime, CachedLevelModTime: 0, FromAdjacent: false));
    }

    private static DevOnlyLocalStopPoint ToStopPoint(ClientSessionSkeleton session) =>
        session.Lifecycle switch
        {
            SessionLifecycle.DynamicLevelPayloadSent => DevOnlyLocalStopPoint.BeforeRuntimeWorldSimulation,
            SessionLifecycle.ReadyForLevelWarp => DevOnlyLocalStopPoint.MissingLevel,
            _ => DevOnlyLocalStopPoint.Rejected
        };

    private static bool TryProcessDecodedPostLoginPacket(
        ClientSessionSkeleton session,
        RuntimePlayer runtimePlayer,
        List<string> log,
        ReadOnlySpan<byte> packet)
    {
        if (packet.IsEmpty)
            return false;

        var packetId = (byte)(packet[0] - 32);
        if (packetId != (byte)PlayerToServerPacketId.PlayerProps)
            return false;

        var parsed = IncomingPlayerPropsParser.Parse(packet[1..]);
        foreach (var update in parsed.Updates)
        {
            try
            {
                RuntimePlayerPropsApplier.ApplyConfirmed(runtimePlayer, [update]);
            }
            catch (NotSupportedException ex)
            {
                log.Add($"{CppNameOf(update.PropertyId)} was parsed with source-confirmed bytes, but its runtime side effects are not ported yet: {ex.Message}");
                return true;
            }
        }

        if (!parsed.Success)
        {
            log.Add($"Decoded PLI_PLAYERPROPS stopped at unconfirmed property {(byte)parsed.UnsupportedPropertyId!.Value}; no gameplay side effects were run.");
            return true;
        }

        log.Add(
            $"Applied decoded PLI_PLAYERPROPS to dev-only runtime player {session.Id}: " +
            $"x={runtimePlayer.PixelX}, y={runtimePlayer.PixelY}, z={runtimePlayer.PixelZ}, " +
            $"sprite={runtimePlayer.Sprite}, level={runtimePlayer.CurrentLevelName}, gani={runtimePlayer.Gani}.");
        return true;
    }

    private static string CppNameOf(PlayerPropertyId propertyId) =>
        propertyId switch
        {
            PlayerPropertyId.Nickname => "PLPROP_NICKNAME",
            PlayerPropertyId.CarryNpc => "PLPROP_CARRYNPC",
            PlayerPropertyId.GmapLevelX => "PLPROP_GMAPLEVELX",
            PlayerPropertyId.GmapLevelY => "PLPROP_GMAPLEVELY",
            PlayerPropertyId.Status => "PLPROP_STATUS",
            _ => $"PLPROP_{(byte)propertyId}"
        };

    private static DevOnlyLocalSessionResult Finish(
        ClientSessionSkeleton session,
        bool accepted,
        DevOnlyLocalStopPoint stopPoint,
        List<string> log)
    {
        var queue = new GraalFileQueue();
        if (session.LoginPacket is { } login)
            ConfigureSocketFlush(queue, login, log);

        queue.AddPacket(session.TakeOutboundBytes());
        return new DevOnlyLocalSessionResult(
            accepted,
            session.Lifecycle,
            stopPoint,
            queue.FlushSocket(forceSendFiles: true),
            log);
    }

    private static void ConfigureSocketFlush(
        GraalFileQueue queue,
        LoginPacket login,
        List<string> log)
    {
        if (login.Type is PlayerSessionType.Client3 or PlayerSessionType.RemoteControl2 && login.EncryptionKey is { } key)
        {
            queue.SetCodec(EncryptionGeneration.Gen5, key);
            log.Add($"DEV-ONLY socket flush used Gen5 with login key {key}.");
            return;
        }

        if (login.Type == PlayerSessionType.Web)
        {
            queue.SetCodec(EncryptionGeneration.Gen1, key: 0);
            log.Add("DEV-ONLY socket flush used Gen1 for web client.");
            return;
        }

        throw new NotSupportedException($"Dev-only socket flush for {login.Type} is not source-confirmed yet.");
    }

    private sealed record DevOnlyLevelLookup(LevelEntrySnapshot? Level) : ILevelLookup
    {
        public LevelEntrySnapshot? FindLevel(string levelName) =>
            Level is not null && string.Equals(Level.LevelName, levelName, StringComparison.Ordinal)
                ? Level
                : null;
    }

    private sealed record DevOnlyLocalAccount(
        ushort PlayerId,
        PlayerSessionType Type,
        string AccountName,
        string Nickname,
        string LevelName,
        int PixelX,
        int PixelY,
        short PixelZ,
        string Platform,
        IReadOnlyList<string> Chests)
    {
        public static DevOnlyLocalAccount FromLogin(
            ClientSessionSkeleton session,
            string accountName,
            DevOnlyLocalServerOptions options)
        {
            var nickname = accountName.StartsWith("pc:", StringComparison.Ordinal)
                ? accountName[3..]
                : accountName;

            return new DevOnlyLocalAccount(
                session.Id,
                session.Type,
                accountName,
                nickname,
                options.LevelName,
                unchecked((int)(options.StartX * 16)),
                unchecked((int)(options.StartY * 16)),
                unchecked((short)(options.StartZ * 16)),
                session.LoginPacket?.Platform ?? string.Empty,
                []);
        }

        public PostLoginPlayerSnapshot ToPostLoginSnapshot()
        {
            var source = ToPropertySource();
            return new PostLoginPlayerSnapshot(
                PlayerId,
                Type,
                Property(PlayerPropertyId.AccountName, source),
                Property(PlayerPropertyId.Nickname, source),
                Property(PlayerPropertyId.CurrentLevel, source),
                Property(PlayerPropertyId.X, source),
                Property(PlayerPropertyId.Y, source),
                Property(PlayerPropertyId.Alignment, source),
                Property(PlayerPropertyId.IpAddress, source),
                source,
                SendLoginPropertySet.All,
                PlayerFlags: [],
                ServerFlags: []);
        }

        private PlayerPropertySource ToPropertySource() =>
            new(
                Nickname,
                MaxPower: 3,
                Hitpoints: 3.0f,
                Rupees: 0,
                Arrows: 5,
                Bombs: 10,
                GlovePower: 1,
                SwordPower: 1,
                SwordImage: "sword1.png",
                ShieldPower: 1,
                ShieldImage: "shield1.png",
                Gani: "idle",
                HeadImage: "head0.png",
                ChatMessage: string.Empty,
                Colors: [2, 0, 10, 4, 18],
                PlayerId,
                PixelX,
                PixelY,
                Sprite: 2,
                Status: 20,
                CarrySprite: 0,
                LevelName,
                HorseImage: string.Empty,
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
                AccountName,
                BodyImage: "body.png",
                EloRating: 1500,
                EloDeviation: 350,
                GaniAttributes: new Dictionary<int, string>(),
                Platform,
                TextCodePage: 0,
                CommunityName: Nickname,
                PixelZ);

        private static byte[] Property(PlayerPropertyId id, PlayerPropertySource source) =>
            PlayerPropertySerializer.SerializeConfirmedLoginSubset(source, [id])
                .Skip(1)
                .ToArray();
    }
}

public sealed class DevOnlyLocalSessionConnection
{
    private readonly DevOnlyLocalSessionPipeline _pipeline;
    private readonly ClientSessionSkeleton _session;
    private readonly RuntimePlayer _runtimePlayer;
    private readonly List<string> _log = [];
    private InboundPacketDecoder? _inboundDecoder;
    private ClientPacketStreamFramer? _inboundFramer;

    internal DevOnlyLocalSessionConnection(DevOnlyLocalSessionPipeline pipeline, ushort playerId)
    {
        _pipeline = pipeline;
        _session = new ClientSessionSkeleton(playerId);
        _runtimePlayer = new RuntimePlayer(playerId, string.Empty, RuntimePlayerKind.Client);
    }

    public DevOnlyLocalSessionResult ProcessLengthPrefixedInput(ReadOnlySpan<byte> input)
    {
        var result = _pipeline.ProcessConnectionInput(_session, _runtimePlayer, _inboundDecoder, _inboundFramer, _log, input);
        if (_inboundDecoder is null && _session.LoginPacket is { } login)
        {
            _inboundDecoder = new InboundPacketDecoder(_session.InboundEncryptionGeneration, login.EncryptionKey ?? 0);
            _inboundFramer = new ClientPacketStreamFramer(new ClientPacketParseOptions(StripRawDataTrailingNewline: true));
        }
        return result;
    }
}
