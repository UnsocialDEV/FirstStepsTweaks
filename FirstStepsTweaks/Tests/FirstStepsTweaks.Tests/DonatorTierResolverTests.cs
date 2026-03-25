using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class DonatorTierResolverTests
{
    private readonly DonatorTierResolver resolver = new();

    [Fact]
    public void ResolveLabel_ReturnsNull_WhenNoPrivilegesMatch()
    {
        string? result = resolver.ResolveLabel(_ => false);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("firststepstweaks.supporter", "Supporter")]
    [InlineData("firststepstweaks.contributor", "Contributor")]
    [InlineData("firststepstweaks.sponsor", "Sponsor")]
    [InlineData("firststepstweaks.patron", "Patron")]
    [InlineData("firststepstweaks.founder", "Founder")]
    public void ResolveLabel_ReturnsExpectedTier_ForSinglePrivilege(string privilege, string expected)
    {
        string? result = resolver.ResolveLabel(current => current == privilege);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveLabel_ReturnsHighestTier_WhenMultiplePrivilegesMatch()
    {
        string? result = resolver.ResolveLabel(privilege =>
            privilege == "firststepstweaks.supporter"
            || privilege == "firststepstweaks.sponsor"
            || privilege == "firststepstweaks.founder");

        Assert.Equal("Founder", result);
    }
}
