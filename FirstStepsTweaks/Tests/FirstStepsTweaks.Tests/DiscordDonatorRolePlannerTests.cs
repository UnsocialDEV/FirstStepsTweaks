using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordDonatorRolePlannerTests
{
    private readonly DiscordDonatorRolePlanner planner = new(new DonatorPrivilegeCatalog());

    [Fact]
    public void Plan_WhenDiscordHasNoMatchingRoles_ReturnsNoDonatorRole()
    {
        DiscordDonatorRolePlan result = planner.Plan(new[] { "villager" });

        Assert.Null(result.TargetRoleCode);
    }

    [Fact]
    public void Plan_WhenDiscordHasOneMatchingRole_ReturnsMatchingLowercaseRoleCode()
    {
        DiscordDonatorRolePlan result = planner.Plan(new[] { "supporter" });

        Assert.Equal("supporter", result.TargetRoleCode);
    }

    [Fact]
    public void Plan_WhenDiscordHasMultipleMatchingRoles_ReturnsHighestMatchingRoleCode()
    {
        DiscordDonatorRolePlan result = planner.Plan(new[] { "supporter", "founder" });

        Assert.Equal("founder", result.TargetRoleCode);
    }

    [Fact]
    public void Plan_TreatsDiscordRoleNamesAsCaseInsensitive()
    {
        DiscordDonatorRolePlan result = planner.Plan(new[] { "FoUnDeR" });

        Assert.Equal("founder", result.TargetRoleCode);
    }
}
