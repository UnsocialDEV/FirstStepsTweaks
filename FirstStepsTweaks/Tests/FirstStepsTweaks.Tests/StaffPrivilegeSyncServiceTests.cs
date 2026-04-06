using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class StaffPrivilegeSyncServiceTests
{
    private static readonly string[] ModeratorOnlyExpectedPrivileges =
    [
        StaffPrivilegeCatalog.ModeratorPrivilege,
        "manageotherplayergroups",
        "buildblockseverywhere",
        "useblockseverywhere",
        "kick",
        "ban",
        "announce",
        "readlists",
        "commandplayer",
        "worldaudit.inspect",
        "worldaudit.lookup",
        "worldaudit.lookup.block",
        "worldaudit.lookup.container",
        "firststepstweaks.bypassteleportcooldown"
    ];

    private static readonly string[] AdminOnlyExpectedPrivileges =
    [
        StaffPrivilegeCatalog.AdminPrivilege,
        "controlserver",
        "gamemode",
        "freemove",
        "pickingrange",
        "worldedit",
        "give",
        "tp",
        "time",
        "firststepstweaks.graveadmin",
        "worldaudit.rollback",
        "worldaudit.restore",
        "worldaudit.purge",
        "worldaudit.reload",
        "worldaudit.status",
        "worldaudit.consumer",
        "worldaudit.admin"
    ];

    [Fact]
    public void SyncOnlinePlayer_GrantsModeratorPrivileges()
    {
        var player = StaffTestServerPlayerFactory.Create("uid-1", "Mod");
        var store = CreateStoreWithAssignment("uid-1", StaffLevel.Moderator);
        var mutator = new RecordingPrivilegeMutator();
        var service = new StaffPrivilegeSyncService(store, new PlayerPrivilegeReader(), mutator, new StaffPrivilegeCatalog());

        service.SyncOnlinePlayer(player);

        foreach (string privilege in ModeratorOnlyExpectedPrivileges)
        {
            Assert.Contains(privilege, ((TestServerPlayerProxy)(object)player).Privileges);
        }

        foreach (string privilege in AdminOnlyExpectedPrivileges)
        {
            Assert.DoesNotContain(privilege, ((TestServerPlayerProxy)(object)player).Privileges);
        }
    }

    [Fact]
    public void SyncOnlinePlayer_GrantsAdminPrivileges()
    {
        var player = StaffTestServerPlayerFactory.Create("uid-1", "Admin");
        var store = CreateStoreWithAssignment("uid-1", StaffLevel.Admin);
        var mutator = new RecordingPrivilegeMutator();
        var service = new StaffPrivilegeSyncService(store, new PlayerPrivilegeReader(), mutator, new StaffPrivilegeCatalog());

        service.SyncOnlinePlayer(player);

        foreach (string privilege in ModeratorOnlyExpectedPrivileges)
        {
            Assert.Contains(privilege, ((TestServerPlayerProxy)(object)player).Privileges);
        }

        foreach (string privilege in AdminOnlyExpectedPrivileges)
        {
            Assert.Contains(privilege, ((TestServerPlayerProxy)(object)player).Privileges);
        }
    }

    [Fact]
    public void ApplyAssignmentToOnlinePlayer_DowngradesAdminToModerator()
    {
        var player = StaffTestServerPlayerFactory.Create("uid-1", "Admin",
            [
                .. ModeratorOnlyExpectedPrivileges,
                .. AdminOnlyExpectedPrivileges
            ]);
        var mutator = new RecordingPrivilegeMutator();
        var service = new StaffPrivilegeSyncService(new StaffAssignmentStore(StaffTestCoreServerApiFactory.Create()), new PlayerPrivilegeReader(), mutator, new StaffPrivilegeCatalog());

        service.ApplyAssignmentToOnlinePlayer(player, StaffLevel.Moderator);

        foreach (string privilege in ModeratorOnlyExpectedPrivileges)
        {
            Assert.Contains(privilege, ((TestServerPlayerProxy)(object)player).Privileges);
        }

        foreach (string privilege in AdminOnlyExpectedPrivileges)
        {
            Assert.DoesNotContain(privilege, ((TestServerPlayerProxy)(object)player).Privileges);
        }
    }

    [Fact]
    public void RemoveManagedPrivileges_ClearsManagedPrivilegesOnly()
    {
        var player = StaffTestServerPlayerFactory.Create("uid-1", "Admin",
            [
                .. ModeratorOnlyExpectedPrivileges,
                .. AdminOnlyExpectedPrivileges,
                "custom"
            ]);
        var mutator = new RecordingPrivilegeMutator();
        var service = new StaffPrivilegeSyncService(new StaffAssignmentStore(StaffTestCoreServerApiFactory.Create()), new PlayerPrivilegeReader(), mutator, new StaffPrivilegeCatalog());

        service.RemoveManagedPrivileges(player);

        foreach (string privilege in ModeratorOnlyExpectedPrivileges)
        {
            Assert.DoesNotContain(privilege, ((TestServerPlayerProxy)(object)player).Privileges);
        }

        foreach (string privilege in AdminOnlyExpectedPrivileges)
        {
            Assert.DoesNotContain(privilege, ((TestServerPlayerProxy)(object)player).Privileges);
        }

        Assert.Contains("custom", ((TestServerPlayerProxy)(object)player).Privileges);
    }

    [Fact]
    public void SyncOnlinePlayer_PreservesUnrelatedPrivileges()
    {
        var player = StaffTestServerPlayerFactory.Create("uid-1", "Mod", ["custom"]);
        var store = CreateStoreWithAssignment("uid-1", StaffLevel.Moderator);
        var mutator = new RecordingPrivilegeMutator();
        var service = new StaffPrivilegeSyncService(store, new PlayerPrivilegeReader(), mutator, new StaffPrivilegeCatalog());

        service.SyncOnlinePlayer(player);

        Assert.Contains("custom", ((TestServerPlayerProxy)(object)player).Privileges);
    }

    private static IStaffAssignmentStore CreateStoreWithAssignment(string playerUid, StaffLevel level)
    {
        var store = new StaffAssignmentStore(StaffTestCoreServerApiFactory.Create());
        store.SaveRoster(new StaffRoster
        {
            Assignments =
            [
                new StaffAssignment { PlayerUid = playerUid, LastKnownPlayerName = playerUid, Level = level }
            ]
        });
        return store;
    }
}
