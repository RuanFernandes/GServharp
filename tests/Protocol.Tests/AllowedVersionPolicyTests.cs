using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class AllowedVersionPolicyTests
{
    [Fact]
    public void EmptyAllowedVersionListRejectsClientLikeCppLoopDefault()
    {
        Assert.False(AllowedVersionPolicy.IsAllowed(ClientVersionId.Client6037, []));
    }

    [Fact]
    public void ExactAllowedVersionAcceptsMatchingClient()
    {
        Assert.True(AllowedVersionPolicy.IsAllowed(ClientVersionId.Client6037, ["G3D0311C"]));
    }

    [Fact]
    public void RangeAllowedVersionAcceptsClientInsideInclusiveBounds()
    {
        Assert.True(AllowedVersionPolicy.IsAllowed(ClientVersionId.Client222, ["GNW01113:GNW28015"]));
    }

    [Fact]
    public void RangeAllowedVersionRejectsClientOutsideBounds()
    {
        Assert.False(AllowedVersionPolicy.IsAllowed(ClientVersionId.Client6037, ["GNW01113:GNW28015"]));
    }
}
