using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class DonatorTierResolverTests
{
    private readonly DonatorTierResolver resolver = new();

    [Fact]
    public void ResolveLabel_ReturnsNull_WhenNoRoleMatches()
    {
        string? result = resolver.ResolveLabel("villager");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("supporter", "Supporter")]
    [InlineData("contributor", "Contributor")]
    [InlineData("sponsor", "Sponsor")]
    [InlineData("patron", "Patron")]
    [InlineData("founder", "Founder")]
    public void ResolveLabel_ReturnsExpectedTier_ForSingleRole(string roleCode, string expected)
    {
        string? result = resolver.ResolveLabel(roleCode);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveTier_ReturnsExpectedTierForRoleCode()
    {
        DonatorTier? result = resolver.ResolveTier("founder");

        Assert.Equal(DonatorTier.Founder, result);
    }
}
