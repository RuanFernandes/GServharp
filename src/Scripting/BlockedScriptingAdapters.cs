namespace GServ.Scripting;

public sealed class BlockedGs2CompilerAdapter
{
    public void Compile(string source)
    {
        _ = source;
        throw new NotSupportedException(
            "gs2compiler execution is blocked until the exact original dependency commit, native loading, bytecode headers, and error behavior are ported.");
    }
}

public sealed class BlockedScriptRuntime
{
    public void QueueAction(string action)
    {
        _ = action;
        throw new NotSupportedException(
            "V8NPCSERVER script execution is blocked until the original V8 lifecycle, sandbox, bindings, and error behavior are ported.");
    }
}
