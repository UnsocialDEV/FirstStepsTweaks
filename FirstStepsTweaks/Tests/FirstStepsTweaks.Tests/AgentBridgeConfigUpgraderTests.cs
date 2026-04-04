using FirstStepsTweaks.Config;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class AgentBridgeConfigUpgraderTests
{
    [Fact]
    public void TryUpgradeLegacyLoopbackPort_UpgradesLegacyLoopbackDefault()
    {
        var config = new FirstStepsTweaksConfig
        {
            AgentBridge = new AgentBridgeConfig
            {
                Host = AgentBridgeConfig.DefaultHost,
                Port = AgentBridgeConfig.LegacyDefaultPort,
                SharedToken = "token"
            }
        };
        var upgrader = new AgentBridgeConfigUpgrader();

        bool changed = upgrader.TryUpgradeLegacyLoopbackPort(config);

        Assert.True(changed);
        Assert.Equal(AgentBridgeConfig.DefaultPort, config.AgentBridge.Port);
    }

    [Fact]
    public void TryUpgradeLegacyLoopbackPort_UpgradesLegacyLocalhostAlias()
    {
        var config = new FirstStepsTweaksConfig
        {
            AgentBridge = new AgentBridgeConfig
            {
                Host = "localhost",
                Port = AgentBridgeConfig.LegacyDefaultPort
            }
        };
        var upgrader = new AgentBridgeConfigUpgrader();

        bool changed = upgrader.TryUpgradeLegacyLoopbackPort(config);

        Assert.True(changed);
        Assert.Equal(AgentBridgeConfig.DefaultPort, config.AgentBridge.Port);
    }

    [Fact]
    public void TryUpgradeLegacyLoopbackPort_PreservesCustomPort()
    {
        var config = new FirstStepsTweaksConfig
        {
            AgentBridge = new AgentBridgeConfig
            {
                Host = AgentBridgeConfig.DefaultHost,
                Port = 30001
            }
        };
        var upgrader = new AgentBridgeConfigUpgrader();

        bool changed = upgrader.TryUpgradeLegacyLoopbackPort(config);

        Assert.False(changed);
        Assert.Equal(30001, config.AgentBridge.Port);
    }

    [Fact]
    public void TryUpgradeLegacyLoopbackPort_PreservesNonLoopbackBinding()
    {
        var config = new FirstStepsTweaksConfig
        {
            AgentBridge = new AgentBridgeConfig
            {
                Host = "0.0.0.0",
                Port = AgentBridgeConfig.LegacyDefaultPort
            }
        };
        var upgrader = new AgentBridgeConfigUpgrader();

        bool changed = upgrader.TryUpgradeLegacyLoopbackPort(config);

        Assert.False(changed);
        Assert.Equal(AgentBridgeConfig.LegacyDefaultPort, config.AgentBridge.Port);
    }
}
