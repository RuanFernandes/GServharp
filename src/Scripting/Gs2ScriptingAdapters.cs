using System.Runtime.CompilerServices;
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

public sealed record Gs2ScriptLoadResult(bool Success, string Error = "")
{
    public static Gs2ScriptLoadResult Failed(string error) => new(false, error);
}

public sealed record Gs2ScriptRunResult(bool Success, IReadOnlyList<string> Output, string Error = "")
{
    public static Gs2ScriptRunResult Failed(string error, IReadOnlyList<string>? output = null) =>
        new(false, output ?? [], error);
}

public sealed class Gs2ServerScriptHost
{
    private readonly Gs2ScriptRuntime _runtime = new();
    private readonly List<string> _output = [];
    private static readonly ConditionalWeakTable<Script, Gs2ServerScriptHost> Owners = new();
    private static bool _globalsRegistered;

    public Gs2ServerScriptHost()
    {
        RegisterGlobals();
    }

    public IReadOnlyList<string> Output => _output;

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

    public async Task<Gs2ScriptRunResult> Call(string scriptName, string eventName)
    {
        if (!_runtime.TryGet(scriptName, out var script))
            return Gs2ScriptRunResult.Failed($"script '{scriptName}' is not loaded");

        var start = _output.Count;
        try
        {
            await script.Call(eventName).ConfigureAwait(false);
            return new Gs2ScriptRunResult(true, _output.Skip(start).ToArray());
        }
        catch (Exception ex)
        {
            return Gs2ScriptRunResult.Failed(ex.Message, _output.Skip(start).ToArray());
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
                { "echo", "", Echo }
            });

        foreach (var property in Script.GlobalProperties.Where(entry => !entry.Value.Compiled))
            property.Value.Compile();

        _globalsRegistered = true;
    }

    private static int Echo(Script script, IStackEntry[] args)
    {
        if (Owners.TryGetValue(script, out var host))
            host._output.Add(args.Length == 0 ? "" : args[0]?.GetValue()?.ToString() ?? "");

        return 0;
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
