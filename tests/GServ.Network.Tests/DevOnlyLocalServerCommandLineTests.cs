using GServ.Network;
using Xunit;

namespace GServ.Network.Tests;

public sealed class DevOnlyLocalServerCommandLineTests
{
    [Fact]
    public void ParseReturnsDisabledWhenDevOnlyFlagIsAbsent()
    {
        var config = DevOnlyLocalServerCommandLine.Parse(["--port", "14900"]);

        Assert.False(config.Enabled);
    }

    [Fact]
    public void ParseRequiresDevRootAndLevelWhenDevOnlyFlagIsPresent()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DevOnlyLocalServerCommandLine.Parse(["--dev-only-local", "--dev-root", "tmp"]));

        Assert.Equal("--dev-level is required for --dev-only-local.", ex.Message);
    }

    [Fact]
    public void ParseReadsExplicitDevOnlyOptions()
    {
        var config = DevOnlyLocalServerCommandLine.Parse(
            ["--dev-only-local", "--dev-root", "tmp", "--dev-level", "start.nw", "--port", "14900"]);

        Assert.True(config.Enabled);
        Assert.Equal("tmp", config.RootPath);
        Assert.Equal("start.nw", config.LevelName);
        Assert.Equal(14900, config.Port);
    }
}
