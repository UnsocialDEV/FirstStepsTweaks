using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class StaffPrivilegeCatalogTests
{
    private static readonly string[] ExpectedModeratorPrivileges =
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

    private static readonly string[] ExpectedAdminOnlyPrivileges =
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
    public void ModeratorPrivileges_ContainConfiguredModeratorBundle()
    {
        var catalog = new StaffPrivilegeCatalog();
        var privileges = catalog.GetPrivilegesFor(StaffLevel.Moderator);

        Assert.Equal(ExpectedModeratorPrivileges.Length, privileges.Count);
        foreach (string privilege in ExpectedModeratorPrivileges)
        {
            Assert.Contains(privilege, privileges);
        }
    }

    [Fact]
    public void AdminPrivileges_IncludeModeratorAndAdminManagedPrivileges()
    {
        var catalog = new StaffPrivilegeCatalog();
        var privileges = catalog.GetPrivilegesFor(StaffLevel.Admin);

        foreach (string privilege in ExpectedModeratorPrivileges)
        {
            Assert.Contains(privilege, privileges);
        }

        foreach (string privilege in ExpectedAdminOnlyPrivileges)
        {
            Assert.Contains(privilege, privileges);
        }
    }

    [Fact]
    public void AdminPrivileges_DedupeSharedPrivileges()
    {
        var catalog = new StaffPrivilegeCatalog();
        var privileges = catalog.GetPrivilegesFor(StaffLevel.Admin);

        Assert.Equal(1, privileges.Count(privilege => privilege == "firststepstweaks.bypassteleportcooldown"));
    }
}
