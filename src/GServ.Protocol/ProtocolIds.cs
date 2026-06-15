namespace GServ.Protocol;

public enum PlayerToServerPacketId : byte
{
    LevelWarp = 0,
    BoardModify = 1,
    PlayerProps = 2,
    RawData = 50,
    RequestText = 152,
    SendText = 154,
    SetEncryptionKey = 252,
    Bundle = 253
}

public enum ServerToPlayerPacketId : byte
{
    LevelBoard = 0,
    BaddyProps = 2,
    LevelChest = 4,
    LevelName = 6,
    BoardModify = 7,
    OtherPlayerProps = 8,
    PlayerProps = 9,
    IsLeader = 10,
    PlayerWarp = 14,
    WarpFailed = 15,
    DisconnectMessage = 16,
    HorseAdd = 17,
    Signature = 25,
    FlagSet = 28,
    NpcWeaponDelete = 34,
    LevelModTime = 39,
    NewWorldTime = 42,
    StaffGuilds = 47,
    PlayerWarp2 = 49,
    LargeFileStart = 68,
    LargeFileEnd = 69,
    LargeFileSize = 84,
    RawData = 100,
    BoardPacket = 101,
    File = 102,
    BoardLayer = 107,
    SetActiveLevel = 156,
    Unknown168 = 168,
    GhostIcon = 174,
    ServerListConnected = 190,
    ClearWeapons = 194,
    SetEncryptionKey = 252,
    Bundle = 253
}

public enum ServerToListServerPacketId : byte
{
    PlayerAdd = 14,
    VerifyAccount2 = 17
}

public enum ListServerToServerPacketId : byte
{
    VerifyAccount2 = 11
}

[Flags]
public enum PlayerSessionType
{
    Await = unchecked((int)0x80000000),
    Client = 1 << 0,
    RemoteControl = 1 << 1,
    NpcServer = 1 << 2,
    NpcControl = 1 << 3,
    Client2 = 1 << 4,
    Client3 = 1 << 5,
    RemoteControl2 = 1 << 6,
    External = 1 << 7,
    Web = 1 << 8,
    AnyClient = Client | Client2 | Client3 | Web,
    AnyRemoteControl = RemoteControl | RemoteControl2,
    AnyNpcControl = NpcControl,
    AnyControl = AnyRemoteControl | AnyNpcControl,
    AnyPlayer = AnyClient | AnyRemoteControl,
    NonIterable = NpcServer | AnyNpcControl | External
}

public enum EncryptionGeneration : uint
{
    Gen1 = 0,
    Gen2 = 1,
    Gen3 = 2,
    Gen4 = 3,
    Gen5 = 4,
    Gen6 = 5
}

public enum CompressionType : byte
{
    Uncompressed = 0x02,
    Zlib = 0x04,
    Bz2 = 0x06
}
