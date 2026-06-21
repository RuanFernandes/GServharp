using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed class ClientSessionSkeleton
{
    private readonly MemoryStream _outbound = new();

    public ClientSessionSkeleton(ushort id)
    {
        Id = id;
    }

    public ushort Id { get; }
    public PlayerSessionType Type { get; private set; } = PlayerSessionType.Await;
    public EncryptionGeneration InboundEncryptionGeneration { get; private set; } = EncryptionGeneration.Gen3;
    public SessionLifecycle Lifecycle { get; private set; } = SessionLifecycle.AwaitingLoginPrelude;
    public LoginPacket? LoginPacket { get; private set; }

    public void ReceiveLoginPrelude(ReadOnlySpan<byte> payload)
    {
        var prelude = LoginPreludeParser.Parse(payload);
        Type = prelude.Type;
        InboundEncryptionGeneration = prelude.InboundGeneration;
        Lifecycle = SessionLifecycle.LoginPreludeParsed;
    }

    public bool ReceiveLoginPacket(ReadOnlySpan<byte> payload)
    {
        var login = LoginPacketParser.Parse(payload);
        Type = login.Type;
        InboundEncryptionGeneration = login.InboundGeneration;
        LoginPacket = login;

        if (!LoginPacketParser.IsKnownSessionType(login.Type))
        {
            var message = $"Your client type is unknown.  Please inform the OpenGraal Team.  Type: {(int)login.Type}.";
            _outbound.Write(OutboundLoginPackets.DisconnectMessage(message, appendNewline: true));
            Lifecycle = SessionLifecycle.Rejected;
            return false;
        }

        Lifecycle = SessionLifecycle.LoginPreludeParsed;
        return true;
    }

    public byte[] TakeOutboundBytes()
    {
        var bytes = _outbound.ToArray();
        _outbound.SetLength(0);
        return bytes;
    }

    public bool ReceiveServerListAuthResponse(ServerListVerifyAccount2Response response)
    {
        if (LoginPacket is null)
            throw new InvalidOperationException("Login packet must be parsed before server-list auth response.");

        LoginPacket = LoginPacket with { AccountName = response.AccountName };
        if (!response.IsSuccess)
        {
            QueueDisconnect(response.Message);
            return false;
        }

        Lifecycle = SessionLifecycle.ServerListAuthAcceptedPreWorld;
        return true;
    }

    internal void QueueDisconnect(string message)
    {
        _outbound.Write(OutboundLoginPackets.DisconnectMessage(message, appendNewline: true));
        Lifecycle = SessionLifecycle.Rejected;
    }

    internal void QueuePacket(byte[] packet)
    {
        _outbound.Write(packet);
    }

    internal void MarkWaitingForServerListAuth()
    {
        Lifecycle = SessionLifecycle.WaitingForServerListAuth;
    }

    internal void MarkReadyForWorldEntry()
    {
        Lifecycle = SessionLifecycle.ReadyForWorldEntry;
    }

    internal void MarkReadyForLevelWarp()
    {
        Lifecycle = SessionLifecycle.ReadyForLevelWarp;
    }

    internal void MarkReadyForLevelRuntime()
    {
        Lifecycle = SessionLifecycle.ReadyForLevelRuntime;
    }

    internal void MarkSameLevelWarpPositionUpdated()
    {
        Lifecycle = SessionLifecycle.SameLevelWarpPositionUpdated;
    }

    internal void MarkLevelPayloadSent()
    {
        Lifecycle = SessionLifecycle.LevelPayloadSent;
    }

    internal void MarkDynamicLevelPayloadSent()
    {
        Lifecycle = SessionLifecycle.DynamicLevelPayloadSent;
    }

    internal void MarkLevelRuntimePacketsSent()
    {
        Lifecycle = SessionLifecycle.LevelRuntimePacketsSent;
    }

    internal void MarkLevelEntryPlayerPropsSynchronized()
    {
        Lifecycle = SessionLifecycle.LevelEntryPlayerPropsSynchronized;
    }
}
