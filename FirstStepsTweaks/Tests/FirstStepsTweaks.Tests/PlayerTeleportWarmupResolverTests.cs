using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class PlayerTeleportWarmupResolverTests
{
    private readonly PlayerTeleportWarmupResolver resolver = new();

    [Fact]
    public void Resolve_UsesDefaultWarmup_WhenNoPrivilegesMatch()
    {
        var config = new TeleportConfig
        {
            WarmupSeconds = 10,
            DonatorWarmupSeconds = 3
        };

        int result = resolver.Resolve(roleCode: null, config);

        Assert.Equal(10, result);
    }

    [Theory]
    [InlineData("supporter")]
    [InlineData("contributor")]
    [InlineData("sponsor")]
    [InlineData("patron")]
    [InlineData("founder")]
    public void Resolve_UsesDonatorWarmup_ForAnyDonatorTier(string roleCode)
    {
        var config = new TeleportConfig
        {
            WarmupSeconds = 10,
            DonatorWarmupSeconds = 3
        };

        int result = resolver.Resolve(roleCode, config);

        Assert.Equal(3, result);
    }

    [Fact]
    public void Resolve_UsesFallbackDonatorWarmup_WhenConfigValueIsMissing()
    {
        var config = new TeleportConfig
        {
            WarmupSeconds = 10,
            DonatorWarmupSeconds = null
        };

        int result = resolver.Resolve("supporter", config);

        Assert.Equal(TeleportConfig.DefaultDonatorWarmupSeconds, result);
    }

    [Fact]
    public void Resolve_UsesDefaultWarmup_WhenPlayerIsNull()
    {
        var config = new TeleportConfig
        {
            WarmupSeconds = 10,
            DonatorWarmupSeconds = 3
        };

        int result = resolver.Resolve(player: null, config);

        Assert.Equal(10, result);
    }
}
