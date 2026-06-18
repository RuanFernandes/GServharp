using Preagonal.GServer.Scripting;
using Xunit;

namespace Preagonal.GServer.Scripting.Tests;

public sealed class ScriptingBoundaryTests
{
    [Fact]
    public void RuntimeUsesGs2()
    {
        Assert.True(ScriptingRuntimeStatus.IsRuntimeImplemented);
        Assert.Contains("GS2Engine", ScriptingRuntimeStatus.Blocker);
    }

    [Fact]
    public void DependencyStatusDocumentsRecoveredGs2CompilerButOriginalSubmoduleCommitIsUnproven()
    {
        Assert.Equal("https://github.com/xtjoeytx/gs2-parser.git", ScriptingRuntimeStatus.Gs2CompilerRepositoryUrl);
        Assert.Equal("4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9", ScriptingRuntimeStatus.RecoveredGs2CompilerCommit);
        Assert.False(ScriptingRuntimeStatus.IsExactOriginalGs2CompilerCommitProven);
    }

    [Fact]
    public void VmUsesNuget()
    {
        Assert.Equal("https://github.com/Preagonal/Preagonal.Scripting.GS2Engine.git", ScriptingRuntimeStatus.ServerSideVmRepositoryUrl);
        Assert.Equal("Preagonal.Scripting.GS2Engine", ScriptingRuntimeStatus.ServerSideVmPackage);
        Assert.True(ScriptingRuntimeStatus.IsServerSideVmWired);
    }

    [Fact]
    public void ClientOnlyDefaultsGs1()
    {
        var slices = SourceCodeSlices.Parse("echo(\"hi\");", gs2Default: false, serverSideVm: false);

        Assert.Equal("", slices.ServerSide);
        Assert.Equal("echo(\"hi\");", slices.ClientSide);
        Assert.Equal("echo(\"hi\");", slices.ClientGs1);
        Assert.Equal("", slices.ClientGs2);
    }

    [Fact]
    public void ServerVmSplitsClientside()
    {
        var slices = SourceCodeSlices.Parse("server();\n//#CLIENTSIDE\nclient();", gs2Default: false, serverSideVm: true);

        Assert.Equal("server();\n", slices.ServerSide);
        Assert.Equal("//#CLIENTSIDE\nclient();", slices.ClientSide);
        Assert.Equal("//#CLIENTSIDE\nclient();", slices.ClientGs1);
        Assert.Equal("", slices.ClientGs2);
    }

    [Fact]
    public void Gs2MarkerSelectsClientGs2()
    {
        var slices = SourceCodeSlices.Parse("//#CLIENTSIDE\nclientGs1();\n//#GS2\nclientGs2();", gs2Default: false, serverSideVm: false);

        Assert.Equal("//#CLIENTSIDE\nclientGs1();\n", slices.ClientGs1);
        Assert.Equal("//#GS2\nclientGs2();", slices.ClientGs2);
    }

    [Fact]
    public void Gs1MarkerSelectsClientGs1()
    {
        var slices = SourceCodeSlices.Parse("//#CLIENTSIDE\nclientGs2();\n//#GS1\nclientGs1();", gs2Default: true, serverSideVm: false);

        Assert.Equal("//#CLIENTSIDE\nclientGs2();\n", slices.ClientGs2);
        Assert.Equal("//#GS1\nclientGs1();", slices.ClientGs1);
    }

    [Fact]
    public void CompilerBuildsBytecode()
    {
        var compiler = new Gs2CompilerAdapter();

        var result = compiler.Compile("//#CLIENTSIDE\nfunction onCreated() {\n}", "weapon", "test");

        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(result.Bytecode);
    }

    [Fact]
    public void ApiCatalogTracksGs2EngineBindings()
    {
        var apis = ScriptVisibleApiCatalog.All;

        Assert.Contains(apis, api => api.Name == "server" && api.SourceFile == "GS2Engine");
        Assert.Contains(apis, api => api.Name == "player" && api.SourceFile == "GS2Engine");
        Assert.Contains(apis, api => api.Name == "npc" && api.SourceFile == "GS2Engine");
        Assert.Contains(apis, api => api.Name == "level" && api.SourceFile == "GS2Engine");
        Assert.Contains(apis, api => api.Name == "weapon" && api.SourceFile == "GS2Engine");
        Assert.Contains(apis, api => api.Name == "environment" && api.SourceFile == "GS2Engine");
        Assert.All(apis, api => Assert.False(api.IsImplemented));
    }
}
