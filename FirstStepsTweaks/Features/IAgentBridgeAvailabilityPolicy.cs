namespace FirstStepsTweaks.Features;

internal interface IAgentBridgeAvailabilityPolicy
{
    bool IsAvailable(out string unavailableReason);
}
