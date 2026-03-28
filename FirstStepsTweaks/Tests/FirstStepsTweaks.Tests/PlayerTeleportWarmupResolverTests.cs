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

        int result = resolver.Resolve(_ => false, config);

        Assert.Equal(10, result);
    }

    [Theory]
    [InlineData("firststepstweaks.supporter")]
    [InlineData("firststepstweaks.contributor")]
    [InlineData("firststepstweaks.sponsor")]
    [InlineData("firststepstweaks.patron")]
    [InlineData("firststepstweaks.founder")]
    public void Resolve_UsesDonatorWarmup_ForAnyDonatorTier(string privilege)
    {
        var config = new TeleportConfig
        {
            WarmupSeconds = 10,
            DonatorWarmupSeconds = 3
        };

        int result = resolver.Resolve(current => current == privilege, config);

        Assert.Equal(3, result);
    }

    [Fact]
    public void Resolve_UsesDonatorWarmup_WhenMultiplePrivilegesMatch()
    {
        var config = new TeleportConfig
        {
            WarmupSeconds = 10,
            DonatorWarmupSeconds = 3
        };

        int result = resolver.Resolve(privilege =>
            privilege == "firststepstweaks.supporter"
            || privilege == "firststepstweaks.sponsor"
            || privilege == "firststepstweaks.founder", config);

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

        int result = resolver.Resolve(privilege => privilege == "firststepstweaks.supporter", config);

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
