using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class PlayerHomeLimitResolverTests
{
    private readonly PlayerHomeLimitResolver resolver = new();

    [Fact]
    public void Resolve_UsesDefaultLimit_WhenNoPrivilegesMatch()
    {
        var config = new TeleportConfig
        {
            HomeLimits = new HomeLimitConfig
            {
                Default = 2,
                Supporter = 3
            }
        };

        int result = resolver.Resolve(_ => false, config);

        Assert.Equal(2, result);
    }

    [Fact]
    public void Resolve_UsesHighestTier_WhenMultiplePrivilegesMatch()
    {
        var config = new TeleportConfig
        {
            HomeLimits = new HomeLimitConfig
            {
                Default = 1,
                Supporter = 2,
                Contributor = 3,
                Sponsor = 4,
                Patron = 5,
                Founder = 6
            }
        };

        int result = resolver.Resolve(privilege =>
            privilege == "firststepstweaks.supporter"
            || privilege == "firststepstweaks.sponsor"
            || privilege == "firststepstweaks.founder", config);

        Assert.Equal(6, result);
    }

    [Fact]
    public void Resolve_UsesConfiguredTierSpecificLimit()
    {
        var config = new TeleportConfig
        {
            HomeLimits = new HomeLimitConfig
            {
                Default = 1,
                Supporter = 4,
                Contributor = 7
            }
        };

        int result = resolver.Resolve(privilege => privilege == "firststepstweaks.contributor", config);

        Assert.Equal(7, result);
    }
}
