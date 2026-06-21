namespace Preagonal.GServer.Admin;

public sealed record ControlLoginDecision(bool Allowed, string? DisconnectMessage);

public static class ControlLoginGate
{
    public const string MissingRightsDisconnectMessage = "You do not have RC rights.";

    public static ControlLoginDecision Evaluate(bool isStaff, bool isAdminIp) =>
        isStaff && isAdminIp
            ? new ControlLoginDecision(true, null)
            : new ControlLoginDecision(false, MissingRightsDisconnectMessage);
}
