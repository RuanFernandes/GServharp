using Preagonal.GServer.Game;
using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public enum PostLoginPacketDispatchStatus
{
    Handled,
    Blocked,
    InvalidPacket,
    InvalidPacketLimitExceeded
}

public sealed record PostLoginPacketDispatchResult(
    PostLoginPacketDispatchStatus Status,
    bool ContinueSession,
    byte RawPacketId,
    PlayerToServerPacketId? PacketId,
    string Message,
    byte[] OutboundBytes)
{
    public static PostLoginPacketDispatchResult Handled(byte rawPacketId, PlayerToServerPacketId packetId, string message) =>
        new(PostLoginPacketDispatchStatus.Handled, true, rawPacketId, packetId, message, []);

    public static PostLoginPacketDispatchResult Blocked(byte rawPacketId, string message) =>
        new(PostLoginPacketDispatchStatus.Blocked, false, rawPacketId, null, message, []);

    public static PostLoginPacketDispatchResult Invalid(byte rawPacketId, int invalidPacketCount) =>
        new(
            PostLoginPacketDispatchStatus.InvalidPacket,
            true,
            rawPacketId,
            null,
            $"Unknown Player Packet: {rawPacketId}; invalid packet count is {invalidPacketCount}.",
            []);

    public static PostLoginPacketDispatchResult InvalidLimitExceeded(byte rawPacketId, int invalidPacketCount) =>
        new(
            PostLoginPacketDispatchStatus.InvalidPacketLimitExceeded,
            false,
            rawPacketId,
            null,
            $"Unknown Player Packet: {rawPacketId}; invalid packet count {invalidPacketCount} exceeded the C++ msgPLI_NULL limit.",
            OutboundLoginPackets.DisconnectMessage("Disconnected for sending invalid packets.", appendNewline: true));
}

public sealed class PostLoginPacketDispatcher
{
    private readonly RuntimePlayer _player;

    public PostLoginPacketDispatcher(RuntimePlayer player)
    {
        _player = player;
    }

    public int InvalidPacketCount { get; private set; }

    public PostLoginPacketDispatchResult DispatchDecodedPacket(ReadOnlySpan<byte> packet)
    {
        if (packet.IsEmpty)
            return CountInvalidPacket(0);

        var reader = new GraalBinaryReader(packet);
        var rawPacketId = reader.ReadGChar();

        if (rawPacketId == (byte)PlayerToServerPacketId.PlayerProps)
            return DispatchPlayerProps(rawPacketId, packet[1..]);

        if (rawPacketId == (byte)PlayerToServerPacketId.ClaimPker)
        {
            var parsed = CombatPackets.ParseClaimPker(packet);
            return parsed.Success
                ? PostLoginPacketDispatcherResult(parsed, rawPacketId)
                : PostLoginPacketDispatcherResult(null, rawPacketId);
        }

        if (rawPacketId == (byte)PlayerToServerPacketId.BaddyHurt)
        {
            var parsed = CombatPackets.ParseBaddyHurt(packet);
            return parsed.Success
                ? PostLoginPacketDispatcherResult(parsed, rawPacketId)
                : PostLoginPacketDispatcherResult(null, rawPacketId);
        }

        if (rawPacketId == (byte)PlayerToServerPacketId.HurtPlayer)
        {
            var parsed = CombatPackets.ParseHurtPlayer(packet);
            return parsed.Success
                ? PostLoginPacketDispatcherResult(parsed, rawPacketId)
                : PostLoginPacketDispatcherResult(null, rawPacketId);
        }

        if (CppPlayerPacketDispatchTable.IsAssigned(rawPacketId))
            return PostLoginPacketDispatchResult.Blocked(
                rawPacketId,
                $"{CppPlayerPacketDispatchTable.NameOf(rawPacketId)} is source-confirmed in Player::createFunctions but not implemented in the production C# dispatcher yet.");

        return CountInvalidPacket(rawPacketId);
    }

    private PostLoginPacketDispatchResult DispatchPlayerProps(byte rawPacketId, ReadOnlySpan<byte> body)
    {
        var parsed = IncomingPlayerPropsParser.Parse(body);
        foreach (var update in parsed.Updates)
        {
            try
            {
                RuntimePlayerPropsApplier.ApplyConfirmed(_player, [update]);
            }
            catch (NotSupportedException ex)
            {
                return PostLoginPacketDispatchResult.Blocked(
                    rawPacketId,
                    $"{CppNameOf(update.PropertyId)} was parsed with source-confirmed bytes, but its runtime side effects are not ported yet: {ex.Message}");
            }
        }

        if (!parsed.Success)
            return PostLoginPacketDispatchResult.Blocked(
                rawPacketId,
                $"PLI_PLAYERPROPS stopped at unconfirmed property {(byte)parsed.UnsupportedPropertyId!.Value}; no unconfirmed side effects were run.");

        return PostLoginPacketDispatchResult.Handled(
            rawPacketId,
            PlayerToServerPacketId.PlayerProps,
            "Applied confirmed PLI_PLAYERPROPS subset.");
    }

    private PostLoginPacketDispatchResult PostLoginPacketDispatcherResult(
        object? parsedPacket,
        byte rawPacketId)
    {
        return parsedPacket is null
            ? PostLoginPacketDispatchResult.Blocked(
                rawPacketId,
                $"{CppPlayerPacketDispatchTable.NameOf(rawPacketId)} could not be parsed safely yet.")
            : PostLoginPacketDispatchResult.Blocked(
                rawPacketId,
                $"{CppPlayerPacketDispatchTable.NameOf(rawPacketId)} packet payload was parsed, but runtime combat side effects are intentionally blocked until production wiring is confirmed.");
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

    private PostLoginPacketDispatchResult CountInvalidPacket(byte rawPacketId)
    {
        InvalidPacketCount++;
        return InvalidPacketCount > 5
            ? PostLoginPacketDispatchResult.InvalidLimitExceeded(rawPacketId, InvalidPacketCount)
            : PostLoginPacketDispatchResult.Invalid(rawPacketId, InvalidPacketCount);
    }

    private static class CppPlayerPacketDispatchTable
    {
        private static readonly IReadOnlyDictionary<byte, string> AssignedPacketNames = new Dictionary<byte, string>
        {
            [0] = "PLI_LEVELWARP",
            [1] = "PLI_BOARDMODIFY",
            [2] = "PLI_PLAYERPROPS",
            [3] = "PLI_NPCPROPS",
            [4] = "PLI_BOMBADD",
            [5] = "PLI_BOMBDEL",
            [6] = "PLI_TOALL",
            [7] = "PLI_HORSEADD",
            [8] = "PLI_HORSEDEL",
            [9] = "PLI_ARROWADD",
            [10] = "PLI_FIRESPY",
            [11] = "PLI_THROWCARRIED",
            [12] = "PLI_ITEMADD",
            [13] = "PLI_ITEMDEL",
            [14] = "PLI_CLAIMPKER",
            [15] = "PLI_BADDYPROPS",
            [16] = "PLI_BADDYHURT",
            [17] = "PLI_BADDYADD",
            [18] = "PLI_FLAGSET",
            [19] = "PLI_FLAGDEL",
            [20] = "PLI_OPENCHEST",
            [21] = "PLI_PUTNPC",
            [22] = "PLI_NPCDEL",
            [23] = "PLI_WANTFILE",
            [24] = "PLI_SHOWIMGPLAYER",
            [26] = "PLI_HURTPLAYER",
            [27] = "PLI_EXPLOSION",
            [28] = "PLI_PRIVATEMESSAGE",
            [29] = "PLI_NPCWEAPONDEL",
            [30] = "PLI_LEVELWARPMOD",
            [31] = "PLI_PACKETCOUNT",
            [32] = "PLI_ITEMTAKE",
            [33] = "PLI_WEAPONADD",
            [34] = "PLI_UPDATEFILE",
            [35] = "PLI_ADJACENTLEVEL",
            [36] = "PLI_HITOBJECTS",
            [37] = "PLI_LANGUAGE",
            [38] = "PLI_TRIGGERACTION",
            [39] = "PLI_MAPINFO",
            [40] = "PLI_SHOOT",
            [41] = "PLI_SERVERWARP",
            [44] = "PLI_PROCESSLIST",
            [46] = "PLI_UNKNOWN46",
            [47] = "PLI_VERIFYWANTSEND",
            [48] = "PLI_SHOOT2",
            [50] = "PLI_RAWDATA",
            [51] = "PLI_RC_SERVEROPTIONSGET",
            [52] = "PLI_RC_SERVEROPTIONSSET",
            [53] = "PLI_RC_FOLDERCONFIGGET",
            [54] = "PLI_RC_FOLDERCONFIGSET",
            [55] = "PLI_RC_RESPAWNSET",
            [56] = "PLI_RC_HORSELIFESET",
            [57] = "PLI_RC_APINCREMENTSET",
            [58] = "PLI_RC_BADDYRESPAWNSET",
            [59] = "PLI_RC_PLAYERPROPSGET",
            [60] = "PLI_RC_PLAYERPROPSSET",
            [61] = "PLI_RC_DISCONNECTPLAYER",
            [62] = "PLI_RC_UPDATELEVELS",
            [63] = "PLI_RC_ADMINMESSAGE",
            [64] = "PLI_RC_PRIVADMINMESSAGE",
            [65] = "PLI_RC_LISTRCS",
            [66] = "PLI_RC_DISCONNECTRC",
            [67] = "PLI_RC_APPLYREASON",
            [68] = "PLI_RC_SERVERFLAGSGET",
            [69] = "PLI_RC_SERVERFLAGSSET",
            [70] = "PLI_RC_ACCOUNTADD",
            [71] = "PLI_RC_ACCOUNTDEL",
            [72] = "PLI_RC_ACCOUNTLISTGET",
            [73] = "PLI_RC_PLAYERPROPSGET2",
            [74] = "PLI_RC_PLAYERPROPSGET3",
            [75] = "PLI_RC_PLAYERPROPSRESET",
            [76] = "PLI_RC_PLAYERPROPSSET2",
            [77] = "PLI_RC_ACCOUNTGET",
            [78] = "PLI_RC_ACCOUNTSET",
            [79] = "PLI_RC_CHAT",
            [80] = "PLI_PROFILEGET",
            [81] = "PLI_PROFILESET",
            [82] = "PLI_RC_WARPPLAYER",
            [83] = "PLI_RC_PLAYERRIGHTSGET",
            [84] = "PLI_RC_PLAYERRIGHTSSET",
            [85] = "PLI_RC_PLAYERCOMMENTSGET",
            [86] = "PLI_RC_PLAYERCOMMENTSSET",
            [87] = "PLI_RC_PLAYERBANGET",
            [88] = "PLI_RC_PLAYERBANSET",
            [89] = "PLI_RC_FILEBROWSER_START",
            [90] = "PLI_RC_FILEBROWSER_CD",
            [91] = "PLI_RC_FILEBROWSER_END",
            [92] = "PLI_RC_FILEBROWSER_DOWN",
            [93] = "PLI_RC_FILEBROWSER_UP",
            [94] = "PLI_NPCSERVERQUERY",
            [96] = "PLI_RC_FILEBROWSER_MOVE",
            [97] = "PLI_RC_FILEBROWSER_DELETE",
            [98] = "PLI_RC_FILEBROWSER_RENAME",
            [103] = "PLI_NC_NPCGET",
            [104] = "PLI_NC_NPCDELETE",
            [105] = "PLI_NC_NPCRESET",
            [106] = "PLI_NC_NPCSCRIPTGET",
            [107] = "PLI_NC_NPCWARP",
            [108] = "PLI_NC_NPCFLAGSGET",
            [109] = "PLI_NC_NPCSCRIPTSET",
            [110] = "PLI_NC_NPCFLAGSSET",
            [111] = "PLI_NC_NPCADD",
            [112] = "PLI_NC_CLASSEDIT",
            [113] = "PLI_NC_CLASSADD",
            [114] = "PLI_NC_LOCALNPCSGET",
            [115] = "PLI_NC_WEAPONLISTGET",
            [116] = "PLI_NC_WEAPONGET",
            [117] = "PLI_NC_WEAPONADD",
            [118] = "PLI_NC_WEAPONDELETE",
            [119] = "PLI_NC_CLASSDELETE",
            [130] = "PLI_REQUESTUPDATEBOARD",
            [150] = "PLI_NC_LEVELLISTGET",
            [152] = "PLI_REQUESTTEXT",
            [154] = "PLI_SENDTEXT",
            [155] = "PLI_RC_LARGEFILESTART",
            [156] = "PLI_RC_LARGEFILEEND",
            [157] = "PLI_UPDATEGANI",
            [158] = "PLI_UPDATESCRIPT",
            [159] = "PLI_UPDATEPACKAGEREQUESTFILE",
            [160] = "PLI_RC_FOLDERDELETE",
            [162] = "PLI_RC_UNKNOWN162"
        };

        public static bool IsAssigned(byte packetId) =>
            AssignedPacketNames.ContainsKey(packetId);

        public static string NameOf(byte packetId) =>
            AssignedPacketNames.GetValueOrDefault(packetId, $"PLI_{packetId}");
    }
}
