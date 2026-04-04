namespace FirstStepsTweaks.Features;

internal sealed class ParkedAgentBridgeAvailabilityPolicy : IAgentBridgeAvailabilityPolicy
{
    private const string UnavailableReason =
        "Agent bridge startup is intentionally disabled and preserved for future reuse. Re-enable work should start in the bridge availability policy.";

    public bool IsAvailable(out string unavailableReason)
    {
        unavailableReason = UnavailableReason;
        return false;
    }
}
