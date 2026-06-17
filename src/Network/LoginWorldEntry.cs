using System.Text;
using System.Net;
using Preagonal.GServer.Game;
using Preagonal.GServer.Persistence;
using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed record LoginWorldEntryOptions(
    IAccountPersistenceFileSystem AccountFileSystem,
    IAccountLoadSettings AccountSettings,
    NwLevelFileLoader LevelLoader,
    ILevelLookup LevelLookup,
    AccountLoginOptions AccountLoginOptions);

public static class LoginWorldEntry
{
    public static bool Complete(
        ClientSessionSkeleton session,
        LoginWorldEntryOptions options,
        out byte[] serverListAddPlayerPacket,
        out PostLoginPlayerSnapshot snapshot)
    {
        serverListAddPlayerPacket = [];
        snapshot = EmptySnapshot(session);
        var accountLogin = AccountLoginBoundary.Begin(
            session,
            options.AccountFileSystem,
            options.AccountSettings,
            options.AccountLoginOptions);
        if (!accountLogin.Accepted || accountLogin.Account is null)
            return false;

        var account = accountLogin.Account;
        snapshot = BuildSnapshot(session, account, options.AccountLoginOptions.RemoteIp);
        var postLogin = PostLoginWorldEntryBoundary.BeginClient(session, snapshot);
        serverListAddPlayerPacket = postLogin.ServerListAddPlayerPacket;

        var levelName = string.IsNullOrWhiteSpace(account.LevelName)
            ? "onlinestartlocal.nw"
            : account.LevelName;
        var entry = WarpWorldEntryBoundary.BeginSetLevel(
            session,
            options.LevelLookup,
            new LevelWarpRequest(
                levelName,
                account.PixelX / 16.0f,
                account.PixelY / 16.0f,
                account.PixelZ / 16.0f,
                session.LoginPacket!.VersionId,
                ModTime: 0));
        if (!entry.Accepted)
            return false;

        var loaded = options.LevelLoader.TryLoad(levelName);
        if (!loaded.Success)
            return false;

        SendLevelBoundary.BeginModern(
            session,
            ModernLevelPayload.FromNwStatic(loaded.ToModernStaticPayload(chest => account.Chests.Contains(chest, StringComparer.Ordinal))),
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 0, FromAdjacent: false));
        return true;
    }

    private static PostLoginPlayerSnapshot EmptySnapshot(ClientSessionSkeleton session) =>
        new(
            session.Id,
            session.Type,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            new PlayerPropertySource(
                Nickname: "",
                MaxPower: 0,
                Hitpoints: 0,
                Rupees: 0,
                Arrows: 0,
                Bombs: 0,
                GlovePower: 0,
                SwordPower: 0,
                SwordImage: "",
                ShieldPower: 0,
                ShieldImage: "",
                Gani: "",
                HeadImage: "",
                ChatMessage: "",
                Colors: [],
                PlayerId: session.Id,
                X: 0,
                Y: 0,
                Sprite: 0,
                Status: 0,
                CarrySprite: 0,
                CurrentLevel: "",
                HorseImage: "",
                HorseBombCount: 0,
                CarryNpcId: 0,
                ApCounter: 0,
                MagicPoints: 0,
                Kills: 0,
                Deaths: 0,
                OnlineSeconds: 0,
                AccountIp: 0,
                Alignment: 0,
                AdditionalFlags: 0,
                AccountName: "",
                BodyImage: "",
                EloRating: 1500,
                EloDeviation: 350,
                GaniAttributes: new Dictionary<int, string>(),
                Os: "",
                TextCodePage: 1252,
                CommunityName: ""),
            [],
            [],
            []);

    private static PostLoginPlayerSnapshot BuildSnapshot(ClientSessionSkeleton session, AccountFileData account, string remoteIp)
    {
        var source = BuildPropertySource(session, account, remoteIp);
        return new PostLoginPlayerSnapshot(
            session.Id,
            session.Type,
            GCharString(account.AccountName),
            GCharString(account.Nickname),
            GCharString(account.LevelName),
            [(byte)Math.Clamp(account.PixelX / 8, 0, 223)],
            [(byte)Math.Clamp(account.PixelY / 8, 0, 223)],
            [account.Alignment],
            IpProperty(remoteIp),
            source,
            SendLoginPropertySet.ForClient(session.LoginPacket is { VersionId: < ClientVersionId.Client21 }),
            account.Flags.Select(flag => new LoginFlag(flag.Key, flag.Value)).ToArray(),
            []);
    }

    private static PlayerPropertySource BuildPropertySource(ClientSessionSkeleton session, AccountFileData account, string remoteIp) =>
        new(
            Nickname: account.Nickname,
            MaxPower: account.MaxHitpoints,
            Hitpoints: account.Hitpoints,
            Rupees: account.Rupees,
            Arrows: account.Arrows,
            Bombs: account.Bombs,
            GlovePower: account.GlovePower,
            SwordPower: account.SwordPower,
            SwordImage: account.SwordImage,
            ShieldPower: account.ShieldPower,
            ShieldImage: account.ShieldImage,
            Gani: account.Gani,
            HeadImage: account.HeadImage,
            ChatMessage: "",
            Colors: account.Colors,
            PlayerId: session.Id,
            X: account.PixelX,
            Y: account.PixelY,
            Sprite: account.Sprite,
            Status: unchecked((byte)account.Status),
            CarrySprite: 0,
            CurrentLevel: account.LevelName,
            HorseImage: "",
            HorseBombCount: 0,
            CarryNpcId: 0,
            ApCounter: account.ApCounter,
            MagicPoints: account.MagicPoints,
            Kills: unchecked((int)account.Kills),
            Deaths: unchecked((int)account.Deaths),
            OnlineSeconds: account.OnlineSeconds,
            AccountIp: account.AccountIp,
            Alignment: account.Alignment,
            AdditionalFlags: 0,
            AccountName: account.AccountName,
            BodyImage: account.BodyImage,
            EloRating: (int)account.EloRating,
            EloDeviation: (int)account.EloDeviation,
            GaniAttributes: account.GaniAttributes
                .Select((value, index) => (value, index))
                .Where(entry => entry.value is not null)
                .ToDictionary(entry => entry.index + 1, entry => entry.value ?? ""),
            Os: session.LoginPacket?.Identity ?? "",
            TextCodePage: 1252,
            CommunityName: account.CommunityName,
            Z: account.PixelZ,
            BowPower: account.BowPower,
            BowImage: account.BowImage,
            Language: account.Language);

    private static byte[] GCharString(string value)
    {
        var writer = new GraalBinaryWriter();
        var bytes = Encoding.ASCII.GetBytes(value);
        writer.WriteGChar((byte)Math.Min(bytes.Length, 223));
        writer.WriteBytes(bytes.AsSpan(0, Math.Min(bytes.Length, 223)));
        return writer.ToArray();
    }

    private static byte[] IpProperty(string remoteIp)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGInt5(ParseIpv4(remoteIp));
        return writer.ToArray();
    }

    private static uint ParseIpv4(string remoteIp)
    {
        if (!IPAddress.TryParse(remoteIp, out var ip))
            return 0;

        var bytes = ip.MapToIPv4().GetAddressBytes();
        return ((uint)bytes[0] << 24) |
            ((uint)bytes[1] << 16) |
            ((uint)bytes[2] << 8) |
            bytes[3];
    }
}

public sealed class FileLevelLookup(NwLevelFileLoader loader) : ILevelLookup
{
    public LevelEntrySnapshot? FindLevel(string levelName)
    {
        var loaded = loader.TryLoad(levelName);
        return loaded.Success ? new LevelEntrySnapshot(loaded.LevelName) : null;
    }
}
