using System;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeCommandRequestValidator
{
    public bool TryValidate(AgentBridgeRequest? request, out string message)
    {
        if (request == null)
        {
            message = "Request body was empty or invalid JSON.";
            return false;
        }

        if (!string.Equals(request.Type, "command", StringComparison.OrdinalIgnoreCase))
        {
            message = "Only 'command' requests are supported in the current phase.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            message = "Command requests must include a non-empty command.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
