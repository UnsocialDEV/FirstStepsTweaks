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

        int result = resolver.Resolve(roleCode: null, config);

        Assert.Equal(2, result);
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

        int result = resolver.Resolve("contributor", config);

        Assert.Equal(7, result);
    }

    [Fact]
    public void Resolve_UsesFounderLimit_WhenFounderRoleAssigned()
    {
        var config = new TeleportConfig
        {
            HomeLimits = new HomeLimitConfig
            {
                Default = 1,
                Founder = 6
            }
        };

        int result = resolver.Resolve("founder", config);

        Assert.Equal(6, result);
    }
}
