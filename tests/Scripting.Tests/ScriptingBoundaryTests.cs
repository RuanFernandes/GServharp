using Preagonal.GServer.Scripting;
using Xunit;

namespace Preagonal.GServer.Scripting.Tests;

public sealed class ScriptingBoundaryTests
{
    [Fact]
    public void ScriptingRuntimeIsExplicitlyUnimplementedUntilV8BehaviorIsPorted()
    {
        Assert.False(ScriptingRuntimeStatus.IsRuntimeImplemented);
        Assert.Contains("V8NPCSERVER", ScriptingRuntimeStatus.Blocker);
    }

    [Fact]
    public void DependencyStatusDocumentsRecoveredGs2CompilerButOriginalSubmoduleCommitIsUnproven()
    {
        Assert.Equal("https://github.com/xtjoeytx/gs2-parser.git", ScriptingRuntimeStatus.Gs2CompilerRepositoryUrl);
        Assert.Equal("4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9", ScriptingRuntimeStatus.RecoveredGs2CompilerCommit);
        Assert.False(ScriptingRuntimeStatus.IsExactOriginalGs2CompilerCommitProven);
    }

    [Fact]
    public void DependencyStatusDocumentsServerSideVmSubmodule()
    {
        Assert.Equal("https://github.com/Preagonal/Preagonal.Scripting.GS2Engine.git", ScriptingRuntimeStatus.ServerSideVmRepositoryUrl);
        Assert.Equal("external/gs2engine", ScriptingRuntimeStatus.ServerSideVmSubmodulePath);
        Assert.False(ScriptingRuntimeStatus.IsServerSideVmWired);
    }

    [Fact]
    public void NonV8SourceTreatsAllCodeAsClientSideAndDefaultsToGs1()
    {
        var slices = SourceCodeSlices.Parse("echo(\"hi\");", gs2Default: false, v8NpcServer: false);

        Assert.Equal("", slices.ServerSide);
        Assert.Equal("echo(\"hi\");", slices.ClientSide);
        Assert.Equal("echo(\"hi\");", slices.ClientGs1);
        Assert.Equal("", slices.ClientGs2);
    }

    [Fact]
    public void V8SourceSplitsServerAndClientAtClientsideMarker()
    {
        var slices = SourceCodeSlices.Parse("server();\n//#CLIENTSIDE\nclient();", gs2Default: false, v8NpcServer: true);

        Assert.Equal("server();\n", slices.ServerSide);
        Assert.Equal("//#CLIENTSIDE\nclient();", slices.ClientSide);
        Assert.Equal("//#CLIENTSIDE\nclient();", slices.ClientGs1);
        Assert.Equal("", slices.ClientGs2);
    }

    [Fact]
    public void Gs2MarkerSwitchesClientGs2WhenGs2DefaultIsFalse()
    {
        var slices = SourceCodeSlices.Parse("//#CLIENTSIDE\nclientGs1();\n//#GS2\nclientGs2();", gs2Default: false, v8NpcServer: false);

        Assert.Equal("//#CLIENTSIDE\nclientGs1();\n", slices.ClientGs1);
        Assert.Equal("//#GS2\nclientGs2();", slices.ClientGs2);
    }

    [Fact]
    public void Gs1MarkerSwitchesClientGs1WhenGs2DefaultIsTrue()
    {
        var slices = SourceCodeSlices.Parse("//#CLIENTSIDE\nclientGs2();\n//#GS1\nclientGs1();", gs2Default: true, v8NpcServer: false);

        Assert.Equal("//#CLIENTSIDE\nclientGs2();\n", slices.ClientGs2);
        Assert.Equal("//#GS1\nclientGs1();", slices.ClientGs1);
    }

    [Fact]
    public void RuntimeGuardRejectsCompileAndExecuteUntilPorted()
    {
        var compiler = new BlockedGs2CompilerAdapter();
        var runtime = new BlockedScriptRuntime();

        var compileError = Assert.Throws<NotSupportedException>(() => compiler.Compile("//#GS2\nfunction test() {}"));
        var executeError = Assert.Throws<NotSupportedException>(() => runtime.QueueAction("npc.created"));

        Assert.Contains("gs2compiler", compileError.Message);
        Assert.Contains("V8NPCSERVER", executeError.Message);
    }

    [Fact]
    public void ScriptVisibleApiCatalogKeepsRecoveredV8BindingGroupsBlocked()
    {
        var apis = ScriptVisibleApiCatalog.All;

        Assert.Contains(apis, api => api.Name == "server" && api.SourceFile == "V8ServerImpl.cpp");
        Assert.Contains(apis, api => api.Name == "player" && api.SourceFile == "V8PlayerImpl.cpp");
        Assert.Contains(apis, api => api.Name == "npc" && api.SourceFile == "V8NPCImpl.cpp");
        Assert.Contains(apis, api => api.Name == "level" && api.SourceFile == "V8LevelImpl.cpp");
        Assert.Contains(apis, api => api.Name == "weapon" && api.SourceFile == "V8WeaponImpl.cpp");
        Assert.Contains(apis, api => api.Name == "environment" && api.SourceFile == "V8EnvironmentImpl.cpp");
        Assert.All(apis, api => Assert.False(api.IsImplemented));
    }
}
