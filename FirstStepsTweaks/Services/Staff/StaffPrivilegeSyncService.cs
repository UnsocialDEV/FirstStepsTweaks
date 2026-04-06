using System;
using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class StaffPrivilegeSyncService
    {
        private readonly IStaffAssignmentStore staffAssignmentStore;
        private readonly IPlayerPrivilegeReader privilegeReader;
        private readonly IPlayerPrivilegeMutator privilegeMutator;
        private readonly StaffPrivilegeCatalog privilegeCatalog;

        public StaffPrivilegeSyncService(
            IStaffAssignmentStore staffAssignmentStore,
            IPlayerPrivilegeReader privilegeReader,
            IPlayerPrivilegeMutator privilegeMutator,
            StaffPrivilegeCatalog privilegeCatalog)
        {
            this.staffAssignmentStore = staffAssignmentStore;
            this.privilegeReader = privilegeReader;
            this.privilegeMutator = privilegeMutator;
            this.privilegeCatalog = privilegeCatalog;
        }

        public void SyncOnlinePlayer(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            StaffLevel level = ResolveAssignmentLevel(player.PlayerUID);
            ApplyAssignmentToOnlinePlayer(player, level);
        }

        public void ApplyAssignmentToOnlinePlayer(IServerPlayer player, StaffLevel level)
        {
            if (player == null)
            {
                return;
            }

            HashSet<string> targetPrivileges = new HashSet<string>(privilegeCatalog.GetPrivilegesFor(level), StringComparer.OrdinalIgnoreCase);
            foreach (string privilege in GetCurrentManagedPrivileges(player))
            {
                if (targetPrivileges.Contains(privilege))
                {
                    continue;
                }

                privilegeMutator.Revoke(player, privilege);
            }

            foreach (string privilege in targetPrivileges)
            {
                if (privilegeReader.HasPrivilege(player, privilege))
                {
                    continue;
                }

                privilegeMutator.Grant(player, privilege);
            }
        }

        public void RemoveManagedPrivileges(IServerPlayer player)
        {
            ApplyAssignmentToOnlinePlayer(player, StaffLevel.None);
        }

        public bool HasExpectedPrivileges(IServerPlayer player, StaffLevel level)
        {
            if (player == null)
            {
                return false;
            }

            HashSet<string> targetPrivileges = new HashSet<string>(privilegeCatalog.GetPrivilegesFor(level), StringComparer.OrdinalIgnoreCase);
            foreach (string privilege in privilegeCatalog.GetAllManagedPrivileges())
            {
                bool shouldHavePrivilege = targetPrivileges.Contains(privilege);
                bool hasPrivilege = privilegeReader.HasPrivilege(player, privilege);
                if (shouldHavePrivilege != hasPrivilege)
                {
                    return false;
                }
            }

            return true;
        }

        private StaffLevel ResolveAssignmentLevel(string playerUid)
        {
            StaffRoster roster = staffAssignmentStore.LoadRoster();
            StaffAssignment assignment = roster.Assignments.FirstOrDefault(candidate =>
                string.Equals(candidate.PlayerUid, playerUid, StringComparison.OrdinalIgnoreCase));

            return assignment?.Level ?? StaffLevel.None;
        }

        private IReadOnlyCollection<string> GetCurrentManagedPrivileges(IServerPlayer player)
        {
            return privilegeCatalog.GetAllManagedPrivileges()
                .Where(privilege => privilegeReader.HasPrivilege(player, privilege))
                .ToArray();
        }
    }
}
