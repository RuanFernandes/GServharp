using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public enum LevelMapType
{
    BigMap = 0,
    Gmap = 1
}

public sealed record LevelMapSnapshot(LevelMapType Type, string MapName);

public sealed record LevelEntrySnapshot(
    string LevelName,
    LevelMapSnapshot? Map = null,
    byte MapX = 0,
    byte MapY = 0);

public sealed record LevelWarpRequest(
    string LevelName,
    float X,
    float Y,
    float Z,
    ClientVersionId ClientVersion,
    long ModTime);

public sealed record PlayerWarpState(
    LevelEntrySnapshot? CurrentLevel,
    float CurrentX,
    float CurrentY);

public sealed record PlayerWarpSettings(
    string UnstickLevelName,
    float UnstickX,
    float UnstickY)
{
    public static PlayerWarpSettings Default { get; } = new("onlinestartlocal.nw", 30.0f, 35.0f);
}

public interface ILevelLookup
{
    LevelEntrySnapshot? FindLevel(string levelName);
}

public enum LevelEntryStopPoint
{
    MissingLevel,
    BeforeSendLevelRuntime
}

public sealed record LevelEntryBoundaryResult(
    bool Accepted,
    LevelEntryStopPoint StopPoint,
    LevelEntrySnapshot? Level);

public enum PlayerWarpStopPoint
{
    SameLevelPositionUpdated,
    TargetReadyForSendLevelRuntime,
    FallbackPreviousReadyForSendLevelRuntime,
    FallbackUnstickReadyForSendLevelRuntime,
    Failed
}

public sealed record PlayerWarpBoundaryResult(
    bool CppReturnValue,
    bool ReachedSendLevelRuntime,
    PlayerWarpStopPoint StopPoint,
    LevelEntrySnapshot? Level);

public static class WarpWorldEntryBoundary
{
    public static PlayerWarpBoundaryResult BeginClientLevelWarpPacket(
        ClientSessionSkeleton session,
        ILevelLookup levelLookup,
        PlayerWarpState state,
        ReadOnlySpan<byte> packet,
        ClientVersionId clientVersion,
        float currentZ,
        PlayerWarpSettings settings)
    {
        var levelWarp = LevelWarpPacketParser.Parse(packet);
        return BeginWarp(
            session,
            levelLookup,
            state,
            new LevelWarpRequest(
                levelWarp.LevelName,
                levelWarp.X,
                levelWarp.Y,
                currentZ,
                clientVersion,
                levelWarp.ModTime),
            settings);
    }

    public static PlayerWarpBoundaryResult BeginWarp(
        ClientSessionSkeleton session,
        ILevelLookup levelLookup,
        PlayerWarpState state,
        LevelWarpRequest request,
        PlayerWarpSettings settings)
    {
        if (session.Lifecycle != SessionLifecycle.ReadyForLevelWarp)
            throw new InvalidOperationException("warp boundary requires ReadyForLevelWarp.");

        var targetLevel = levelLookup.FindLevel(request.LevelName);
        if (IsSameLevel(state.CurrentLevel, targetLevel))
        {
            session.QueuePacket(SameLevelPositionProps(request.X, request.Y));
            session.MarkSameLevelWarpPositionUpdated();
            return new PlayerWarpBoundaryResult(
                true,
                false,
                PlayerWarpStopPoint.SameLevelPositionUpdated,
                targetLevel);
        }

        var unstickLevel = levelLookup.FindLevel(settings.UnstickLevelName);
        var targetResult = BeginSetLevel(session, levelLookup, request);
        if (targetResult.Accepted)
        {
            return new PlayerWarpBoundaryResult(
                true,
                true,
                PlayerWarpStopPoint.TargetReadyForSendLevelRuntime,
                targetResult.Level);
        }

        if (state.CurrentLevel is not null)
        {
            var previousResult = BeginSetLevel(
                session,
                levelLookup,
                request with
                {
                    LevelName = state.CurrentLevel.LevelName,
                    X = state.CurrentX,
                    Y = state.CurrentY,
                    ModTime = 0
                });
            if (previousResult.Accepted)
            {
                return new PlayerWarpBoundaryResult(
                    false,
                    true,
                    PlayerWarpStopPoint.FallbackPreviousReadyForSendLevelRuntime,
                    previousResult.Level);
            }
        }

        if (unstickLevel is null)
            return new PlayerWarpBoundaryResult(false, false, PlayerWarpStopPoint.Failed, null);

        var unstickResult = BeginSetLevel(
            session,
            levelLookup,
            request with
            {
                LevelName = unstickLevel.LevelName,
                X = settings.UnstickX,
                Y = settings.UnstickY,
                ModTime = 0
            });

        return unstickResult.Accepted
            ? new PlayerWarpBoundaryResult(
                false,
                true,
                PlayerWarpStopPoint.FallbackUnstickReadyForSendLevelRuntime,
                unstickResult.Level)
            : new PlayerWarpBoundaryResult(false, false, PlayerWarpStopPoint.Failed, null);
    }

    public static LevelEntryBoundaryResult BeginSetLevel(
        ClientSessionSkeleton session,
        ILevelLookup levelLookup,
        LevelWarpRequest request)
    {
        if (session.Lifecycle != SessionLifecycle.ReadyForLevelWarp)
            throw new InvalidOperationException("setLevel boundary requires ReadyForLevelWarp.");

        var level = levelLookup.FindLevel(request.LevelName);
        if (level is null)
        {
            session.QueuePacket(AppendNewline(WarpPackets.BuildWarpFailed(request.LevelName)));
            return new LevelEntryBoundaryResult(false, LevelEntryStopPoint.MissingLevel, null);
        }

        if (request.ModTime == 0 || request.ClientVersion < ClientVersionId.Client21)
        {
            var packet = level.Map is { Type: LevelMapType.Gmap } &&
                         request.ClientVersion >= ClientVersionId.Client21
                ? WarpPackets.BuildPlayerWarp2(request.X, request.Y, request.Z, level.MapX, level.MapY, level.Map.MapName)
                : WarpPackets.BuildPlayerWarp(request.X, request.Y, level.LevelName);
            session.QueuePacket(AppendNewline(packet));
        }

        session.MarkReadyForLevelRuntime();
        return new LevelEntryBoundaryResult(true, LevelEntryStopPoint.BeforeSendLevelRuntime, level);
    }

    private static bool IsSameLevel(LevelEntrySnapshot? currentLevel, LevelEntrySnapshot? targetLevel)
    {
        if (currentLevel is null || targetLevel is null)
            return false;

        return string.Equals(currentLevel.LevelName, targetLevel.LevelName, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] SameLevelPositionProps(float x, float y)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.PlayerProps);
        writer.WriteGChar((byte)PlayerPropertyId.X);
        writer.WriteGChar((byte)(x * 2));
        writer.WriteGChar((byte)PlayerPropertyId.Y);
        writer.WriteGChar((byte)(y * 2));
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
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
