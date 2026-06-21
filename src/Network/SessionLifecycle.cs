namespace Preagonal.GServer.Network;

public enum SessionLifecycle
{
    AwaitingLoginPrelude,
    LoginPreludeParsed,
    WaitingForServerListAuth,
    ServerListAuthAcceptedPreWorld,
    ReadyForWorldEntry,
    ReadyForLevelWarp,
    SameLevelWarpPositionUpdated,
    ReadyForLevelRuntime,
    LevelPayloadSent,
    DynamicLevelPayloadSent,
    LevelRuntimePacketsSent,
    LevelEntryPlayerPropsSynchronized,
    Authenticated,
    Rejected,
    Disconnecting,
    Disconnected
}
