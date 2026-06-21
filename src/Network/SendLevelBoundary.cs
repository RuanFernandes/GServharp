using Preagonal.GServer.Game;
using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed record LevelLayerPayload(int LayerIndex, byte[] Packet);
public sealed record LevelBoardChangePayload(long ModTime, byte[] BoardString);
public sealed record LevelChestPayload(bool HasChest, byte X, byte Y, byte ItemIndex, byte SignIndex);
public sealed record LevelHorsePayload(byte[] HorseString);
public sealed record LevelBaddyPayload(byte BaddyId, byte[] Props);
public sealed record LevelRuntimeContinuationPayload(
    string? GmapName,
    bool HasMapContext,
    bool IsLevelLeader,
    bool IsSingleplayer,
    uint NewWorldTime,
    byte[] NpcsPacket);
public sealed record NearbyLevelPlayerSnapshot(
    ushort PlayerId,
    bool IsClient,
    bool IsOnSameLevel,
    string? MapKey,
    string? Group,
    int MapX,
    int MapY,
    byte[] OtherPlayerPropsPacket);
public sealed record LevelEntryPlayerSyncPayload(
    bool IsSingleplayer,
    bool HasMapContext,
    bool IsGroupMap,
    string? MapKey,
    string? PlayerGroup,
    int PlayerMapX,
    int PlayerMapY,
    byte[] SelfPropsPacket,
    IReadOnlyList<NearbyLevelPlayerSnapshot> NearbyPlayers);
public sealed record LevelEntryBroadcast(ushort PlayerId, byte[] Packet);

public sealed record ModernLevelPayload(
    string LevelName,
    long LevelModTime,
    byte[] BoardPacket,
    IReadOnlyList<LevelLayerPayload> Layers,
    byte[] LinksPacket,
    byte[] SignsPacket,
    IReadOnlyList<LevelBoardChangePayload>? BoardChanges = null,
    IReadOnlyList<LevelChestPayload>? Chests = null,
    IReadOnlyList<LevelHorsePayload>? Horses = null,
    IReadOnlyList<LevelBaddyPayload>? Baddies = null,
    LevelRuntimeContinuationPayload? RuntimeContinuation = null,
    LevelEntryPlayerSyncPayload? PlayerSync = null)
{
    public static ModernLevelPayload FromNwStatic(NwLevelStaticPayload payload) =>
        new(
            payload.LevelName,
            payload.LevelModTime,
            payload.BoardPacket,
            payload.Layers
                .Select(layer => new LevelLayerPayload(layer.LayerIndex, layer.Packet))
                .ToArray(),
            payload.LinksPacket,
            payload.SignsPacket,
            Chests: payload.Chests
                .Select(chest => new LevelChestPayload(
                    chest.HasChest,
                    chest.X,
                    chest.Y,
                    chest.ItemIndex,
                    chest.SignIndex))
                .ToArray());
}

public sealed record SendLevelRequest(
    long RequestedModTime,
    long CachedLevelModTime,
    bool FromAdjacent);

public enum SendLevelStopPoint
{
    BeforeDynamicLevelRuntime,
    BeforeGmapCorrection,
    BeforeNearbyPlayerProps,
    BeforeRuntimeSimulation
}

public sealed record SendLevelBoundaryResult(
    bool Accepted,
    SendLevelStopPoint StopPoint,
    IReadOnlyList<LevelEntryBroadcast>? Broadcasts = null);

public static class SendLevelBoundary
{
    private const int BoardRawDataLength = 1 + (64 * 64 * 2) + 1;

    public static SendLevelBoundaryResult BeginModern(
        ClientSessionSkeleton session,
        ModernLevelPayload level,
        SendLevelRequest request)
    {
        if (session.Lifecycle != SessionLifecycle.ReadyForLevelRuntime)
            throw new InvalidOperationException("sendLevel boundary requires ReadyForLevelRuntime.");

        QueuePacket(session, WarpPackets.BuildLevelName(level.LevelName));

        var cachedLevelModTime = request.CachedLevelModTime;
        var requestedModTime = request.RequestedModTime == -1
            ? level.LevelModTime
            : request.RequestedModTime;

        if (cachedLevelModTime == 0)
        {
            if (requestedModTime != level.LevelModTime)
            {
                QueuePacket(session, RawDataHeader(BoardRawDataLength));
                QueuePacket(session, level.BoardPacket);

                foreach (var layer in level.Layers)
                {
                    if (layer.LayerIndex == 0)
                        continue;

                    QueuePacket(session, RawDataHeader(layer.Packet.Length));
                    QueuePacket(session, layer.Packet);
                }
            }

            QueuePacket(session, LevelModTime(level.LevelModTime));
            QueuePacket(session, level.LinksPacket);
            QueuePacket(session, level.SignsPacket);
        }

        session.MarkLevelPayloadSent();

        if (!request.FromAdjacent)
        {
            QueuePacket(session, BoardChangesPacket(level.BoardChanges ?? Array.Empty<LevelBoardChangePayload>(), cachedLevelModTime));
            QueuePacket(session, ChestPacket(level.Chests ?? Array.Empty<LevelChestPayload>()));
            QueuePacket(session, HorsePacket(level.Horses ?? Array.Empty<LevelHorsePayload>()));
            QueuePacket(session, BaddyPacket(level.Baddies ?? Array.Empty<LevelBaddyPayload>()));
        }

        session.MarkDynamicLevelPayloadSent();
        if (level.RuntimeContinuation is not { } runtime)
            return new SendLevelBoundaryResult(true, SendLevelStopPoint.BeforeGmapCorrection);

        if (!string.IsNullOrEmpty(runtime.GmapName))
            QueuePacket(session, WarpPackets.BuildLevelName(runtime.GmapName));

        QueuePacket(session, GhostIcon(false));

        var shouldSendMapScopedPackets = !request.FromAdjacent || runtime.HasMapContext;
        if (shouldSendMapScopedPackets && (runtime.IsLevelLeader || runtime.IsSingleplayer))
            QueuePacket(session, IsLeader());

        QueuePacket(session, NewWorldTime(runtime.NewWorldTime));

        if (shouldSendMapScopedPackets)
        {
            QueuePacket(session, SetActiveLevel(runtime.GmapName ?? level.LevelName));
            QueuePacket(session, runtime.NpcsPacket);
        }

        session.MarkLevelRuntimePacketsSent();
        if (level.PlayerSync is not { } playerSync)
            return new SendLevelBoundaryResult(true, SendLevelStopPoint.BeforeNearbyPlayerProps);

        var broadcasts = SynchronizeNearbyPlayerProps(session, playerSync);
        session.MarkLevelEntryPlayerPropsSynchronized();
        return new SendLevelBoundaryResult(true, SendLevelStopPoint.BeforeRuntimeSimulation, broadcasts);
    }

    private static byte[] RawDataHeader(int length)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.RawData);
        writer.WriteGInt(unchecked((uint)length));
        return writer.ToArray();
    }

    private static byte[] LevelModTime(long modTime)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.LevelModTime);
        writer.WriteGInt5(unchecked((uint)modTime));
        return writer.ToArray();
    }

    private static byte[] BoardChangesPacket(
        IReadOnlyList<LevelBoardChangePayload> changes,
        long cachedLevelModTime)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.LevelBoard);

        foreach (var change in changes)
        {
            if (change.ModTime >= cachedLevelModTime)
                writer.WriteBytes(change.BoardString);
        }

        return writer.ToArray();
    }

    private static byte[] ChestPacket(IReadOnlyList<LevelChestPayload> chests)
    {
        var writer = new GraalBinaryWriter();

        foreach (var chest in chests)
        {
            writer.WriteGChar((byte)ServerToPlayerPacketId.LevelChest);
            writer.WriteGChar((byte)(chest.HasChest ? 1 : 0));
            writer.WriteGChar(chest.X);
            writer.WriteGChar(chest.Y);

            if (!chest.HasChest)
            {
                writer.WriteGChar(chest.ItemIndex);
                writer.WriteGChar(chest.SignIndex);
            }

            writer.WriteByte((byte)'\n');
        }

        return writer.ToArray();
    }

    private static byte[] HorsePacket(IReadOnlyList<LevelHorsePayload> horses)
    {
        var writer = new GraalBinaryWriter();

        foreach (var horse in horses)
        {
            writer.WriteGChar((byte)ServerToPlayerPacketId.HorseAdd);
            writer.WriteBytes(horse.HorseString);
            writer.WriteByte((byte)'\n');
        }

        return writer.ToArray();
    }

    private static byte[] BaddyPacket(IReadOnlyList<LevelBaddyPayload> baddies)
    {
        var writer = new GraalBinaryWriter();

        foreach (var baddy in baddies)
        {
            writer.WriteGChar((byte)ServerToPlayerPacketId.BaddyProps);
            writer.WriteGChar(baddy.BaddyId);
            writer.WriteBytes(baddy.Props);
            writer.WriteByte((byte)'\n');
        }

        return writer.ToArray();
    }

    private static byte[] GhostIcon(bool enabled)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.GhostIcon);
        writer.WriteGChar((byte)(enabled ? 1 : 0));
        return writer.ToArray();
    }

    private static byte[] IsLeader()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.IsLeader);
        return writer.ToArray();
    }

    private static byte[] NewWorldTime(uint newWorldTime)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.NewWorldTime);
        writer.WriteGInt4(newWorldTime);
        return writer.ToArray();
    }

    private static byte[] SetActiveLevel(string levelName)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.SetActiveLevel);
        writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes(levelName));
        return writer.ToArray();
    }

    private static IReadOnlyList<LevelEntryBroadcast> SynchronizeNearbyPlayerProps(
        ClientSessionSkeleton session,
        LevelEntryPlayerSyncPayload sync)
    {
        if (sync.IsSingleplayer)
            return Array.Empty<LevelEntryBroadcast>();

        var broadcasts = new List<LevelEntryBroadcast>();
        foreach (var other in sync.NearbyPlayers)
        {
            if (other.PlayerId == session.Id)
                continue;

            if (ShouldBroadcastSelfProps(sync, other))
                broadcasts.Add(new LevelEntryBroadcast(other.PlayerId, AppendNewline(sync.SelfPropsPacket)));

            if (ShouldSendOtherPropsToJoiningPlayer(sync, other))
                QueuePacket(session, other.OtherPlayerPropsPacket);
        }

        return broadcasts;
    }

    private static bool ShouldBroadcastSelfProps(LevelEntryPlayerSyncPayload sync, NearbyLevelPlayerSnapshot other)
    {
        if (!other.IsClient)
            return false;

        return sync.HasMapContext
            ? IsNearbyOnSameMap(sync, other)
            : other.IsOnSameLevel;
    }

    private static bool ShouldSendOtherPropsToJoiningPlayer(LevelEntryPlayerSyncPayload sync, NearbyLevelPlayerSnapshot other)
    {
        return sync.HasMapContext
            ? other.IsClient && IsNearbyOnSameMap(sync, other)
            : other.IsOnSameLevel;
    }

    private static bool IsNearbyOnSameMap(LevelEntryPlayerSyncPayload sync, NearbyLevelPlayerSnapshot other)
    {
        if (!string.Equals(other.MapKey, sync.MapKey, StringComparison.Ordinal))
            return false;

        if (sync.IsGroupMap && !string.Equals(sync.PlayerGroup, other.Group, StringComparison.Ordinal))
            return false;

        return Math.Abs(other.MapX - sync.PlayerMapX) < 2 &&
            Math.Abs(other.MapY - sync.PlayerMapY) < 2;
    }

    private static void QueuePacket(ClientSessionSkeleton session, byte[] packet)
    {
        if (packet.Length == 0)
            return;

        session.QueuePacket(AppendNewline(packet));
    }

    private static byte[] AppendNewline(byte[] packet)
    {
        if (packet.Length > 0 && packet[^1] == (byte)'\n')
            return packet;

        var output = new byte[packet.Length + 1];
        packet.CopyTo(output, 0);
        output[^1] = (byte)'\n';
        return output;
    }
}
