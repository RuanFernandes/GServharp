using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class AllowedVersionPolicyTests
{
    [Fact]
    public void EmptyListRejects()
    {
        Assert.False(AllowedVersionPolicy.IsAllowed(ClientVersionId.Client6037, []));
    }

    [Fact]
    public void ExactTokenAllowsMatch()
    {
        Assert.True(AllowedVersionPolicy.IsAllowed(ClientVersionId.Client6037, ["G3D0311C"]));
    }

    [Fact]
    public void RangeAllowsInside()
    {
        Assert.True(AllowedVersionPolicy.IsAllowed(ClientVersionId.Client222, ["GNW01113:GNW28015"]));
    }

    [Fact]
    public void RangeRejectsOutside()
    {
        Assert.False(AllowedVersionPolicy.IsAllowed(ClientVersionId.Client6037, ["GNW01113:GNW28015"]));
    }
}
