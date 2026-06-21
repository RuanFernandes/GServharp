using Preagonal.GServer.Network;
using Preagonal.GServer.Persistence;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class PlayerSendLoginContinuationTests
{
    [Fact]
    public void AccountLoginLoadsAccountSnapshotAndRunsContinuationChecks()
    {
        var session = AuthenticatedClient3Session();
        var filesystem = new MemoryAccountFileSystem(@"C:\GServer\");
        filesystem.AddExisting(
            @"C:\GServer\accounts\Ruan.txt",
            "Ruan.txt",
            string.Join(
                "\n",
                "GRACC001",
                "BANNED 1",
                "BANREASON source-confirmed",
                "LOCALRIGHTS 16384",
                "IPRANGE 127.0.0.*"));

        var result = AccountLoginBoundary.Begin(
            session,
            filesystem,
            AccountLoadSettings.Empty,
            new AccountLoginOptions(
                OnlyStaff: false,
                ServerName: "Graal Reborn",
                ActiveSessions: [],
                StaffAccounts: [],
                RemoteIp: "127.0.0.55"));

        Assert.True(result.AccountLoaded);
        Assert.True(result.Accepted);
        Assert.False(result.GuestIdentityBlocked);
        Assert.Null(result.CreatedAccountSave);
        Assert.Equal(
            OutboundLoginPackets.Signature(appendNewline: true)
                .Concat(OutboundLoginPackets.Unknown168(appendNewline: true))
                .ToArray(),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void AccountLoginSavesDefaultCreatedAccountBeforeContinuation()
    {
        var session = AuthenticatedClient3Session();
        var filesystem = new MemoryAccountFileSystem(@"C:\GServer\");
        filesystem.AddReadable(
            @"C:\GServer\accounts\defaultaccount.txt",
            "GRACC001\nLEVEL ignored.nw\nLOADONLY 0");
        var settings = new AccountLoadSettings(new Dictionary<string, string>
        {
            ["startlevel"] = "onlinestartlocal.nw"
        });

        var result = AccountLoginBoundary.Begin(
            session,
            filesystem,
            settings,
            new AccountLoginOptions(
                OnlyStaff: false,
                ServerName: "Graal Reborn",
                ActiveSessions: [],
                StaffAccounts: [],
                RemoteIp: "127.0.0.1"));

        Assert.True(result.Accepted);
        Assert.True(result.AccountLoaded);
        Assert.NotNull(result.CreatedAccountSave);
        Assert.Equal(@"accounts/Ruan.txt", result.CreatedAccountSave!.AccountFileAdded);
        Assert.Contains("LEVEL onlinestartlocal.nw", result.CreatedAccountSave.Contents);
        Assert.Contains(@"accounts/Ruan.txt", filesystem.AddedFiles);
    }

    [Fact]
    public void AccountLoginKeepsGuestIdentityGenerationBlocked()
    {
        var session = AuthenticatedClient3Session("guest");
        session.ReceiveServerListAuthResponse(
            new ServerListVerifyAccount2Response("guest", session.Id, PlayerSessionType.Client3, "SUCCESS"));
        var filesystem = new MemoryAccountFileSystem(@"C:\GServer\");
        filesystem.AddExisting(
            @"C:\GServer\accounts\guest.txt",
            "guest.txt",
            "GRACC001\nLOADONLY 0");

        var result = AccountLoginBoundary.Begin(
            session,
            filesystem,
            AccountLoadSettings.Empty,
            new AccountLoginOptions(
                OnlyStaff: false,
                ServerName: "Graal Reborn",
                ActiveSessions: [],
                StaffAccounts: [],
                RemoteIp: "127.0.0.1"));

        Assert.False(result.Accepted);
        Assert.True(result.AccountLoaded);
        Assert.True(result.GuestIdentityBlocked);
        Assert.Empty(session.TakeOutboundBytes());
    }

    [Fact]
    public void AccountLoginUsesConfirmedGuestIdentitySelectionWhenGeneratorIsProvided()
    {
        var session = AuthenticatedClient3Session("guest");
        session.ReceiveServerListAuthResponse(
            new ServerListVerifyAccount2Response("guest", session.Id, PlayerSessionType.Client3, "SUCCESS"));
        var filesystem = new MemoryAccountFileSystem(@"C:\GServer\");
        filesystem.AddExisting(
            @"C:\GServer\accounts\guest.txt",
            "guest.txt",
            "GRACC001\nLOADONLY 0\nIPRANGE 0.0.0.0");

        var result = AccountLoginBoundary.Begin(
            session,
            filesystem,
            AccountLoadSettings.Empty,
            new AccountLoginOptions(
                OnlyStaff: false,
                ServerName: "Graal Reborn",
                ActiveSessions:
                [
                    new ActivePlayerSession(12, "PC:123456", PlayerSessionType.Client3, TimeSpan.FromSeconds(1))
                ],
                StaffAccounts: [],
                RemoteIp: "127.0.0.1",
                GuestIdentitySelector: new CandidateGuestIdentitySelector([1234567, 7654321])));

        Assert.True(result.Accepted);
        Assert.True(result.AccountLoaded);
        Assert.False(result.GuestIdentityBlocked);
        Assert.Equal("pc:765432", result.GuestAccountName);
        Assert.Equal(
            OutboundLoginPackets.Signature(appendNewline: true)
                .Concat(OutboundLoginPackets.Unknown168(appendNewline: true))
                .ToArray(),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void AccountSnapshotUsesStaffListAndCppAdminIpWildcardMatching()
    {
        var account = new AccountFileData
        {
            AccountName = "pc:Ruan",
            AdminRights = 0x04000,
            AdminIp = "10.0.0.?,127.0.0.*"
        };

        var snapshot = AccountLoginBoundary.ToPlayerSendLoginAccount(
            account,
            staffAccounts: [" pc:ruan "],
            remoteIp: "127.0.0.44",
            isGuest: false);

        Assert.True(snapshot.HasModifyStaffAccountRight);
        Assert.True(snapshot.IsStaff);
        Assert.True(snapshot.IsAdminIp);
        Assert.Equal(["10.0.0.?", "127.0.0.*"], snapshot.AdminIps);
    }

    [Fact]
    public void BannedAccountRejectsBeforeEarlyLoginPackets()
    {
        var session = AuthenticatedClient3Session();
        var account = BaseAccount() with { IsBanned = true, BanReason = "cheating" };

        var result = PlayerSendLoginContinuation.Begin(session, account, BaseOptions());

        Assert.False(result.Accepted);
        Assert.Equal(SessionLifecycle.Rejected, session.Lifecycle);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("You have been banned.  Reason: cheating", appendNewline: true),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void StaffOnlyServerRejectsNonStaffClientBeforeEarlyLoginPackets()
    {
        var session = AuthenticatedClient3Session();
        var options = BaseOptions() with { OnlyStaff = true };

        var result = PlayerSendLoginContinuation.Begin(session, BaseAccount(), options);

        Assert.False(result.Accepted);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("This server is currently restricted to staff only.", appendNewline: true),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void AdminIpMismatchRejectsClientBeforeEarlyLoginPackets()
    {
        var session = AuthenticatedClient3Session();
        var account = BaseAccount() with { IsAdminIp = false, AdminIps = ["127.0.0.1"] };

        var result = PlayerSendLoginContinuation.Begin(session, account, BaseOptions());

        Assert.False(result.Accepted);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("Your IP doesn't match one of the allowed IPs for this account.", appendNewline: true),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void RcLoginWithoutStaffRightsUsesRcRightsDisconnectMessage()
    {
        var session = AuthenticatedRemoteControlSession();
        var account = BaseAccount() with { IsStaff = false };

        var result = PlayerSendLoginContinuation.Begin(session, account, BaseOptions());

        Assert.False(result.Accepted);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("You do not have RC rights.", appendNewline: true),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void ClientSuccessQueuesSignatureAndUnknown168ThenStopsBeforeWorldEntry()
    {
        var session = AuthenticatedClient3Session();

        var result = PlayerSendLoginContinuation.Begin(session, BaseAccount(), BaseOptions());

        Assert.True(result.Accepted);
        Assert.Equal(SessionLifecycle.ReadyForWorldEntry, session.Lifecycle);
        Assert.Equal(
            OutboundLoginPackets.Signature(appendNewline: true)
                .Concat(OutboundLoginPackets.Unknown168(appendNewline: true))
                .ToArray(),
            session.TakeOutboundBytes());
        Assert.Empty(result.DuplicateDisconnects);
    }

    [Fact]
    public void LoginNamedServerReportsBlockedFullStopBranchWithoutGuessingPacketBytes()
    {
        var session = AuthenticatedClient3Session();
        var options = BaseOptions() with { ServerName = "Classic Login" };

        var result = PlayerSendLoginContinuation.Begin(session, BaseAccount(), options);

        Assert.True(result.Accepted);
        Assert.True(result.LoginServerFullStopBlocked);
        Assert.Equal(
            OutboundLoginPackets.Signature(appendNewline: true)
                .Concat(OutboundLoginPackets.Unknown168(appendNewline: true))
                .ToArray(),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void DuplicateClientKicksOldSession()
    {
        var session = AuthenticatedClient3Session();
        var options = BaseOptions() with
        {
            ActiveSessions =
            [
                new ActivePlayerSession(12, "PC:RUAN", PlayerSessionType.Client3, TimeSpan.FromSeconds(5))
            ]
        };

        var result = PlayerSendLoginContinuation.Begin(session, BaseAccount(), options);

        Assert.True(result.Accepted);
        Assert.Equal(SessionLifecycle.ReadyForWorldEntry, session.Lifecycle);
        var duplicate = Assert.Single(result.DuplicateDisconnects);
        Assert.Equal(12, duplicate.SessionId);
        Assert.Equal("Someone else has logged into your account.", duplicate.Message);
        Assert.Equal(
            OutboundLoginPackets.Signature(appendNewline: true)
                .Concat(OutboundLoginPackets.Unknown168(appendNewline: true))
                .ToArray(),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void StaleDuplicateClientIsMarkedForDisconnectAndCurrentSessionContinues()
    {
        var session = AuthenticatedClient3Session();
        var options = BaseOptions() with
        {
            ActiveSessions =
            [
                new ActivePlayerSession(12, "pc:Ruan", PlayerSessionType.Client3, TimeSpan.FromSeconds(31))
            ]
        };

        var result = PlayerSendLoginContinuation.Begin(session, BaseAccount(), options);

        Assert.True(result.Accepted);
        var duplicate = Assert.Single(result.DuplicateDisconnects);
        Assert.Equal(12, duplicate.SessionId);
        Assert.Equal("Someone else has logged into your account.", duplicate.Message);
        Assert.Equal(SessionLifecycle.ReadyForWorldEntry, session.Lifecycle);
    }

    private static PlayerSendLoginAccount BaseAccount() =>
        new(
            AccountName: "pc:Ruan",
            IsBanned: false,
            BanReason: "",
            HasModifyStaffAccountRight: false,
            IsStaff: false,
            IsAdminIp: true,
            AdminIps: ["0.0.0.0"],
            IsGuest: false);

    private static PlayerSendLoginOptions BaseOptions() =>
        new(
            OnlyStaff: false,
            ServerName: "Graal Reborn",
            ActiveSessions: []);

    private static ClientSessionSkeleton AuthenticatedClient3Session(string accountName = "Ruan")
    {
        var session = new ClientSessionSkeleton(7);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(5);
        packet.WriteGChar(42);
        packet.WriteBytes("G3D0311C"u8);
        packet.WriteGChar((byte)accountName.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(accountName));
        packet.WriteGChar(2);
        packet.WriteBytes("pw"u8);
        packet.WriteBytes("win"u8);
        Assert.True(session.ReceiveLoginPacket(packet.ToArray()));
        if (!string.Equals(accountName, "guest", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(session.ReceiveServerListAuthResponse(
                new ServerListVerifyAccount2Response("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS")));
        }
        return session;
    }

    private static ClientSessionSkeleton AuthenticatedRemoteControlSession()
    {
        var session = new ClientSessionSkeleton(8);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(1);
        packet.WriteGChar(42);
        packet.WriteBytes("GNW2214"u8);
        packet.WriteGChar(4);
        packet.WriteBytes("Ruan"u8);
        packet.WriteGChar(2);
        packet.WriteBytes("pw"u8);
        packet.WriteBytes("win"u8);
        Assert.True(session.ReceiveLoginPacket(packet.ToArray()));
        Assert.True(session.ReceiveServerListAuthResponse(
            new ServerListVerifyAccount2Response("pc:Ruan", 8, PlayerSessionType.RemoteControl, "SUCCESS")));
        return session;
    }

    private sealed class MemoryAccountFileSystem(string serverPath) : IAccountPersistenceFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _findi = new(StringComparer.OrdinalIgnoreCase);

        public List<string> AddedFiles { get; } = [];
        public string ServerPath { get; } = serverPath;

        public string? FindCaseInsensitive(string fileName) =>
            _findi.TryGetValue(fileName, out var path) ? path : null;

        public string? ReadAllText(string path) =>
            _files.TryGetValue(path, out var contents) ? contents : null;

        public string? FileExistsAs(string fileName) =>
            _findi.TryGetValue(fileName, out var path) ? Path.GetFileName(path) : null;

        public bool WriteAllText(string path, string contents)
        {
            _files[path] = contents;
            _findi[Path.GetFileName(path)] = path;
            return true;
        }

        public void AddFile(string relativePath) =>
            AddedFiles.Add(relativePath);

        public void AddExisting(string path, string indexedFileName, string contents)
        {
            AddReadable(path, contents);
            _findi[indexedFileName] = path;
        }

        public void AddReadable(string path, string contents) =>
            _files[path] = contents;
    }
}
