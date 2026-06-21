using Preagonal.GServer.Game;
using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public interface ILiveWorldSessionSink
{
    ushort PlayerId { get; }
    void QueuePacket(byte[] packet);
}

public sealed record LiveWorldForwardingDelivery(ushort PlayerId, byte[] Packet);

public enum LiveWorldPlayerPropsForwardingStatus
{
    Delivered,
    Blocked
}

public sealed record LiveWorldPlayerPropsForwardingResult(
    LiveWorldPlayerPropsForwardingStatus Status,
    string Message,
    IReadOnlyList<LiveWorldForwardingDelivery> Deliveries)
{
    public static LiveWorldPlayerPropsForwardingResult Delivered(IReadOnlyList<LiveWorldForwardingDelivery> deliveries) =>
        new(LiveWorldPlayerPropsForwardingStatus.Delivered, "Applied and forwarded confirmed player props.", deliveries);

    public static LiveWorldPlayerPropsForwardingResult Blocked(string message) =>
        new(LiveWorldPlayerPropsForwardingStatus.Blocked, message, []);
}

public static class LiveWorldSessionForwarder
{
    public static IReadOnlyList<LiveWorldForwardingDelivery> ForwardConfirmedOneLevelPacket(
        RuntimeServer server,
        RuntimeLevel level,
        byte[] packet,
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks,
        IReadOnlySet<ushort>? exclude = null)
    {
        var recipients = LiveWorldForwardingSelector.SelectOneLevelRecipients(
            server,
            level,
            exclude);

        return Deliver(packet, recipients, sinks);
    }

    public static IReadOnlyList<LiveWorldForwardingDelivery> ForwardConfirmedLevelAreaPacket(
        RuntimeServer server,
        RuntimePlayer sender,
        byte[] packet,
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks,
        IReadOnlySet<ushort>? exclude = null)
    {
        var recipients = LiveWorldForwardingSelector.SelectLevelAreaRecipients(
            server,
            sender,
            exclude ?? new HashSet<ushort> { sender.Id });

        return Deliver(packet, recipients, sinks);
    }

    public static IReadOnlyList<LiveWorldForwardingDelivery> ApplyAndForwardConfirmedPlayerProps(
        RuntimeServer server,
        RuntimePlayer sender,
        IEnumerable<IncomingPlayerPropertyUpdate> updates,
        bool senderSupportsPreciseMovement,
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks,
        RuntimePlayerPropsOptions? options = null)
    {
        var result = TryApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            updates,
            senderSupportsPreciseMovement,
            sinks,
            options);

        if (result.Status == LiveWorldPlayerPropsForwardingStatus.Blocked)
            throw new NotSupportedException(result.Message);

        return result.Deliveries;
    }

    public static LiveWorldPlayerPropsForwardingResult TryApplyAndForwardConfirmedPlayerProps(
        RuntimeServer server,
        RuntimePlayer sender,
        IEnumerable<IncomingPlayerPropertyUpdate> updates,
        bool senderSupportsPreciseMovement,
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks,
        RuntimePlayerPropsOptions? options = null)
    {
        options ??= RuntimePlayerPropsOptions.Default;
        var updateArray = updates.ToArray();
        var directDeliveries = new List<LiveWorldForwardingDelivery>();
        var selfDeliveries = new List<LiveWorldForwardingDelivery>();
        foreach (var update in updateArray)
        {
            try
            {
                RuntimePlayerPropsApplier.ApplyConfirmed(sender, [update], options);
            }
            catch (NotSupportedException ex)
            {
                return LiveWorldPlayerPropsForwardingResult.Blocked(
                    $"{CppNameOf(update.PropertyId)} was parsed with source-confirmed bytes, but its runtime side effects are not ported yet: {ex.Message}");
            }

            directDeliveries.AddRange(BuildAndDeliverDirectPlayerPropPackets(server, sender, update, sinks));
            selfDeliveries.AddRange(BuildAndDeliverSelfPlayerPropPackets(server, sender, update, sinks));
        }

        var levelDeliveries = BuildAndDeliverLevelPlayerProps(
            server,
            sender,
            updateArray,
            sinks);

        var deliveries = new List<LiveWorldForwardingDelivery>(directDeliveries.Count + levelDeliveries.Count + selfDeliveries.Count);
        deliveries.AddRange(directDeliveries);
        deliveries.AddRange(levelDeliveries);
        deliveries.AddRange(selfDeliveries);

        return LiveWorldPlayerPropsForwardingResult.Delivered(deliveries);
    }

    private static IReadOnlyList<LiveWorldForwardingDelivery> BuildAndDeliverLevelPlayerProps(
        RuntimeServer server,
        RuntimePlayer sender,
        IReadOnlyList<IncomingPlayerPropertyUpdate> updates,
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks)
    {
        var recipients = LiveWorldForwardingSelector.SelectLevelAreaRecipients(
            server,
            sender,
            new HashSet<ushort> { sender.Id });
        if (recipients.Count == 0)
            return [];

        var deliveries = new List<LiveWorldForwardingDelivery>();
        var state = new IncomingPlayerPropsForwardingState(
            (byte)(sender.Hitpoints * 2.0f),
            CurrentLevelName: BuildCurrentLevelPropValue(sender),
            AccountName: sender.AccountName,
            AccountIp: sender.AccountIp,
            CommunityName: sender.CommunityName,
            EloRating: sender.EloRating,
            EloDeviation: sender.EloDeviation);

        foreach (var recipientId in recipients)
        {
            var recipient = server.GetPlayer(recipientId);
            if (recipient is null)
                continue;

            var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
                sender.Id,
                sender.PixelX,
                sender.PixelY,
                sender.PixelZ,
                updates,
                recipient.ClientVersion >= ClientVersionId.Client23,
                appendNewline: true,
                senderClientVersion: sender.ClientVersion,
                state: state);

            if (!HasPlayerPropsPayload(packet))
                continue;

            if (!sinks.TryGetValue(recipientId, out var sink))
                continue;

            sink.QueuePacket(packet);
            deliveries.Add(new LiveWorldForwardingDelivery(recipientId, packet));
        }

        return deliveries;
    }

    private static IReadOnlyList<LiveWorldForwardingDelivery> BuildAndDeliverDirectPlayerPropPackets(
        RuntimeServer server,
        RuntimePlayer sender,
        IncomingPlayerPropertyUpdate update,
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks)
    {
        var payload = new GraalBinaryWriter();
        switch (update.PropertyId)
        {
            case PlayerPropertyId.UdpPort:
                payload.WriteGChar((byte)PlayerPropertyId.UdpPort);
                payload.WriteGInt(sender.UdpPort);
                break;

            case PlayerPropertyId.PlayerStatusMessage:
                payload.WriteGChar((byte)PlayerPropertyId.PlayerStatusMessage);
                payload.WriteGChar(sender.StatusMessage);
                return Deliver(packet: BuildPacket(sender.Id, payload), SelectAnyClientExceptSelf(server, sender), sinks);

            case PlayerPropertyId.Nickname:
                payload.WriteGChar((byte)PlayerPropertyId.Nickname);
                WriteGCharString(payload, sender.Nickname);
                return Deliver(packet: BuildPacket(sender.Id, payload), SelectAllExceptSelfAndNpcServer(server, sender), sinks);

            default:
                return [];
        }

        return Deliver(packet: BuildPacket(sender.Id, payload), SelectAnyClientExceptSelf(server, sender), sinks);
    }

    private static IReadOnlyList<LiveWorldForwardingDelivery> BuildAndDeliverSelfPlayerPropPackets(
        RuntimeServer server,
        RuntimePlayer sender,
        IncomingPlayerPropertyUpdate update,
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks)
    {
        var payload = new GraalBinaryWriter();
        switch (update.PropertyId)
        {
            case PlayerPropertyId.Nickname:
                payload.WriteGChar((byte)PlayerPropertyId.Nickname);
                WriteGCharString(payload, sender.Nickname);
                return Deliver(
                    packet: PlayerPropertySerializer.BuildPlayerPropsPacket(payload.ToArray(), appendNewline: true),
                    recipients: new[] { sender.Id },
                    sinks);

            default:
                return [];
        }
    }
    private static byte[] BuildPacket(ushort playerId, GraalBinaryWriter payload) =>
        PlayerPropertySerializer.BuildOtherPlayerPropsPacket(
            playerId,
            payload.ToArray(),
            appendNewline: true);

    private static IReadOnlyList<ushort> SelectAnyClientExceptSelf(RuntimeServer server, RuntimePlayer sender)
    {
        var recipients = new List<ushort>();
        foreach (var other in server.Players)
        {
            if (other.Id == sender.Id)
                continue;
            if (!other.IsClient)
                continue;

            recipients.Add(other.Id);
        }

        return recipients;
    }

    private static IReadOnlyList<ushort> SelectAllExceptSelfAndNpcServer(RuntimeServer server, RuntimePlayer sender)
    {
        var recipients = new List<ushort>();
        foreach (var other in server.Players)
        {
            if (other.Id == sender.Id)
                continue;
            if (other.Kind == RuntimePlayerKind.NpcServer)
                continue;

            recipients.Add(other.Id);
        }

        return recipients;
    }

    private static void WriteGCharString(GraalBinaryWriter writer, string value)
    {
        writer.WriteGChar((byte)value.Length);
        writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes(value));
    }

    private static bool HasPlayerPropsPayload(byte[] packet) =>
        packet.Length > 4;

    private static IReadOnlyList<LiveWorldForwardingDelivery> Deliver(
        byte[] packet,
        IReadOnlyList<ushort> recipients,
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks)
    {
        var deliveries = new List<LiveWorldForwardingDelivery>();
        foreach (var recipient in recipients)
        {
            if (!sinks.TryGetValue(recipient, out var sink))
                continue;

            sink.QueuePacket(packet);
            deliveries.Add(new LiveWorldForwardingDelivery(recipient, packet));
        }

        return deliveries;
    }

    private static string BuildCurrentLevelPropValue(RuntimePlayer sender)
    {
        if (sender.Level?.Map is { Type: RuntimeMapType.Gmap } map)
            return map.Name;

        if (sender.Level?.IsSingleplayer == true)
            return sender.CurrentLevelName + ".singleplayer";

        return sender.CurrentLevelName;
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
}

