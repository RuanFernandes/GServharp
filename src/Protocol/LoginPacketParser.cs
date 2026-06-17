using System.Text;

namespace GServ.Protocol;

public sealed record LoginPacket(
    PlayerSessionType Type,
    EncryptionGeneration InboundGeneration,
    byte? EncryptionKey,
    string VersionToken,
    ClientVersionId VersionId,
    RemoteControlVersionId RemoteControlVersionId,
    NpcControlVersionId NpcControlVersionId,
    string AccountName,
    string Password,
    string Identity,
    string Platform);

public static class LoginPacketParser
{
    public static LoginPacket Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new GraalBinaryReader(payload);
        var type = (PlayerSessionType)(1 << reader.ReadGChar());
        var generation = LoginPreludeParser.Parse(payload).InboundGeneration;

        byte? key = null;
        var versionToken = string.Empty;
        var clientVersionId = ClientVersionId.Unknown;
        var rcVersionId = RemoteControlVersionId.Unknown;
        var ncVersionId = NpcControlVersionId.Unknown;

        if (type == PlayerSessionType.Client)
        {
            versionToken = ReadAscii(reader, 8);
            clientVersionId = GraalVersionCatalog.GetClientVersionId(versionToken);
            if (clientVersionId == ClientVersionId.Unknown)
            {
                generation = EncryptionGeneration.Gen3;
                reader = new GraalBinaryReader(payload[1..]);
            }
        }

        if (clientVersionId == ClientVersionId.Unknown &&
            (IsNonWebClient(type) || NeedsRcKey(type, generation)))
        {
            key = reader.ReadGChar();
        }

        if (string.IsNullOrEmpty(versionToken) || clientVersionId == ClientVersionId.Unknown)
        {
            versionToken = ReadAscii(reader, 8);
            if (IsAnyClient(type))
                clientVersionId = GraalVersionCatalog.GetClientVersionId(versionToken);
            else if (IsAnyRemoteControl(type))
                rcVersionId = GraalVersionCatalog.GetRemoteControlVersionId(versionToken);
            else if (type == PlayerSessionType.NpcControl)
                ncVersionId = GraalVersionCatalog.GetNpcControlVersionId(versionToken);
        }

        var accountName = ReadGCharString(reader);
        var password = ReadGCharString(reader);
        var identity = ReadAscii(reader, reader.BytesLeft);
        var platform = identity.Length == 0 ? string.Empty : identity.Split(',', 2)[0];

        return new LoginPacket(
            type,
            generation,
            key,
            versionToken,
            clientVersionId,
            rcVersionId,
            ncVersionId,
            accountName,
            password,
            identity,
            platform);
    }

    public static bool IsKnownSessionType(PlayerSessionType type) =>
        type is PlayerSessionType.Client
            or PlayerSessionType.RemoteControl
            or PlayerSessionType.NpcServer
            or PlayerSessionType.NpcControl
            or PlayerSessionType.Client2
            or PlayerSessionType.Client3
            or PlayerSessionType.RemoteControl2
            or PlayerSessionType.Web;

    private static bool NeedsRcKey(PlayerSessionType type, EncryptionGeneration generation) =>
        (type == PlayerSessionType.RemoteControl2) || (IsAnyRemoteControl(type) && generation > EncryptionGeneration.Gen3);

    private static bool IsNonWebClient(PlayerSessionType type) =>
        IsAnyClient(type) && type != PlayerSessionType.Web;

    private static bool IsAnyClient(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyClient) != 0;

    private static bool IsAnyRemoteControl(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyRemoteControl) != 0;

    private static string ReadGCharString(GraalBinaryReader reader) =>
        ReadAscii(reader, reader.ReadGChar());

    private static string ReadAscii(GraalBinaryReader reader, int length) =>
        Encoding.ASCII.GetString(reader.ReadBytes(length));
}
