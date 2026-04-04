using FirstStepsTweaks.Config;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeStartupValidator
{
    public bool TryValidate(AgentBridgeConfig config, out string message)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            message = "AgentBridge.Host must be configured.";
            return false;
        }

        if (config.Port <= 0 || config.Port > 65535)
        {
            message = "AgentBridge.Port must be between 1 and 65535.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.SharedToken))
        {
            message = "AgentBridge.SharedToken must be configured before the listener can start.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
