namespace Preagonal.GServer.Admin;

[Flags]
public enum AdminRight
{
    None = 0,
    WarpTo = 0x00001,
    WarpToPlayer = 0x00002,
    Summon = 0x00004,
    UpdateLevel = 0x00008,
    Disconnect = 0x00010,
    ViewAttributes = 0x00020,
    SetAttributes = 0x00040,
    SetSelfAttributes = 0x00080,
    ResetAttributes = 0x00100,
    AdminMessage = 0x00200,
    SetRights = 0x00400,
    Ban = 0x00800,
    SetComments = 0x01000,
    Invisible = 0x02000,
    ModifyStaffAccount = 0x04000,
    SetServerFlags = 0x08000,
    SetServerOptions = 0x10000,
    SetFolderOptions = 0x20000,
    SetFolderRights = 0x40000,
    NpcControl = 0x80000,
    AnyRight = 0xFFFFFF
}

public static class AdminRights
{
    public static bool HasRight(AdminRight rights, AdminRight mask) => (rights & mask) != 0;
}
