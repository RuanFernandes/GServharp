using System.Text;
using System.IO.Compression;

namespace Preagonal.GServer.Protocol;

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
    string Platform,
    string DebugInfo = "");

public static class LoginPacketParser
{
    public static LoginPacket Parse(ReadOnlySpan<byte> payload)
    {
        var loginPayload = DecodeLoginPayload(payload);
        var reader = new GraalBinaryReader(loginPayload);
        var type = (PlayerSessionType)(1 << reader.ReadGChar());
        var generation = LoginPreludeParser.Parse(loginPayload).InboundGeneration;

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
                reader = new GraalBinaryReader(loginPayload[1..]);
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

        if (type == PlayerSessionType.Client2 && clientVersionId == ClientVersionId.Unknown && key is not null)
        {
            var retryReader = new GraalBinaryReader(loginPayload[1..]);
            var retryVersionToken = ReadAscii(retryReader, 8);
            var retryVersionId = GraalVersionCatalog.GetClientVersionId(retryVersionToken);
            if (retryVersionId != ClientVersionId.Unknown)
            {
                key = null;
                reader = retryReader;
                versionToken = retryVersionToken;
                clientVersionId = retryVersionId;
            }
        }

        if (IsAnyClient(type) && clientVersionId == ClientVersionId.Unknown &&
            TryFindClientVersion(loginPayload, out var versionOffset, out var foundVersionToken, out var foundVersionId))
        {
            key = versionOffset > 1 ? unchecked((byte)(loginPayload[versionOffset - 1] - 32)) : null;
            reader = new GraalBinaryReader(loginPayload[(versionOffset + 8)..]);
            versionToken = foundVersionToken;
            clientVersionId = foundVersionId;
        }

        var accountName = ReadGCharString(reader);
        var password = ReadGCharString(reader);
        var identity = ReadAscii(reader, reader.BytesLeft);
        var platform = identity.Length == 0 ? string.Empty : identity.Split(',', 2)[0];
        var debugInfo = BuildDebugInfo(loginPayload, key);

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
            platform,
            debugInfo);
    }

    private static byte[] DecodeLoginPayload(ReadOnlySpan<byte> payload)
    {
        var decoded = LooksLikeZlib(payload)
            ? ZlibDecompress(payload)
            : payload.ToArray();

        return decoded.Length > 0 && decoded[^1] == (byte)'\n'
            ? decoded[..^1]
            : decoded;
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

    private static bool TryFindClientVersion(
        ReadOnlySpan<byte> payload,
        out int offset,
        out string token,
        out ClientVersionId versionId)
    {
        var searchLimit = Math.Min(payload.Length - 8, 12);
        for (var i = 1; i <= searchLimit; i++)
        {
            var candidate = Encoding.ASCII.GetString(payload.Slice(i, 8));
            var candidateId = GraalVersionCatalog.GetClientVersionId(candidate);
            if (candidateId == ClientVersionId.Unknown)
                continue;

            offset = i;
            token = candidate;
            versionId = candidateId;
            return true;
        }

        offset = -1;
        token = string.Empty;
        versionId = ClientVersionId.Unknown;
        return false;
    }

    private static string BuildDebugInfo(byte[] loginPayload, byte? key)
    {
        var previewLength = Math.Min(loginPayload.Length, 96);
        var preview = Convert.ToHexString(loginPayload.AsSpan(0, previewLength));
        return $"loginHex={preview}; loginBytes={loginPayload.Length}; key={(key is null ? "none" : key.Value.ToString())}";
    }

    private static bool LooksLikeZlib(ReadOnlySpan<byte> payload) =>
        payload.Length >= 2 &&
        payload[0] == 0x78 &&
        (((payload[0] << 8) + payload[1]) % 31) == 0;

    private static byte[] ZlibDecompress(ReadOnlySpan<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
