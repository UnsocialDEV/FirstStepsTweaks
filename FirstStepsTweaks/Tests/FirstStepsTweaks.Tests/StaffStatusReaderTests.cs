using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class StaffStatusReaderTests
{
    [Fact]
    public void GetLevel_ResolvesUidBackedAssignment()
    {
        var store = new StaffAssignmentStore(StaffTestCoreServerApiFactory.Create());
        store.SaveRoster(new StaffRoster
        {
            Assignments =
            [
                new StaffAssignment { PlayerUid = "uid-1", LastKnownPlayerName = "Admin", Level = StaffLevel.Admin }
            ]
        });
        var reader = new StaffStatusReader(store);

        Assert.Equal(StaffLevel.Admin, reader.GetLevel("uid-1", "Admin"));
    }

    [Fact]
    public void GetLevel_ResolvesLegacyNameWhenUidAssignmentMissing()
    {
        var store = new StaffAssignmentStore(StaffTestCoreServerApiFactory.Create());
        store.SaveRoster(new StaffRoster
        {
            LegacyAssignments =
            [
                new LegacyStaffAssignment { PlayerName = "LegacyAdmin", Level = StaffLevel.Admin }
            ]
        });
        var reader = new StaffStatusReader(store);

        Assert.Equal(StaffLevel.Admin, reader.GetLevel("unknown", "LegacyAdmin"));
    }

    [Fact]
    public void StaffJoinSyncService_ConvertsLegacyAssignmentsOnJoin()
    {
        IServerPlayer player = StaffTestServerPlayerFactory.Create("uid-1", "LegacyAdmin");
        var store = new StaffAssignmentStore(StaffTestCoreServerApiFactory.Create());
        store.SaveRoster(new StaffRoster
        {
            LegacyAssignments =
            [
                new LegacyStaffAssignment { PlayerName = "LegacyAdmin", Level = StaffLevel.Admin }
            ]
        });

        var syncService = new StaffPrivilegeSyncService(store, new PlayerPrivilegeReader(), new RecordingPrivilegeMutator(), new StaffPrivilegeCatalog());
        var joinSyncService = new StaffJoinSyncService(store, syncService);

        joinSyncService.OnPlayerNowPlaying(player);

        StaffRoster roster = store.LoadRoster();
        Assert.Empty(roster.LegacyAssignments);
        Assert.Single(roster.Assignments);
        Assert.Equal("uid-1", roster.Assignments[0].PlayerUid);
        Assert.Equal(StaffLevel.Admin, roster.Assignments[0].Level);
    }
}
