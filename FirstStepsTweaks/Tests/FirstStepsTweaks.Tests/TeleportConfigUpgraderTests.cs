using FirstStepsTweaks.Config;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class TeleportConfigUpgraderTests
{
    [Fact]
    public void TryUpgradeDonatorWarmupSeconds_InitializesMissingValue()
    {
        var config = new FirstStepsTweaksConfig
        {
            Teleport = new TeleportConfig
            {
                WarmupSeconds = 10,
                DonatorWarmupSeconds = null
            }
        };
        var upgrader = new TeleportConfigUpgrader();

        bool changed = upgrader.TryUpgradeDonatorWarmupSeconds(config);

        Assert.True(changed);
        Assert.Equal(TeleportConfig.DefaultDonatorWarmupSeconds, config.Teleport.DonatorWarmupSeconds);
    }

    [Fact]
    public void TryUpgradeDonatorWarmupSeconds_PreservesExistingValue()
    {
        var config = new FirstStepsTweaksConfig
        {
            Teleport = new TeleportConfig
            {
                WarmupSeconds = 10,
                DonatorWarmupSeconds = 5
            }
        };
        var upgrader = new TeleportConfigUpgrader();

        bool changed = upgrader.TryUpgradeDonatorWarmupSeconds(config);

        Assert.False(changed);
        Assert.Equal(5, config.Teleport.DonatorWarmupSeconds);
    }
}
