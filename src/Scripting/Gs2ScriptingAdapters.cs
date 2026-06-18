using Preagonal.Scripting.GS2Compiler;
using Preagonal.Scripting.GS2Engine.GS2.Script;

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
