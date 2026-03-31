using System.Linq;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordDonatorPrivilegePlannerTests
{
    private readonly DiscordDonatorPrivilegePlanner planner = new(new DonatorPrivilegeCatalog());

    [Fact]
    public void Plan_WhenDiscordHasNoMatchingRoles_ReturnsNoDonatorPrivileges()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(new[] { "villager" });

        Assert.Empty(result.TargetPrivileges);
    }

    [Fact]
    public void Plan_WhenDiscordHasOneMatchingRole_ReturnsMatchingPrivilege()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(new[] { "supporter" });

        Assert.Equal(new[] { "firststepstweaks.supporter" }, result.TargetPrivileges);
    }

    [Fact]
    public void Plan_WhenDiscordHasMultipleMatchingRoles_ReturnsCumulativePrivilegesForHighestTier()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(new[] { "supporter", "founder" });

        Assert.Equal(
            new[]
            {
                "firststepstweaks.founder",
                "firststepstweaks.patron",
                "firststepstweaks.sponsor",
                "firststepstweaks.contributor",
                "firststepstweaks.supporter"
            },
            result.TargetPrivileges);
    }

    [Fact]
    public void Plan_TreatsDiscordRoleNamesAsCaseInsensitive()
    {
        DiscordDonatorPrivilegePlan result = planner.Plan(new[] { "FoUnDeR" });

        Assert.Equal("firststepstweaks.founder", result.TargetPrivileges.First());
    }
}
