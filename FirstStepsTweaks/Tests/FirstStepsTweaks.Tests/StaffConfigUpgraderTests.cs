using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class StaffConfigUpgraderTests
{
    [Fact]
    public void TryUpgradeLegacyAdminPlayerNames_MigratesDistinctNames()
    {
        var store = new StaffAssignmentStore(StaffTestCoreServerApiFactory.Create());
        var upgrader = new StaffConfigUpgrader(store);

        bool changed = upgrader.TryUpgradeLegacyAdminPlayerNames(["Alice", "alice", "Bob"]);

        StaffRoster roster = store.LoadRoster();
        Assert.True(changed);
        Assert.Equal(2, roster.LegacyAssignments.Count);
        Assert.All(roster.LegacyAssignments, assignment => Assert.Equal(StaffLevel.Admin, assignment.Level));
    }

    [Fact]
    public void TryUpgradeLegacyAdminPlayerNames_IgnoresEmptyValues()
    {
        var store = new StaffAssignmentStore(StaffTestCoreServerApiFactory.Create());
        var upgrader = new StaffConfigUpgrader(store);

        bool changed = upgrader.TryUpgradeLegacyAdminPlayerNames([" ", null!, "Admin"]);

        Assert.True(changed);
        Assert.Single(store.LoadRoster().LegacyAssignments);
    }
}
