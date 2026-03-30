using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordDonatorRolePlannerTests
{
    private readonly DiscordDonatorRolePlanner planner = new(new DonatorPrivilegeCatalog());

    [Fact]
    public void Plan_WhenDiscordHasNoMatchingRoles_ReturnsNoDonatorPrivilege()
    {
        DiscordDonatorRolePlan result = planner.Plan(new[] { "villager" });

        Assert.Null(result.TargetPrivilege);
    }

    [Fact]
    public void Plan_WhenDiscordHasOneMatchingRole_ReturnsMatchingPrivilege()
    {
        DiscordDonatorRolePlan result = planner.Plan(new[] { "supporter" });

        Assert.Equal("firststepstweaks.supporter", result.TargetPrivilege);
    }

    [Fact]
    public void Plan_WhenDiscordHasMultipleMatchingRoles_ReturnsHighestMatchingPrivilege()
    {
        DiscordDonatorRolePlan result = planner.Plan(new[] { "supporter", "founder" });

        Assert.Equal("firststepstweaks.founder", result.TargetPrivilege);
    }

    [Fact]
    public void Plan_TreatsDiscordRoleNamesAsCaseInsensitive()
    {
        DiscordDonatorRolePlan result = planner.Plan(new[] { "FoUnDeR" });

        Assert.Equal("firststepstweaks.founder", result.TargetPrivilege);
    }
}
