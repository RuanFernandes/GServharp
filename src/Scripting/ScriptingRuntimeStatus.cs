namespace GServ.Scripting;

public static class ScriptingRuntimeStatus
{
    public const bool IsRuntimeImplemented = false;
    public const string Gs2CompilerRepositoryUrl = "https://github.com/xtjoeytx/gs2-parser.git";
    public const string RecoveredGs2CompilerCommit = "4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9";
    public const bool IsExactOriginalGs2CompilerCommitProven = false;
    public const string Blocker =
        "Original scripting is gated by V8NPCSERVER and gs2compiler behavior; exact original gs2compiler submodule commit is still unproven.";
}
