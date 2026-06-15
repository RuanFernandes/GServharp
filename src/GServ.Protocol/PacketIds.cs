namespace GServ.Protocol;

/// <summary>
/// Source status for packet ID values recovered from gs2lib's authoritative IEnums.h.
/// </summary>
public static class PacketIdSourceStatus
{
    public const string AuthoritativeEnumHeader = "IEnums.h";
    public const string AuthoritativeRepositoryUrl = "https://xtjoeytx@bitbucket.org/xtjoeytx/gs2lib.git";
    public const string AuthoritativeCommit = "63b1ae96491c188905b50c6b61c8532c601a2122";
    public const bool NumericPacketIdsRecovered = true;
}

/// <summary>
/// Source status for external C++ protocol dependencies that are required before
/// byte-compatible networking can be implemented.
/// </summary>
public static class ProtocolDependencySourceStatus
{
    public const string ExpectedSourceDependency = "gs2lib";
    public const string ExpectedSourceIncludePath = "gs2lib_SOURCE_DIR/include";
    public const string RecoveredRepositoryUrl = "https://xtjoeytx@bitbucket.org/xtjoeytx/gs2lib.git";
    public const string RecoveredCommit = "63b1ae96491c188905b50c6b61c8532c601a2122";
    public const bool IEnumsHeaderRecovered = true;
    public const bool CStringHeaderRecovered = true;
    public const bool CEncryptionHeaderRecovered = true;
    public const bool CFileQueueHeaderRecovered = true;
    public const bool CSocketHeaderRecovered = true;
}

/// <summary>
/// Protocol-critical client-to-server packet IDs confirmed from gs2lib/include/IEnums.h.
/// This is intentionally scoped to packet IDs needed by the protocol foundation.
/// </summary>
public enum ClientPacketId : byte
{
    PLI_BOMBDEL = 5,
    PLI_PACKETCOUNT = 31,
    PLI_RAWDATA = 50,
    PLI_SET_ENC_KEY = 252,
    PLI_BUNDLE = 253,
}

/// <summary>
/// Protocol-critical server-to-client packet IDs confirmed from gs2lib/include/IEnums.h.
/// This is intentionally scoped to packet IDs needed by the protocol foundation.
/// </summary>
public enum ServerPacketId : byte
{
    PLO_DISCMESSAGE = 16,
    PLO_SIGNATURE = 25,
    PLO_LARGEFILESTART = 68,
    PLO_LARGEFILEEND = 69,
    PLO_LARGEFILESIZE = 84,
    PLO_RAWDATA = 100,
    PLO_BOARDPACKET = 101,
    PLO_FILE = 102,
    PLO_SET_ENC_KEY = 252,
    PLO_BUNDLE = 253,
}
