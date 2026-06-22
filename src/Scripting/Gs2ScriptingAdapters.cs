using System.Runtime.CompilerServices;
using System.Text;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Compiler;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;
using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.GServer.Scripting;

public sealed record Gs2CompileResult(bool Success, byte[] Bytecode, string Error)
{
    public static Gs2CompileResult Failed(string error) => new(false, [], error);
}

public sealed class Gs2CompilerAdapter
{
    public Gs2CompileResult Compile(string source, string type, string name, bool withHeader = true)
    {
        var response = Interface.CompileCode(source, type, name, withHeader);
        return response.Success
            ? new Gs2CompileResult(true, response.ByteCode ?? [], "")
            : Gs2CompileResult.Failed(response.ErrMsg ?? "GS2 compile failed.");
    }
}

public sealed class Gs2ScriptRuntime
{
    private readonly Dictionary<string, Script> _scripts = new(StringComparer.OrdinalIgnoreCase);

    public bool Load(string name, byte[] bytecode)
    {
        if (bytecode.Length == 0)
            return false;

        _scripts[name] = new Script(name, bytecode);
        return true;
    }

    public bool TryGet(string name, out Script script) => _scripts.TryGetValue(name, out script!);
}

public sealed class Gs2PlayerProperties : ScriptProperties<Gs2PlayerContext>
{
    public Gs2PlayerProperties() : base(typeof(ScriptVariable))
    {
        AddFunctions(
            this,
            new FunctionDefinitions<Gs2PlayerContext>
            {
                { "sendpm", "", SendPm },
                { "sendPM", "", SendPm },
                { "addweapon", "", AddWeapon },
                { "addWeapon", "", AddWeapon },
                { "removeweapon", "", RemoveWeapon },
                { "removeWeapon", "", RemoveWeapon },
                { "hasrightflag", "", HasRightFlag }
            });
        AddProperties(
            this,
            new()
            {
                { "account", "", player => player.Account },
                { "nick", "", player => player.Nick, (player, value) => player.Nick = value },
                { "nickname", "", player => player.Nick, (player, value) => player.Nick = value },
                { "level", "", player => player.Level, (player, value) => player.Level = value }
            });
        Compile();
    }

    private static int SendPm(Gs2PlayerContext player, IStackEntry[] args)
    {
        if (args.Length > 0)
            player.Messages.Add(new Gs2PlayerMessage(player.Account, args[0]?.GetValue()?.ToString() ?? ""));
        return 0;
    }

    private static int AddWeapon(Gs2PlayerContext player, IStackEntry[] args)
    {
        if (args.Length > 0)
            player.WeaponActions.Add(new Gs2WeaponAction(player.Account, args[0]?.GetValue()?.ToString() ?? "", true));
        return 0;
    }

    private static int RemoveWeapon(Gs2PlayerContext player, IStackEntry[] args)
    {
        if (args.Length > 0)
            player.WeaponActions.Add(new Gs2WeaponAction(player.Account, args[0]?.GetValue()?.ToString() ?? "", false));
        return 0;
    }

    private static bool HasRightFlag(Gs2PlayerContext player, IStackEntry[] args) => false;
}

public sealed class Gs2PlayerContext : ScriptVariable
{
    public new static readonly Gs2PlayerProperties PropertiesInstance = [];
    public override IScriptProperties Properties => PropertiesInstance;
    public string Account { get; set; } = "";
    public string Nick { get; set; } = "";
    public string Level { get; set; } = "";
    public List<Gs2PlayerMessage> Messages { get; } = [];
    public List<Gs2WeaponAction> WeaponActions { get; } = [];
}

public sealed record Gs2ScriptLoadResult(bool Success, string Error = "")
{
    public static Gs2ScriptLoadResult Failed(string error) => new(false, error);
}

public sealed record Gs2PlayerMessage(string Account, string Message);

public sealed record Gs2WeaponAction(string Account, string WeaponName, bool Add);

public sealed record Gs2ScriptRunResult(bool Success, IReadOnlyList<string> Output, IReadOnlyList<string> ClientTriggers, IReadOnlyList<Gs2PlayerMessage> PlayerMessages, IReadOnlyList<Gs2WeaponAction> WeaponActions, string Error = "")
{
    public static Gs2ScriptRunResult Failed(string error, IReadOnlyList<string>? output = null) =>
        new(false, output ?? [], [], [], [], error);
}

public sealed class Gs2ServerScriptHost
{
    private readonly Gs2ScriptRuntime _runtime = new();
    private readonly List<string> _output = [];
    private readonly List<string> _clientTriggers = [];
    private IReadOnlyDictionary<string, string> _serverFlags = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, string> _serverOptions = new Dictionary<string, string>();
    private Gs2PlayerContext _player = new();
    private static readonly AsyncLocal<Gs2PlayerContext?> CurrentPlayer = new();
    private static readonly ConditionalWeakTable<Script, Gs2ServerScriptHost> Owners = new();
    private static bool _globalsRegistered;

    public Gs2ServerScriptHost()
    {
        RegisterGlobals();
    }

    public IReadOnlyList<string> Output => _output;

    public void SetEnvironment(
        IReadOnlyDictionary<string, string>? serverFlags = null,
        IReadOnlyDictionary<string, string>? serverOptions = null)
    {
        _serverFlags = serverFlags ?? new Dictionary<string, string>();
        _serverOptions = serverOptions ?? new Dictionary<string, string>();
    }

    public void SetPlayer(string account, string nick = "", string level = "")
    {
        _player = new Gs2PlayerContext
        {
            Account = account,
            Nick = nick.Length == 0 ? account : nick,
            Level = level
        };
    }

    public static string NormalizeServerSource(string source)
    {
        var body = source.TrimStart().StartsWith("function ", StringComparison.Ordinal)
            ? NormalizeSingleLineFunction(source)
            : "function onCreated() {\n" + source.TrimEnd() + "\n}";

        return body.Contains("//#CLIENTSIDE", StringComparison.Ordinal)
            ? body
            : "//#CLIENTSIDE\n//#GS2\n" + body;
    }

    public IReadOnlyList<string> FunctionNames(string name) =>
        _runtime.TryGet(name, out var script)
            ? script.Functions.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];

    public Gs2ScriptLoadResult LoadWeapon(string name, byte[] bytecode)
    {
        try
        {
            return _runtime.Load(name, bytecode)
                ? new Gs2ScriptLoadResult(true)
                : Gs2ScriptLoadResult.Failed("empty bytecode");
        }
        catch (Exception ex)
        {
            return Gs2ScriptLoadResult.Failed(ex.Message);
        }
        finally
        {
            if (_runtime.TryGet(name, out var script))
            {
                Owners.Remove(script);
                Owners.Add(script, this);
            }
        }
    }

    public async Task<Gs2ScriptRunResult> Call(string scriptName, string eventName, params string[] args)
    {
        if (!_runtime.TryGet(scriptName, out var script))
            return Gs2ScriptRunResult.Failed($"script '{scriptName}' is not loaded");

        var start = _output.Count;
        var triggerStart = _clientTriggers.Count;
        var messageStart = _player.Messages.Count;
        var weaponStart = _player.WeaponActions.Count;
        try
        {
            var previousPlayer = CurrentPlayer.Value;
            CurrentPlayer.Value = _player;
            Script.GlobalVariables.AddOrUpdate("name", scriptName.ToStackEntry());
            Script.GlobalVariables.AddOrUpdate("params", args.Cast<object?>().ToList().ToStackEntry());
            RegisterGlobalObject("server", BuildFlagObject(_serverFlags, "server."));
            RegisterGlobalObject("serverr", BuildFlagObject(_serverFlags, "serverr."));
            RegisterGlobalObject("serveroptions", BuildObject(_serverOptions));
            RegisterGlobalObject("player", _player);
            try
            {
                await script.Call(eventName, args).ConfigureAwait(false);
                return new Gs2ScriptRunResult(true, _output.Skip(start).ToArray(), _clientTriggers.Skip(triggerStart).ToArray(), _player.Messages.Skip(messageStart).ToArray(), _player.WeaponActions.Skip(weaponStart).ToArray());
            }
            finally
            {
                CurrentPlayer.Value = previousPlayer;
            }
        }
        catch (Exception ex)
        {
            return new Gs2ScriptRunResult(false, _output.Skip(start).ToArray(), _clientTriggers.Skip(triggerStart).ToArray(), _player.Messages.Skip(messageStart).ToArray(), _player.WeaponActions.Skip(weaponStart).ToArray(), ex.Message);
        }
    }

    private static void RegisterGlobals()
    {
        if (_globalsRegistered)
            return;

        Preagonal.Scripting.GS2Engine.Models.ScriptProperties<Script>.AddFunctions(
            null,
            new FunctionDefinitions<Script>
            {
                { "echo", "", Echo },
                { "trace", "", Echo },
                { "printf", "", Echo },
                { "sendtonc", "", Echo },
                { "base64encode", "", Base64Encode },
                { "base64decode", "", Base64Decode },
                { "getimgwidth", "", ImageSize },
                { "getimgheight", "", ImageSize },
                { "findplayer", "", FindPlayer },
                { "findPlayer", "", FindPlayer },
                { "sendpm", "", SendPm },
                { "sendPM", "", SendPm },
                { "addweapon", "", AddWeapon },
                { "addWeapon", "", AddWeapon },
                { "removeweapon", "", RemoveWeapon },
                { "removeWeapon", "", RemoveWeapon },
                { "triggerclient", "", TriggerClient }
            });
        Preagonal.Scripting.GS2Engine.Models.ScriptProperties<Script>.AddProperties(
            null,
            new()
            {
                { "player", "", _ => CurrentPlayer.Value ?? new Gs2PlayerContext() },
                { "screenwidth", "", _ => 1024 },
                { "screenheight", "", _ => 1024 },
                { "TAB", "", _ => "\t" },
                { "NL", "", _ => "\n" },
                { "NULL", "", _ => "" },
                { "nil", "", _ => "" }
            });

        foreach (var property in Script.GlobalProperties.Where(entry => !entry.Value.Compiled))
            property.Value.Compile();

        _globalsRegistered = true;
    }

    private static int Echo(Script script, IStackEntry[] args)
    {
        if (Owners.TryGetValue(script, out var host))
            host._output.Add(args.Length == 0 ? "" : StackValueToString(args[0]));

        return 0;
    }

    private static int TriggerClient(Script script, IStackEntry[] args)
    {
        if (args.Length < 2 || !Owners.TryGetValue(script, out var host))
            return 0;

        var type = args[0]?.GetValue()?.ToString() ?? "";
        if (!type.Equals("gui", StringComparison.OrdinalIgnoreCase) &&
            !type.Equals("weapon", StringComparison.OrdinalIgnoreCase))
            return 0;

        var parts = args.Skip(1).Select(static arg => StackValueToString(arg).Replace('\t', ' '));
        host._clientTriggers.Add("clientside," + string.Join(',', parts));
        return 0;
    }

    private static object Base64Encode(Script script, IStackEntry[] args)
    {
        var value = args.Length == 0 ? "" : args[0]?.GetValue()?.ToString() ?? "";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static object Base64Decode(Script script, IStackEntry[] args)
    {
        var value = args.Length == 0 ? "" : args[0]?.GetValue()?.ToString() ?? "";
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return "";
        }
    }

    private static int ImageSize(Script script, IStackEntry[] args) => 1;

    private static IStackEntry FindPlayer(Script script, IStackEntry[] args)
    {
        var target = args.Length == 0 ? "" : args[0]?.GetValue()?.ToString() ?? "";
        var player = CurrentPlayer.Value ?? new Gs2PlayerContext();
        return target.Length == 0 ||
               target.Equals(player.Account, StringComparison.OrdinalIgnoreCase) ||
               target.Equals(player.Nick, StringComparison.OrdinalIgnoreCase)
            ? player.ToStackEntry()
            : 0.ToStackEntry();
    }

    private static int SendPm(Script script, IStackEntry[] args)
    {
        var player = CurrentPlayer.Value;
        if (player is not null && args.Length > 1)
            player.Messages.Add(new Gs2PlayerMessage(args[0]?.GetValue()?.ToString() ?? "", args[1]?.GetValue()?.ToString() ?? ""));
        return 0;
    }

    private static int AddWeapon(Script script, IStackEntry[] args)
    {
        var player = CurrentPlayer.Value;
        if (player is not null && args.Length > 0)
            player.WeaponActions.Add(new Gs2WeaponAction(player.Account, args[0]?.GetValue()?.ToString() ?? "", true));
        return 0;
    }

    private static int RemoveWeapon(Script script, IStackEntry[] args)
    {
        var player = CurrentPlayer.Value;
        if (player is not null && args.Length > 0)
            player.WeaponActions.Add(new Gs2WeaponAction(player.Account, args[0]?.GetValue()?.ToString() ?? "", false));
        return 0;
    }

    private static string StackValueToString(IStackEntry? entry)
    {
        var value = entry?.GetValue();
        return value switch
        {
            IEnumerable<string> strings => string.Join(",", strings),
            IEnumerable<object?> objects => string.Join(",", objects.Select(static item => item?.ToString() ?? "")),
            _ => value?.ToString() ?? ""
        };
    }

    private static void RegisterGlobalObject(string name, ScriptVariable obj)
    {
        Script.GlobalVariables.AddOrUpdate(name, obj.ToStackEntry());
        Script.GlobalObjects[name] = obj;
    }

    private static ScriptVariable BuildFlagObject(IReadOnlyDictionary<string, string> flags, string prefix)
    {
        var obj = new ScriptVariable();
        foreach (var (key, value) in flags)
        {
            var normalized = key.Trim().ToLowerInvariant();
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                obj.AddOrUpdate(normalized[prefix.Length..], ValueEntry(value));
        }
        if (!obj.ContainsVariable("poopybutthole"))
            obj.AddOrUpdate("poopybutthole", ValueEntry(""));

        return obj;
    }

    private static ScriptVariable BuildObject(IReadOnlyDictionary<string, string> values)
    {
        var obj = new ScriptVariable();
        foreach (var (key, value) in values)
            obj.AddOrUpdate(key.Trim().ToLowerInvariant(), ValueEntry(value));
        if (!obj.ContainsVariable("staff"))
            obj.AddOrUpdate("staff", ValueEntry(""));

        return obj;
    }

    private static IStackEntry ValueEntry(string value)
    {
        var parts = value.Split(',', StringSplitOptions.None);
        return parts.Cast<object?>().ToList().ToStackEntry();
    }

    private static string NormalizeSingleLineFunction(string source)
    {
        var trimmed = source.Trim();
        var closeParen = trimmed.IndexOf(')');
        if (closeParen < 0 || trimmed[(closeParen + 1)..].TrimStart().StartsWith('{'))
            return source;

        var header = trimmed[..(closeParen + 1)];
        var body = trimmed[(closeParen + 1)..].Trim();
        return header + " {\n" + body + "\n}";
    }
}
