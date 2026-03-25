using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordDonatorPrivilegePlannerTests
{
    private readonly DiscordDonatorPrivilegePlanner planner = new(new DonatorPrivilegeCatalog());

    [Fact]
    public void Plan_WhenDiscordHasNoMatchingRoles_DoesNotGrantAnything()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(
            new[] { "villager" },
            _ => false);

        Assert.Empty(result.PrivilegesToGrant);
        Assert.Empty(result.PrivilegesToRevoke);
    }

    [Fact]
    public void Plan_WhenDiscordHasOneMatchingRole_GrantsMatchingPrivilege()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(
            new[] { "supporter" },
            _ => false);

        Assert.Contains("firststepstweaks.supporter", result.PrivilegesToGrant);
        Assert.Empty(result.PrivilegesToRevoke);
    }

    [Fact]
    public void Plan_WhenDiscordHasMultipleMatchingRoles_GrantsOnlyHighestMatchingPrivilege()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(
            new[] { "supporter", "founder" },
            _ => false);

        Assert.Contains("firststepstweaks.founder", result.PrivilegesToGrant);
        Assert.DoesNotContain("firststepstweaks.supporter", result.PrivilegesToGrant);
    }

    [Fact]
    public void Plan_WhenPlayerHasStalePrivilege_RevokesPrivilege()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(
            new[] { "supporter" },
            privilege => privilege == "firststepstweaks.patron");

        Assert.Contains("firststepstweaks.patron", result.PrivilegesToRevoke);
        Assert.Contains("firststepstweaks.supporter", result.PrivilegesToGrant);
    }

    [Fact]
    public void Plan_TreatsDiscordRoleNamesAsCaseInsensitive()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(
            new[] { "FoUnDeR" },
            _ => false);

        Assert.Contains("firststepstweaks.founder", result.PrivilegesToGrant);
    }
}
