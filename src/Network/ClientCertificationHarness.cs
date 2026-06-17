namespace GServ.Network;

public sealed record ClientCaptureStep(string Label, byte[] Bytes);

public sealed record ClientCaptureFlow(string Label, IReadOnlyList<ClientCaptureStep> Steps);

public enum ClientCaptureMismatchKind
{
    None,
    ByteMismatch,
    LengthMismatch,
    LabelMismatch,
    MissingStep
}

public sealed record ClientCaptureStepComparison(
    ClientCaptureStep Expected,
    ClientCaptureStep? Actual,
    ClientCaptureMismatchKind MismatchKind,
    int? FirstMismatchOffset,
    int? ExpectedByte,
    int? ActualByte,
    int ExpectedLength,
    int ActualLength)
{
    public bool Certified => MismatchKind == ClientCaptureMismatchKind.None;
}

public sealed record ClientCaptureFlowComparison(
    ClientCaptureFlow Expected,
    ClientCaptureFlow Actual,
    IReadOnlyList<ClientCaptureStepComparison> StepResults)
{
    public bool Certified => StepResults.All(step => step.Certified);
}

public static class ClientCertificationHarness
{
    public static ClientCaptureStepComparison Compare(ClientCaptureStep expected, ClientCaptureStep actual)
    {
        if (!string.Equals(expected.Label, actual.Label, StringComparison.Ordinal))
        {
            return new ClientCaptureStepComparison(
                expected,
                actual,
                ClientCaptureMismatchKind.LabelMismatch,
                null,
                null,
                null,
                expected.Bytes.Length,
                actual.Bytes.Length);
        }

        var sharedLength = Math.Min(expected.Bytes.Length, actual.Bytes.Length);
        for (var i = 0; i < sharedLength; i++)
        {
            if (expected.Bytes[i] == actual.Bytes[i])
                continue;

            return new ClientCaptureStepComparison(
                expected,
                actual,
                ClientCaptureMismatchKind.ByteMismatch,
                i,
                expected.Bytes[i],
                actual.Bytes[i],
                expected.Bytes.Length,
                actual.Bytes.Length);
        }

        if (expected.Bytes.Length != actual.Bytes.Length)
        {
            return new ClientCaptureStepComparison(
                expected,
                actual,
                ClientCaptureMismatchKind.LengthMismatch,
                sharedLength,
                null,
                null,
                expected.Bytes.Length,
                actual.Bytes.Length);
        }

        return new ClientCaptureStepComparison(
            expected,
            actual,
            ClientCaptureMismatchKind.None,
            null,
            null,
            null,
            expected.Bytes.Length,
            actual.Bytes.Length);
    }

    public static ClientCaptureFlowComparison CompareFlow(ClientCaptureFlow expected, ClientCaptureFlow actual)
    {
        var results = new List<ClientCaptureStepComparison>();
        var max = Math.Max(expected.Steps.Count, actual.Steps.Count);

        for (var i = 0; i < max; i++)
        {
            if (i >= expected.Steps.Count)
            {
                var actualOnly = actual.Steps[i];
                results.Add(new ClientCaptureStepComparison(
                    new ClientCaptureStep($"<missing expected step {i}>", Array.Empty<byte>()),
                    actualOnly,
                    ClientCaptureMismatchKind.MissingStep,
                    0,
                    null,
                    actualOnly.Bytes.Length > 0 ? actualOnly.Bytes[0] : null,
                    0,
                    actualOnly.Bytes.Length));
                continue;
            }

            if (i >= actual.Steps.Count)
            {
                var expectedOnly = expected.Steps[i];
                results.Add(new ClientCaptureStepComparison(
                    expectedOnly,
                    null,
                    ClientCaptureMismatchKind.MissingStep,
                    0,
                    expectedOnly.Bytes.Length > 0 ? expectedOnly.Bytes[0] : null,
                    null,
                    expectedOnly.Bytes.Length,
                    0));
                continue;
            }

            results.Add(Compare(expected.Steps[i], actual.Steps[i]));
        }

        return new ClientCaptureFlowComparison(expected, actual, results);
    }
}
