using GServ.Core;
using Xunit;

namespace GServ.Core.Tests;

public sealed class SourceReferenceTests
{
    [Fact]
    public void ConfirmedGs2libCommitIsRecorded()
    {
        Assert.Equal("63b1ae96491c188905b50c6b61c8532c601a2122", SourceReferences.Gs2LibCommit);
    }
}
