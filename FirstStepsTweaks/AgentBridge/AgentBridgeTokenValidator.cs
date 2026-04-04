using System;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeTokenValidator
{
    private readonly string sharedToken;

    public AgentBridgeTokenValidator(string sharedToken)
    {
        this.sharedToken = sharedToken;
    }

    public bool IsAuthorized(string? token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && string.Equals(token, sharedToken, StringComparison.Ordinal);
    }
}
