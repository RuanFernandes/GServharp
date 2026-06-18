namespace Preagonal.GServer.Scripting;

public static class ScriptingRuntimeStatus
{
    public const bool IsRuntimeImplemented = false;
    public const string Gs2CompilerRepositoryUrl = "https://github.com/xtjoeytx/gs2-parser.git";
    public const string RecoveredGs2CompilerCommit = "4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9";
    public const bool IsExactOriginalGs2CompilerCommitProven = false;
    public const string ServerSideVmRepositoryUrl = "https://github.com/Preagonal/Preagonal.Scripting.GS2Engine.git";
    public const string ServerSideVmSubmodulePath = "external/gs2engine";
    public const bool IsServerSideVmWired = false;
    public const string Blocker =
        "Client script packets remain gated by C++ V8NPCSERVER and gs2compiler behavior; server-side VM support is recovered under external/gs2engine but not wired yet.";
}
