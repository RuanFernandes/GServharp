using GServ.Network;
using Xunit;

namespace GServ.Network.Tests;

public sealed class ClientCertificationHarnessTests
{
    [Fact]
    public void MatchingCaptureStepsAreCertified()
    {
        var comparison = ClientCertificationHarness.Compare(
            new ClientCaptureStep("login-reject", new byte[] { 0x30, 0x41, 0x0A }),
            new ClientCaptureStep("login-reject", new byte[] { 0x30, 0x41, 0x0A }));

        Assert.True(comparison.Certified);
        Assert.Equal(ClientCaptureMismatchKind.None, comparison.MismatchKind);
        Assert.Null(comparison.FirstMismatchOffset);
    }

    [Fact]
    public void FirstByteMismatchFailsCertificationWithExactOffset()
    {
        var comparison = ClientCertificationHarness.Compare(
            new ClientCaptureStep("movement", new byte[] { 0x08, 0x20, 0x21 }),
            new ClientCaptureStep("movement", new byte[] { 0x08, 0x20, 0x22 }));

        Assert.False(comparison.Certified);
        Assert.Equal(ClientCaptureMismatchKind.ByteMismatch, comparison.MismatchKind);
        Assert.Equal(2, comparison.FirstMismatchOffset);
        Assert.Equal(0x21, comparison.ExpectedByte);
        Assert.Equal(0x22, comparison.ActualByte);
    }

    [Fact]
    public void LengthMismatchFailsCertificationAtSharedLengthBoundary()
    {
        var comparison = ClientCertificationHarness.Compare(
            new ClientCaptureStep("file-transfer", new byte[] { 0x66, 0x41, 0x42 }),
            new ClientCaptureStep("file-transfer", new byte[] { 0x66, 0x41 }));

        Assert.False(comparison.Certified);
        Assert.Equal(ClientCaptureMismatchKind.LengthMismatch, comparison.MismatchKind);
        Assert.Equal(2, comparison.FirstMismatchOffset);
        Assert.Equal(3, comparison.ExpectedLength);
        Assert.Equal(2, comparison.ActualLength);
    }

    [Fact]
    public void FlowComparisonPreservesStepOrderAndLabels()
    {
        var result = ClientCertificationHarness.CompareFlow(
            new ClientCaptureFlow(
                "cpp-login",
                new[]
                {
                    new ClientCaptureStep("signature", new byte[] { 0x39 }),
                    new ClientCaptureStep("unknown168", new byte[] { 0x68 })
                }),
            new ClientCaptureFlow(
                "csharp-login",
                new[]
                {
                    new ClientCaptureStep("signature", new byte[] { 0x39 }),
                    new ClientCaptureStep("unknown168", new byte[] { 0x69 })
                }));

        Assert.False(result.Certified);
        Assert.Equal(2, result.StepResults.Count);
        Assert.True(result.StepResults[0].Certified);
        Assert.False(result.StepResults[1].Certified);
        Assert.Equal("unknown168", result.StepResults[1].Expected.Label);
    }

    [Fact]
    public void MissingFlowStepFailsCertification()
    {
        var result = ClientCertificationHarness.CompareFlow(
            new ClientCaptureFlow(
                "cpp",
                new[] { new ClientCaptureStep("shutdown", new byte[] { 0x30 }) }),
            new ClientCaptureFlow("csharp", Array.Empty<ClientCaptureStep>()));

        Assert.False(result.Certified);
        Assert.Single(result.StepResults);
        Assert.Equal(ClientCaptureMismatchKind.MissingStep, result.StepResults[0].MismatchKind);
    }
}
