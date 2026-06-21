using Preagonal.GServer.Network;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class LocalDebugCommandLineTests
{
    [Fact]
    public void ParseReturnsDisabledWhenDevOnlyFlagIsAbsent()
    {
        var config = LocalDebugCommandLine.Parse(["--port", "14900"]);

        Assert.False(config.Enabled);
    }

    [Fact]
    public void ParseRequiresDevRootAndLevelWhenDevOnlyFlagIsPresent()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            LocalDebugCommandLine.Parse(["--local-debug", "--dev-root", "tmp"]));

        Assert.Equal("--dev-level is required for --local-debug.", ex.Message);
    }

    [Fact]
    public void ParseReadsExplicitDevOnlyOptions()
    {
        var config = LocalDebugCommandLine.Parse(
            ["--local-debug", "--dev-root", "tmp", "--dev-level", "start.nw", "--port", "14900"]);

        Assert.True(config.Enabled);
        Assert.Equal("tmp", config.RootPath);
        Assert.Equal("start.nw", config.LevelName);
        Assert.Equal(14900, config.Port);
    }
}
