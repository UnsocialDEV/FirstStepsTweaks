using System;
using System.Linq;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class StaffStatusReader : IStaffStatusReader
    {
        private readonly IStaffAssignmentStore staffAssignmentStore;

        public StaffStatusReader(IStaffAssignmentStore staffAssignmentStore)
        {
            this.staffAssignmentStore = staffAssignmentStore;
        }

        public StaffLevel GetLevel(IServerPlayer player)
        {
            return player == null
                ? StaffLevel.None
                : GetLevel(player.PlayerUID, player.PlayerName);
        }

        public StaffLevel GetLevel(string playerUid, string playerName = null)
        {
            StaffRoster roster = staffAssignmentStore.LoadRoster();
            StaffAssignment assignment = roster.Assignments.FirstOrDefault(candidate =>
                string.Equals(candidate.PlayerUid, playerUid, StringComparison.OrdinalIgnoreCase));

            if (assignment != null)
            {
                return assignment.Level;
            }

            if (string.IsNullOrWhiteSpace(playerName))
            {
                return StaffLevel.None;
            }

            LegacyStaffAssignment legacyAssignment = roster.LegacyAssignments.FirstOrDefault(candidate =>
                string.Equals(candidate.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));

            return legacyAssignment?.Level ?? StaffLevel.None;
        }

        public bool IsModerator(IServerPlayer player)
        {
            return GetLevel(player) >= StaffLevel.Moderator;
        }

        public bool IsModerator(string playerUid, string playerName = null)
        {
            return GetLevel(playerUid, playerName) >= StaffLevel.Moderator;
        }

        public bool IsAdmin(IServerPlayer player)
        {
            return GetLevel(player) == StaffLevel.Admin;
        }

        public bool IsAdmin(string playerUid, string playerName = null)
        {
            return GetLevel(playerUid, playerName) == StaffLevel.Admin;
        }
    }
}
