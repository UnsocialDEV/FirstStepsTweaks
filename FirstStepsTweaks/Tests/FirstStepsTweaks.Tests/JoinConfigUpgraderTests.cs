using FirstStepsTweaks.Config;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class JoinConfigUpgraderTests
{
    [Fact]
    public void TryUpgradeReturningJoinMessage_UpgradesLegacyDefault()
    {
        var config = new FirstStepsTweaksConfig
        {
            Join = new JoinConfig
            {
                ReturningJoinMessage = JoinConfig.LegacyReturningJoinMessage
            }
        };
        var upgrader = new JoinConfigUpgrader();

        bool changed = upgrader.TryUpgradeReturningJoinMessage(config);

        Assert.True(changed);
        Assert.Equal(JoinConfig.DefaultReturningJoinMessage, config.Join.ReturningJoinMessage);
    }

    [Fact]
    public void TryUpgradeReturningJoinMessage_PreservesCustomMessage()
    {
        const string customMessage = "Welcome back {player}, it has been {days} days.";

        var config = new FirstStepsTweaksConfig
        {
            Join = new JoinConfig
            {
                ReturningJoinMessage = customMessage
            }
        };
        var upgrader = new JoinConfigUpgrader();

        bool changed = upgrader.TryUpgradeReturningJoinMessage(config);

        Assert.False(changed);
        Assert.Equal(customMessage, config.Join.ReturningJoinMessage);
    }

    [Fact]
    public void TryUpgradeReturningJoinMessage_LeavesUpgradedMessageUnchanged()
    {
        var config = new FirstStepsTweaksConfig
        {
            Join = new JoinConfig
            {
                ReturningJoinMessage = JoinConfig.DefaultReturningJoinMessage
            }
        };
        var upgrader = new JoinConfigUpgrader();

        bool changed = upgrader.TryUpgradeReturningJoinMessage(config);

        Assert.False(changed);
        Assert.Equal(JoinConfig.DefaultReturningJoinMessage, config.Join.ReturningJoinMessage);
    }
}
