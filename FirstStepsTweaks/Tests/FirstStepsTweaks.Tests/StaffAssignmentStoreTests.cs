using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class StaffAssignmentStoreTests
{
    [Fact]
    public void SaveRoster_PersistsUidAssignments()
    {
        var api = StaffTestCoreServerApiFactory.Create();
        var store = new StaffAssignmentStore(api);

        store.SaveRoster(new StaffRoster
        {
            Assignments =
            [
                new StaffAssignment { PlayerUid = "uid-1", LastKnownPlayerName = "Admin", Level = StaffLevel.Admin }
            ]
        });

        StaffRoster loaded = store.LoadRoster();

        Assert.Single(loaded.Assignments);
        Assert.Equal("uid-1", loaded.Assignments[0].PlayerUid);
        Assert.Equal("Admin", loaded.Assignments[0].LastKnownPlayerName);
        Assert.Equal(StaffLevel.Admin, loaded.Assignments[0].Level);
    }

    [Fact]
    public void SaveRoster_RemovesAssignmentsWithBlankUid()
    {
        var api = StaffTestCoreServerApiFactory.Create();
        var store = new StaffAssignmentStore(api);

        store.SaveRoster(new StaffRoster
        {
            Assignments =
            [
                new StaffAssignment { PlayerUid = " ", LastKnownPlayerName = "Ignored", Level = StaffLevel.Admin }
            ]
        });

        Assert.Empty(store.LoadRoster().Assignments);
    }

    [Fact]
    public void SaveRoster_PersistsLegacyAssignments()
    {
        var api = StaffTestCoreServerApiFactory.Create();
        var store = new StaffAssignmentStore(api);

        store.SaveRoster(new StaffRoster
        {
            LegacyAssignments =
            [
                new LegacyStaffAssignment { PlayerName = "LegacyAdmin", Level = StaffLevel.Admin }
            ]
        });

        StaffRoster loaded = store.LoadRoster();

        Assert.Single(loaded.LegacyAssignments);
        Assert.Equal("LegacyAdmin", loaded.LegacyAssignments[0].PlayerName);
    }

    [Fact]
    public void LoadRoster_LoadsLegacyObjectData_AndRewritesToJsonBytes()
    {
        var api = StaffTestCoreServerApiFactory.Create();
        var saveGame = (SaveGameProxy)(object)((WorldManagerProxy)(object)((CoreServerApiProxy)(object)api).WorldManager!).SaveGame!;
        var logger = (LoggerProxy)(object)((CoreServerApiProxy)(object)api).Logger!;
        var store = new StaffAssignmentStore(api);

        saveGame.StoreDataDirect("fst_staffroster", new StaffRoster
        {
            Assignments =
            [
                new StaffAssignment { PlayerUid = "uid-1", LastKnownPlayerName = "Admin", Level = StaffLevel.Admin }
            ]
        });

        StaffRoster loaded = store.LoadRoster();

        Assert.Single(loaded.Assignments);
        Assert.IsType<byte[]>(saveGame.GetStoredValue("fst_staffroster"));
        Assert.Contains(logger.Notifications, message => message.Contains("Migrated legacy staff roster data", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadRoster_ReturnsEmptyRoster_WhenStoredDataIsMalformed()
    {
        var api = StaffTestCoreServerApiFactory.Create();
        var saveGame = (SaveGameProxy)(object)((WorldManagerProxy)(object)((CoreServerApiProxy)(object)api).WorldManager!).SaveGame!;
        var logger = (LoggerProxy)(object)((CoreServerApiProxy)(object)api).Logger!;
        var store = new StaffAssignmentStore(api);

        saveGame.StoreDataDirect("fst_staffroster", new byte[] { 1, 2, 3, 4 });

        StaffRoster loaded = store.LoadRoster();

        Assert.Empty(loaded.Assignments);
        Assert.Empty(loaded.LegacyAssignments);
        Assert.Contains(logger.Warnings, message => message.Contains("fst_staffroster", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadRoster_NormalizesDuplicateAssignments_WhenMigratingLegacyObjectData()
    {
        var api = StaffTestCoreServerApiFactory.Create();
        var saveGame = (SaveGameProxy)(object)((WorldManagerProxy)(object)((CoreServerApiProxy)(object)api).WorldManager!).SaveGame!;
        var store = new StaffAssignmentStore(api);

        saveGame.StoreDataDirect("fst_staffroster", new StaffRoster
        {
            Assignments =
            [
                new StaffAssignment { PlayerUid = " uid-1 ", LastKnownPlayerName = "First", Level = StaffLevel.Moderator },
                new StaffAssignment { PlayerUid = "UID-1", LastKnownPlayerName = "Second", Level = StaffLevel.Admin },
                new StaffAssignment { PlayerUid = " ", LastKnownPlayerName = "Ignored", Level = StaffLevel.Admin }
            ]
        });

        StaffRoster loaded = store.LoadRoster();

        Assert.Single(loaded.Assignments);
        Assert.Equal("UID-1", loaded.Assignments[0].PlayerUid);
        Assert.Equal("Second", loaded.Assignments[0].LastKnownPlayerName);
        Assert.Equal(StaffLevel.Admin, loaded.Assignments[0].Level);
    }

    [Fact]
    public void LoadRoster_CanReadRewrittenJsonBytes_AfterLegacyMigration()
    {
        var api = StaffTestCoreServerApiFactory.Create();
        var saveGame = (SaveGameProxy)(object)((WorldManagerProxy)(object)((CoreServerApiProxy)(object)api).WorldManager!).SaveGame!;
        var store = new StaffAssignmentStore(api);

        saveGame.StoreDataDirect("fst_staffroster", new StaffRoster
        {
            Assignments =
            [
                new StaffAssignment { PlayerUid = "uid-1", LastKnownPlayerName = "Admin", Level = StaffLevel.Admin }
            ]
        });

        StaffRoster firstLoad = store.LoadRoster();
        StaffRoster secondLoad = store.LoadRoster();

        Assert.Single(firstLoad.Assignments);
        Assert.Single(secondLoad.Assignments);
        Assert.Equal("uid-1", secondLoad.Assignments[0].PlayerUid);
        Assert.True(saveGame.GetStoredValue("fst_staffroster") is byte[]);
    }
}
