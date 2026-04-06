using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class StaffPrivilegeCatalog
    {
        public const string AdminPrivilege = "firststepstweaks.staff.admin";
        public const string ModeratorPrivilege = "firststepstweaks.staff.moderator";
        private static readonly string[] ModeratorPrivileges =
        {
            ModeratorPrivilege,
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
        };

        private static readonly string[] AdminOnlyPrivileges =
        {
            AdminPrivilege,
            "controlserver",
            "gamemode",
            "freemove",
            "pickingrange",
            "worldedit",
            "give",
            "tp",
            "time",
            "firststepstweaks.graveadmin",
            "firststepstweaks.bypassteleportcooldown",
            "worldaudit.rollback",
            "worldaudit.restore",
            "worldaudit.purge",
            "worldaudit.reload",
            "worldaudit.status",
            "worldaudit.consumer",
            "worldaudit.admin"
        };

        public IReadOnlyCollection<string> GetPrivilegesFor(StaffLevel level)
        {
            return level switch
            {
                StaffLevel.Admin => ModeratorPrivileges
                    .Concat(AdminOnlyPrivileges)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StaffLevel.Moderator => ModeratorPrivileges,
                _ => Array.Empty<string>()
            };
        }

        public IReadOnlyCollection<string> GetAllManagedPrivileges()
        {
            return GetPrivilegesFor(StaffLevel.Admin)
                .Concat(GetPrivilegesFor(StaffLevel.Moderator))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
