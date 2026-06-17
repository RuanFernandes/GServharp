using System.Text;
using GServ.Protocol;

namespace GServ.Network;

public sealed record LoginFlag(string Name, string Value);

public enum LoginMapType
{
    BigMap,
    GMap
}

public sealed record LoginMapFile(string MapName, LoginMapType Type);

public sealed record LoginWeaponPacket(string Name, byte[] Packet);

public sealed record PostLoginClientOptions(
    IResourceFileSystem? ResourceFileSystem,
    IReadOnlyList<LoginMapFile> Maps,
    IReadOnlyList<LoginWeaponPacket>? PlayerWeapons = null,
    IReadOnlyList<string>? ProtectedWeaponNames = null,
    IReadOnlyDictionary<string, byte[]>? ProtectedWeaponPackets = null,
    IReadOnlyList<byte[]>? OrderedClassPackets = null);

public sealed record PostLoginPlayerSnapshot(
    ushort PlayerId,
    PlayerSessionType Type,
    byte[] AccountNameProperty,
    byte[] NicknameProperty,
    byte[] CurrentLevelProperty,
    byte[] XProperty,
    byte[] YProperty,
    byte[] AlignmentProperty,
    byte[] IpAddressProperty,
    PlayerPropertySource LoginPropertySource,
    IReadOnlyList<PlayerPropertyId> LoginPropertyIds,
    IReadOnlyList<LoginFlag> PlayerFlags,
    IReadOnlyList<LoginFlag> ServerFlags);

public enum PostLoginClientStopPoint
{
    BeforeWarp
}

public sealed record PostLoginClientBoundaryResult(
    bool Accepted,
    byte[] ServerListAddPlayerPacket,
    PostLoginClientStopPoint StopPoint);

public static class PostLoginWorldEntryBoundary
{
    public static PostLoginClientBoundaryResult BeginClient(
        ClientSessionSkeleton session,
        PostLoginPlayerSnapshot snapshot,
        PostLoginClientOptions? options = null)
    {
        if (session.Lifecycle != SessionLifecycle.ReadyForWorldEntry)
            throw new InvalidOperationException("sendLoginClient boundary requires ReadyForWorldEntry.");

        var serverListAddPlayerPacket = BuildServerListAddPlayerPacket(snapshot);

        var preClient21 = session.LoginPacket?.VersionId < ClientVersionId.Client21;
        var loginPropertyIds = preClient21
            ? snapshot.LoginPropertyIds.Where(id => (byte)id < 37)
            : snapshot.LoginPropertyIds;
        var loginPropertiesPayload = PlayerPropertySerializer.SerializeConfirmedLoginSubset(
            snapshot.LoginPropertySource,
            loginPropertyIds,
            preClient21: preClient21);
        session.QueuePacket(PlayerPropertySerializer.BuildPlayerPropsPacket(loginPropertiesPayload, appendNewline: true));

        SendOldClientMapWorkaround(session, options);

        session.QueuePacket(BlankPacket(ServerToPlayerPacketId.ClearWeapons, appendNewline: true));

        foreach (var flag in snapshot.PlayerFlags)
            session.QueuePacket(FlagSet(flag, appendNewline: true));

        foreach (var flag in snapshot.ServerFlags)
            session.QueuePacket(FlagSet(flag, appendNewline: true));

        session.QueuePacket(NpcWeaponDelete("Bomb", appendNewline: true));
        session.QueuePacket(NpcWeaponDelete("Bow", appendNewline: true));
        SendLoginWeaponsAndClasses(session, options);
        session.QueuePacket(BlankPacket(ServerToPlayerPacketId.ServerListConnected, appendNewline: true));
        session.MarkReadyForLevelWarp();

        return new PostLoginClientBoundaryResult(
            true,
            serverListAddPlayerPacket,
            PostLoginClientStopPoint.BeforeWarp);
    }

    private static void SendOldClientMapWorkaround(
        ClientSessionSkeleton session,
        PostLoginClientOptions? options)
    {
        if (session.LoginPacket?.VersionId is not (ClientVersionId.Client231 or ClientVersionId.Client1411))
            return;

        if (options?.ResourceFileSystem is null)
            return;

        foreach (var map in options.Maps)
        {
            if (map.Type != LoginMapType.BigMap)
                continue;

            FileTransferBoundary.HandleWantFile(
                session,
                options.ResourceFileSystem,
                map.MapName,
                session.LoginPacket.VersionId);
        }
    }

    private static void SendLoginWeaponsAndClasses(
        ClientSessionSkeleton session,
        PostLoginClientOptions? options)
    {
        if (options is null)
            return;

        var ownedWeapons = new HashSet<string>(StringComparer.Ordinal);

        foreach (var weapon in options.PlayerWeapons ?? [])
        {
            ownedWeapons.Add(weapon.Name);
            session.QueuePacket(EnsureNewline(weapon.Packet));
        }

        foreach (var weaponName in options.ProtectedWeaponNames ?? [])
        {
            if (ownedWeapons.Contains(weaponName))
                continue;

            if (options.ProtectedWeaponPackets?.TryGetValue(weaponName, out var packet) == true)
            {
                ownedWeapons.Add(weaponName);
                session.QueuePacket(EnsureNewline(packet));
            }
        }

        if (session.LoginPacket?.VersionId < ClientVersionId.Client40211)
            return;

        foreach (var classPacket in options.OrderedClassPackets ?? [])
            session.QueuePacket(EnsureNewline(classPacket));
    }

    public static byte[] BuildServerListAddPlayerPacket(PostLoginPlayerSnapshot snapshot)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.PlayerAdd);
        writer.WriteGShort(snapshot.PlayerId);
        writer.WriteGChar((byte)snapshot.Type);
        WriteProperty(writer, 34, snapshot.AccountNameProperty);
        WriteProperty(writer, 0, snapshot.NicknameProperty);
        WriteProperty(writer, 20, snapshot.CurrentLevelProperty);
        WriteProperty(writer, 15, snapshot.XProperty);
        WriteProperty(writer, 16, snapshot.YProperty);
        WriteProperty(writer, 32, snapshot.AlignmentProperty);
        WriteProperty(writer, 30, snapshot.IpAddressProperty);
        return writer.ToArray();
    }

    private static byte[] BlankPacket(ServerToPlayerPacketId packetId, bool appendNewline)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)packetId);
        if (appendNewline)
            writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static byte[] FlagSet(LoginFlag flag, bool appendNewline)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.FlagSet);
        writer.WriteBytes(Encoding.ASCII.GetBytes(flag.Value.Length == 0 ? flag.Name : $"{flag.Name}={flag.Value}"));
        if (appendNewline)
            writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static byte[] NpcWeaponDelete(string weaponName, bool appendNewline)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.NpcWeaponDelete);
        writer.WriteBytes(Encoding.ASCII.GetBytes(weaponName));
        if (appendNewline)
            writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static byte[] EnsureNewline(byte[] packet)
    {
        if (packet.Length > 0 && packet[^1] == (byte)'\n')
            return packet;

        var withNewline = new byte[packet.Length + 1];
        Buffer.BlockCopy(packet, 0, withNewline, 0, packet.Length);
        withNewline[^1] = (byte)'\n';
        return withNewline;
    }

    private static void WriteProperty(GraalBinaryWriter writer, byte propertyId, ReadOnlySpan<byte> payload)
    {
        writer.WriteGChar(propertyId);
        writer.WriteBytes(payload);
    }
}
