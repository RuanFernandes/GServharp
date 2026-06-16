using GServ.Game;
using GServ.Protocol;

namespace GServ.Network;

public interface ILiveWorldSessionSink
{
    ushort PlayerId { get; }
    void QueuePacket(byte[] packet);
}

public sealed record LiveWorldForwardingDelivery(ushort PlayerId, byte[] Packet);

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
        IReadOnlyDictionary<ushort, ILiveWorldSessionSink> sinks)
    {
        var updateArray = updates.ToArray();
        RuntimePlayerPropsApplier.ApplyConfirmed(sender, updateArray);

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            sender.Id,
            sender.PixelX,
            sender.PixelY,
            sender.PixelZ,
            updateArray,
            senderSupportsPreciseMovement,
            appendNewline: true,
            state: new IncomingPlayerPropsForwardingState(
                (byte)(sender.Hitpoints * 2.0f),
                CurrentLevelName: BuildCurrentLevelPropValue(sender),
                AccountName: sender.AccountName));

        return ForwardConfirmedLevelAreaPacket(
            server,
            sender,
            packet,
            sinks,
            new HashSet<ushort> { sender.Id });
    }

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
}
