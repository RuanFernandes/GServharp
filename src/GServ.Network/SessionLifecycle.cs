namespace GServ.Network;

public enum SessionLifecycle
{
    AwaitingLoginPrelude,
    LoginPreludeParsed,
    WaitingForServerListAuth,
    ServerListAuthAcceptedPreWorld,
    ReadyForWorldEntry,
    ReadyForLevelWarp,
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
