using Preagonal.GServer.Persistence;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class ServerListStartupOptionsTests
{
    [Fact]
    public void BuildsRegistrationOptions()
    {
        var snapshot = new ServerStartupSnapshot(
            new ServerStartupResolution(
                Success: true,
                ServerName: "Classic",
                ServerPath: @"C:\servers\Classic\",
                Source: ServerStartupSource.CommandLineOrEnvironment,
                Diagnostic: null),
            Gs2Settings.Parse("""
                listip=custom-list.example.test
                listport=14901
                description=Test server
                serverip=203.0.113.9
                allowedversions=G3D0311C
                allowedversions=G3D0321C
                """),
            Gs2Settings.Parse("""
                hq_password=secret
                hq_level=3
                """),
            ["G3D0311C", "G3D0321C"]);

        var options = ServerListStartupOptions.FromStartupSnapshot(
            snapshot,
            new ServerStartupOverrides(
                Server: "Classic",
                Port: "14901",
                LocalIp: "AUTO",
                ServerIp: null,
                ServerInterface: null,
                StaffAccount: null,
                ServerName: "Public Classic",
                ShowHelp: false));

        Assert.Equal("custom-list.example.test", options.ListIp);
        Assert.Equal("14901", options.ListPort);
        Assert.Equal("Public Classic", options.Name);
        Assert.Equal("Test server", options.Description);
        Assert.Equal("English", options.Language);
        Assert.Equal("3.0.9-beta", options.Version);
        Assert.Equal("http://www.graal.in/", options.Url);
        Assert.Equal("203.0.113.9", options.ServerIp);
        Assert.Equal("14901", options.ServerPort);
        Assert.Equal("AUTO", options.LocalIp);
        Assert.Equal("secret", options.HqPassword);
        Assert.Equal(3, options.HqLevel);
        Assert.False(options.OnlyStaff);
        Assert.Equal(["G3D0311C", "G3D0321C"], options.AllowedVersions);
    }

    [Fact]
    public void UsesServerOptionDefaults()
    {
        var snapshot = new ServerStartupSnapshot(
            new ServerStartupResolution(
                Success: true,
                ServerName: "Classic",
                ServerPath: @"C:\servers\Classic\",
                Source: ServerStartupSource.CommandLineOrEnvironment,
                Diagnostic: null),
            Gs2Settings.Parse(""),
            Gs2Settings.Parse(""),
            ["GNW03014"]);

        var options = ServerListStartupOptions.FromStartupSnapshot(
            snapshot,
            new ServerStartupOverrides(
                Server: "Classic",
                Port: null,
                LocalIp: null,
                ServerIp: null,
                ServerInterface: null,
                StaffAccount: null,
                ServerName: null,
                ShowHelp: false));

        Assert.Equal("listserver.graal.in", options.ListIp);
        Assert.Equal("14900", options.ListPort);
        Assert.Equal("14802", options.ServerPort);
        Assert.Equal("AUTO", options.ServerIp);
        Assert.Equal("AUTO", options.LocalIp);
        Assert.Equal(["GNW03014"], options.AllowedVersions);
    }

    [Fact]
    public void UsesPublicName()
    {
        var snapshot = new ServerStartupSnapshot(
            new ServerStartupResolution(true, "default", @"C:\servers\default\", ServerStartupSource.CommandLineOrEnvironment, null),
            Gs2Settings.Parse("name=My Server"),
            Gs2Settings.Parse(""));

        var options = ServerListStartupOptions.FromStartupSnapshot(
            snapshot,
            new ServerStartupOverrides("default", null, null, null, null, null, null, false));

        Assert.Equal("My Server", options.Name);
    }
}
