namespace Preagonal.GServer.Protocol;

public readonly record struct LoginPrelude(PlayerSessionType Type, EncryptionGeneration InboundGeneration, bool ReadsEncryptionKeyBeforeVersion);

public static class LoginPreludeParser
{
    public static LoginPrelude Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new GraalBinaryReader(payload);
        var shiftedType = 1 << reader.ReadGChar();
        var type = (PlayerSessionType)shiftedType;
        return type switch
        {
            PlayerSessionType.Client => new LoginPrelude(type, EncryptionGeneration.Gen2, false),
            PlayerSessionType.RemoteControl => new LoginPrelude(type, EncryptionGeneration.Gen3, false),
            PlayerSessionType.NpcServer => new LoginPrelude(type, EncryptionGeneration.Gen3, false),
            PlayerSessionType.NpcControl => new LoginPrelude(type, EncryptionGeneration.Gen3, false),
            PlayerSessionType.Client2 => new LoginPrelude(type, EncryptionGeneration.Gen4, false),
            PlayerSessionType.Client3 => new LoginPrelude(type, EncryptionGeneration.Gen5, false),
            PlayerSessionType.RemoteControl2 => new LoginPrelude(type, EncryptionGeneration.Gen5, true),
            PlayerSessionType.Web => new LoginPrelude(type, EncryptionGeneration.Gen1, false),
            _ => new LoginPrelude(type, EncryptionGeneration.Gen3, false)
        };
    }
}
