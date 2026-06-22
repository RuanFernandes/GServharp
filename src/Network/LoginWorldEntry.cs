using System.Text;
using System.Net;
using Preagonal.GServer.Game;
using Preagonal.GServer.Persistence;
using Preagonal.GServer.Protocol;
using Preagonal.GServer.Scripting;

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
        out PostLoginPlayerSnapshot snapshot,
        out IReadOnlyList<DuplicateSessionDisconnect> duplicateDisconnects)
    {
        serverListAddPlayerPacket = [];
        snapshot = EmptySnapshot(session);
        duplicateDisconnects = [];
        var accountLogin = AccountLoginBoundary.Begin(
            session,
            options.AccountFileSystem,
            options.AccountSettings,
            options.AccountLoginOptions);
        if (!accountLogin.Accepted || accountLogin.Account is null)
            return false;

        duplicateDisconnects = accountLogin.DuplicateDisconnects;
        var account = accountLogin.Account;
        snapshot = BuildSnapshot(session, account, options.AccountLoginOptions.RemoteIp);
        if (IsRemoteControl(session.Type))
        {
            var controlSnapshot = snapshot with
            {
                CurrentLevelProperty = GCharString(" "),
                LoginPropertySource = snapshot.LoginPropertySource with
                {
                    CurrentLevel = " ",
                    HeadImage = options.AccountSettings.GetString("staffhead", "head25.png"),
                    X = 0,
                    Y = 0,
                    Z = 0
                }
            };
            snapshot = controlSnapshot;
            var rcPostLogin = PostLoginWorldEntryBoundary.BeginRemoteControl(
                session,
                controlSnapshot,
                new PostLoginRemoteControlOptions(
                    options.AccountFileSystem.ServerPath,
                    options.AccountLoginOptions.ServerName,
                    options.AccountSettings.GetString("staffguilds", ""),
                    options.AccountSettings.GetString(
                        "playerlisticons",
                        options.AccountSettings.GetString("statuslist", "")),
                    MaxUploadBytes: 20 * 1024 * 1024));
            serverListAddPlayerPacket = rcPostLogin.ServerListAddPlayerPacket;
            return true;
        }

        if (IsNpcControl(session.Type))
        {
            snapshot = snapshot with
            {
                CurrentLevelProperty = GCharString(" "),
                LoginPropertySource = snapshot.LoginPropertySource with
                {
                    CurrentLevel = " ",
                    HeadImage = options.AccountSettings.GetString("staffhead", "head25.png"),
                    X = 0,
                    Y = 0,
                    Z = 0
                }
            };
            serverListAddPlayerPacket = PostLoginWorldEntryBoundary.BuildServerListAddPlayerPacket(snapshot);
            return true;
        }

        var postLogin = PostLoginWorldEntryBoundary.BeginClient(
            session,
            snapshot,
            new PostLoginClientOptions(
                ResourceFileSystem: null,
                Maps: [],
                PlayerWeapons: BuildLoginWeaponPackets(account, options.AccountSettings, options.AccountFileSystem.ServerPath)));
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
            [],
            account);
    }

    private static PlayerPropertySource BuildPropertySource(ClientSessionSkeleton session, AccountFileData account, string remoteIp) =>
        new(
            Nickname: DisplayNickname(account.AccountName, account.Nickname),
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

    private static IReadOnlyList<LoginWeaponPacket> BuildLoginWeaponPackets(
        AccountFileData account,
        IAccountLoadSettings settings,
        string serverPath)
    {
        if (!GetBool(settings, "defaultweapons", defaultValue: true))
            return [];

        var packets = new List<LoginWeaponPacket>();
        foreach (var weaponName in account.Weapons)
        {
            var itemType = LevelItemCatalog.GetItemId(weaponName);
            if (itemType != LevelItemType.Invalid)
            {
                packets.Add(new LoginWeaponPacket(
                    weaponName,
                    EntityPackets.DefaultWeapon((byte)itemType)));
                continue;
            }

            if (TryLoadServerWeapon(serverPath, weaponName, out var image, out var source))
            {
                packets.Add(new LoginWeaponPacket(
                    weaponName,
                    EntityPackets.NpcWeaponAdd(weaponName, image, source.Replace('\n', '\u00a7'))));

                if (TryCompileClientGs2(weaponName, source, out var bytecode))
                    packets.Add(new LoginWeaponPacket(
                        weaponName,
                        EntityPackets.NpcWeaponScriptRawData(bytecode)));
            }
        }

        return packets;
    }

    private static bool TryCompileClientGs2(string weaponName, string source, out byte[] bytecode)
    {
        bytecode = [];
        try
        {
            var clientGs2 = SourceCodeSlices.Parse(source, gs2Default: true, serverSideVm: true).ClientGs2;
            if (string.IsNullOrWhiteSpace(clientGs2) || LooksLikeGs1ClientScript(clientGs2))
                return false;

            var result = new Gs2CompilerAdapter().Compile(clientGs2, "weapon", weaponName);
            if (!result.Success || result.Bytecode.Length == 0)
                return false;

            bytecode = result.Bytecode;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeGs1ClientScript(string source) =>
        source.Contains("setplayerprop #", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("setstring ", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("timeout=", StringComparison.OrdinalIgnoreCase);

    private static bool TryLoadServerWeapon(string serverPath, string weaponName, out string image, out string source)
    {
        image = "";
        source = "";
        var safe = Path.GetFileName(weaponName.Replace('\\', '/'));
        var fileName = safe.StartsWith("-", StringComparison.Ordinal) ? "weapon" + safe + ".txt" : "weapon-" + safe + ".txt";
        var path = Path.Combine(serverPath, "weapons", fileName);
        if (!File.Exists(path))
            return false;

        var sourceBuilder = new StringBuilder();
        var inScript = false;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Replace("\r", "", StringComparison.Ordinal);
            if (line.Equals("SCRIPT", StringComparison.Ordinal))
            {
                inScript = true;
                continue;
            }

            if (line.Equals("SCRIPTEND", StringComparison.Ordinal))
                break;

            if (inScript)
            {
                sourceBuilder.Append(line).Append('\n');
                continue;
            }

            if (line.StartsWith("IMAGE ", StringComparison.Ordinal))
                image = line["IMAGE ".Length..].Trim();
        }

        source = sourceBuilder.ToString().TrimEnd('\r', '\n');
        return source.Length != 0;
    }

    private static bool GetBool(IAccountLoadSettings settings, string key, bool defaultValue)
    {
        var value = settings.GetString(key, defaultValue ? "true" : "false");
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    private static bool IsRemoteControl(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyRemoteControl) != 0;

    private static bool IsNpcControl(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyNpcControl) != 0;

    private static string DisplayNickname(string accountName, string nickname)
    {
        var nick = nickname.Trim();
        while (nick.StartsWith('*'))
            nick = nick[1..];

        if (nick.Length == 0 ||
            nick.Equals("default", StringComparison.OrdinalIgnoreCase) ||
            nick.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            nick = accountName;

        return nick.Equals(accountName, StringComparison.OrdinalIgnoreCase)
            ? "*" + accountName
            : nick;
    }

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
