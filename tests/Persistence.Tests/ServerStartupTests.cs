using Preagonal.GServer.Persistence;
using Xunit;

namespace Preagonal.GServer.Persistence.Tests;

public sealed class ServerStartupTests
{
    [Fact]
    public void ParseCommandLineMatchesCppOverrideOrderingRules()
    {
        var parsed = ServerStartupCommandLine.Parse(
            ["GServer", "--port", "14900", "--server", "classic", "--localip", "127.0.0.1", "-p", "15000"],
            _ => null);

        Assert.False(parsed.ShowHelp);
        Assert.Equal("classic", parsed.Server);
        Assert.Equal("15000", parsed.Port);
        Assert.Equal("127.0.0.1", parsed.LocalIp);
    }

    [Fact]
    public void ParseEnvironmentOnlyWhenUseEnvExistsAndServerGatesOtherOverrides()
    {
        var env = new Dictionary<string, string?>
        {
            ["USE_ENV"] = "1",
            ["PORT"] = "14900",
            ["SERVERIP"] = "1.2.3.4",
        };

        var parsed = ServerStartupCommandLine.Parse(["GServer", "--server", "ignored"], env.GetValueOrDefault);

        Assert.Null(parsed.Server);
        Assert.Null(parsed.Port);
        Assert.Null(parsed.ServerIp);
    }

    [Fact]
    public void ResolveUsesStartupServerBeforeDirectorySearch()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "startupserver.txt"), "from-file");
        Directory.CreateDirectory(Path.Combine(root, "servers", "only-dir"));

        var result = ServerStartupResolver.Resolve(root, ServerStartupOverrides.Empty);

        Assert.True(result.Success);
        Assert.Equal("from-file", result.ServerName);
        Assert.Equal(ServerStartupSource.StartupServerFile, result.Source);
    }

    [Fact]
    public void ResolveUsesOnlyServerDirectoryWhenStartupFileIsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "servers", "classic"));

        var result = ServerStartupResolver.Resolve(root, ServerStartupOverrides.Empty);

        Assert.True(result.Success);
        Assert.Equal("classic", result.ServerName);
        Assert.Equal(ServerStartupSource.SingleServersDirectory, result.Source);
        Assert.EndsWith($"{Path.DirectorySeparatorChar}servers{Path.DirectorySeparatorChar}classic{Path.DirectorySeparatorChar}", result.ServerPath);
    }

    [Fact]
    public void ResolveFailsWhenNoOverrideStartupFileOrSingleDirectoryExists()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "servers", "one"));
        Directory.CreateDirectory(Path.Combine(root, "servers", "two"));

        var result = ServerStartupResolver.Resolve(root, ServerStartupOverrides.Empty);

        Assert.False(result.Success);
        Assert.Equal(ServerStartupSource.None, result.Source);
    }

    [Fact]
    public void DefaultAllowedVersionsMatchCpp()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var snapshot = ServerStartupLoader.Load(
            repoRoot,
            ServerStartupOverrides.Empty with { Server = "default" });

        Assert.Equal(36, snapshot.EffectiveAllowedVersions.Count);
        Assert.Contains("G3D04048", snapshot.EffectiveAllowedVersions);
        Assert.Contains("G3D18010", snapshot.EffectiveAllowedVersions);
        Assert.Contains("G3D29090", snapshot.EffectiveAllowedVersions);
        Assert.Contains("G3D2504D", snapshot.EffectiveAllowedVersions);
    }
}
