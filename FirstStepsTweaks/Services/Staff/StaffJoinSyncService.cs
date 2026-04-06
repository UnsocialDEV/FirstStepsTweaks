using System;
using System.Linq;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class StaffJoinSyncService
    {
        private readonly IStaffAssignmentStore staffAssignmentStore;
        private readonly StaffPrivilegeSyncService staffPrivilegeSyncService;

        public StaffJoinSyncService(
            IStaffAssignmentStore staffAssignmentStore,
            StaffPrivilegeSyncService staffPrivilegeSyncService)
        {
            this.staffAssignmentStore = staffAssignmentStore;
            this.staffPrivilegeSyncService = staffPrivilegeSyncService;
        }

        public void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            StaffRoster roster = staffAssignmentStore.LoadRoster();
            bool changed = false;

            LegacyStaffAssignment legacyAssignment = roster.LegacyAssignments.FirstOrDefault(candidate =>
                string.Equals(candidate.PlayerName, player.PlayerName, StringComparison.OrdinalIgnoreCase));
            StaffAssignment assignment = roster.Assignments.FirstOrDefault(candidate =>
                string.Equals(candidate.PlayerUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase));

            if (legacyAssignment != null)
            {
                StaffLevel effectiveLevel = assignment == null
                    ? legacyAssignment.Level
                    : MaxLevel(assignment.Level, legacyAssignment.Level);

                if (assignment == null)
                {
                    roster.Assignments.Add(new StaffAssignment
                    {
                        PlayerUid = player.PlayerUID,
                        LastKnownPlayerName = player.PlayerName,
                        Level = effectiveLevel
                    });
                }
                else
                {
                    assignment.Level = effectiveLevel;
                    assignment.LastKnownPlayerName = player.PlayerName ?? string.Empty;
                }

                roster.LegacyAssignments.RemoveAll(candidate =>
                    string.Equals(candidate.PlayerName, player.PlayerName, StringComparison.OrdinalIgnoreCase));
                changed = true;
            }
            else if (assignment != null && !string.Equals(assignment.LastKnownPlayerName, player.PlayerName, StringComparison.Ordinal))
            {
                assignment.LastKnownPlayerName = player.PlayerName ?? string.Empty;
                changed = true;
            }

            if (changed)
            {
                staffAssignmentStore.SaveRoster(roster);
            }

            staffPrivilegeSyncService.SyncOnlinePlayer(player);
        }

        private static StaffLevel MaxLevel(StaffLevel left, StaffLevel right)
        {
            return left >= right ? left : right;
        }
    }
}
