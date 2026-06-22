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
    public async Task ServerScriptRunsCreatedAndCapturesEcho()
    {
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  echo(1);\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);
        Assert.Contains("oncreated", host.FunctionNames("-gr_movement"));

        var run = await host.Call("-gr_movement", "onCreated");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["1"], run.Output);
    }

    [Fact]
    public async Task ServerScriptCapturesTriggerClient()
    {
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onActionServerSide() {\n  triggerclient(\"gui\", name, \"kek\");\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);

        var run = await host.Call("-gr_movement", "onActionServerSide");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["clientside,-gr_movement,kek"], run.ClientTriggers);
    }

    [Fact]
    public async Task ServerScriptEventsAreCaseInsensitive()
    {
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onActionServerside() {\n  echo(\"hit\");\n  triggerclient(\"gui\", name, \"kek\");\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);

        var run = await host.Call("-gr_movement", "onActionServerSide");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["hit"], run.Output);
        Assert.Equal(["clientside,-gr_movement,kek"], run.ClientTriggers);
    }

    [Fact]
    public async Task ServerScriptUsesParamsArray()
    {
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onActionServerSide() {\n  echo(params[0] SPC params[1]);\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);
        host.SetPlayer("moondeath");

        var run = await host.Call("-gr_movement", "onActionServerSide", "from clientside", "1");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["from clientside 1"], run.Output);
    }

    [Fact]
    public async Task ServerScriptActionCanReadPlayer()
    {
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onActionServerside() {\n  echo(\"test\" SPC params[0] SPC player.account);\n  triggerclient(\"gui\", name, \"kek\");\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);
        host.SetPlayer("moondeath");

        var run = await host.Call("-gr_movement", "onActionServerSide", "from clientside", "1");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["test from clientside moondeath"], run.Output);
        Assert.Equal(["clientside,-gr_movement,kek"], run.ClientTriggers);
    }

    [Fact]
    public async Task MixedWeaponTriggerServerEchoesAndTriggersClient()
    {
        const string source = "function onCreated() {\n echo(\"kek\");\n}\nfunction onActionServerside() {\n   echo(\"test\" SPC params[0] SPC player.account);\n   triggerclient(\"gui\", name, \"kek\");\n}\n//#CLIENTSIDE\n//#GS2\nfunction onActionClientside() {\n  player.chat = \"clientside triggered form server:\" SPC params;\n}\nfunction onCreated() {\n  triggerServer(\"gui\", name, \"from clientside\", 1);\n}";
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            Gs2ServerScriptHost.NormalizeServerSource(SourceCodeSlices.Parse(source, gs2Default: true, serverSideVm: true).ServerSide),
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);
        host.SetPlayer("moondeath");

        var run = await host.Call("-gr_movement", "onActionServerSide", "from clientside", "1");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["test from clientside moondeath"], run.Output);
        Assert.Equal(["clientside,-gr_movement,kek"], run.ClientTriggers);
    }

    [Fact]
    public async Task ServerScriptUsesNcAndBase64Globals()
    {
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  sendtonc(base64decode(base64encode(\"kek\")));\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);

        var run = await host.Call("-gr_movement", "onCreated");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["kek"], run.Output);
    }

    [Fact]
    public async Task ServerScriptUsesCommonGoVmGlobals()
    {
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  trace(screenwidth SPC screenheight SPC getimgwidth(\"head0.png\") SPC getimgheight(\"head0.png\"));\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);

        var run = await host.Call("-gr_movement", "onCreated");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["1024 1024 1 1"], run.Output);
    }

    [Fact]
    public async Task ServerScriptUsesPlayerActionGlobals()
    {
        var host = new Gs2ServerScriptHost();
        host.SetPlayer("moondeath", "*moondeath", "onlinestartlocal.nw");
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  sendpm(\"moondeath\", \"kek\");\n  addweapon(\"-Core\");\n  removeweapon(\"-Old\");\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);

        var run = await host.Call("-gr_movement", "onCreated");

        Assert.True(run.Success, run.Error);
        Assert.Equal([new Gs2PlayerMessage("moondeath", "kek")], run.PlayerMessages);
        Assert.Equal([new Gs2WeaponAction("moondeath", "-Core", true), new Gs2WeaponAction("moondeath", "-Old", false)], run.WeaponActions);
    }

    [Fact]
    public async Task ServerScriptReadsFlagsAndOptionArrays()
    {
        var host = new Gs2ServerScriptHost();
        host.SetEnvironment(
            new Dictionary<string, string> { ["serverr.poopybutthole"] = "testing" },
            new Dictionary<string, string> { ["staff"] = "cadavre,moondeath" });
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  echo(serverr.poopybutthole SPC serveroptions.staff[1]);\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);

        var run = await host.Call("-gr_movement", "onCreated");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["testing moondeath"], run.Output);
    }

    [Fact]
    public async Task MissingFlagIndexSafe()
    {
        var host = new Gs2ServerScriptHost();
        host.SetEnvironment(new Dictionary<string, string>(), new Dictionary<string, string>());
        var compile = new Gs2CompilerAdapter().Compile(
            "//#CLIENTSIDE\n//#GS2\nfunction onCreated() {\n  if (serverr.poopybutthole[0] == true) echo(\"bad\");\n  echo(\"ok\");\n}",
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);

        var run = await host.Call("-gr_movement", "onCreated");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["ok"], run.Output);
    }

    [Fact]
    public async Task ServerScriptRunsSingleLineFunction()
    {
        var host = new Gs2ServerScriptHost();
        var compile = new Gs2CompilerAdapter().Compile(
            Gs2ServerScriptHost.NormalizeServerSource("function onCreated() echo(1);"),
            "weapon",
            "-gr_movement");
        Assert.True(compile.Success, compile.Error);

        var load = host.LoadWeapon("-gr_movement", compile.Bytecode);
        Assert.True(load.Success, load.Error);

        var run = await host.Call("-gr_movement", "onCreated");

        Assert.True(run.Success, run.Error);
        Assert.Equal(["1"], run.Output);
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
        Assert.Contains(apis, api => api.Name == "echo" && api.IsImplemented);
        Assert.Contains(apis, api => api.Name == "triggerclient" && api.IsImplemented);
        Assert.Contains(apis, api => api.Name == "sendtonc" && api.IsImplemented);
        Assert.Contains(apis, api => api.Name == "base64encode" && api.IsImplemented);
        Assert.Contains(apis, api => api.Name == "base64decode" && api.IsImplemented);
        Assert.Contains(apis, api => api.Name == "serverr" && api.IsImplemented);
        Assert.All(apis.Where(api => api.SourceFile == "GS2Engine" && api.Blocker.Length != 0), api => Assert.False(api.IsImplemented));
    }
}
