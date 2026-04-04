using System;

namespace FirstStepsTweaks.Config;

public sealed class AgentBridgeConfigUpgrader
{
    public bool TryUpgradeLegacyLoopbackPort(FirstStepsTweaksConfig config)
    {
        if (config == null)
        {
            return false;
        }

        if (config.AgentBridge == null)
        {
            config.AgentBridge = new AgentBridgeConfig();
            return true;
        }

        if (!UsesLegacyLoopbackPort(config.AgentBridge))
        {
            return false;
        }

        config.AgentBridge.Port = AgentBridgeConfig.DefaultPort;
        return true;
    }

    private static bool UsesLegacyLoopbackPort(AgentBridgeConfig config)
    {
        return config.Port == AgentBridgeConfig.LegacyDefaultPort
            && IsLoopbackHost(config.Host);
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        string normalizedHost = host.Trim();
        if (normalizedHost.Length > 1 && normalizedHost[0] == '[' && normalizedHost[^1] == ']')
        {
            normalizedHost = normalizedHost.Substring(1, normalizedHost.Length - 2);
        }

        return string.Equals(normalizedHost, AgentBridgeConfig.DefaultHost, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedHost, "::1", StringComparison.OrdinalIgnoreCase);
    }
}
