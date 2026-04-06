using System;
using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Services;

namespace FirstStepsTweaks.Config
{
    public sealed class StaffConfigUpgrader
    {
        private readonly IStaffAssignmentStore staffAssignmentStore;

        public StaffConfigUpgrader(IStaffAssignmentStore staffAssignmentStore)
        {
            this.staffAssignmentStore = staffAssignmentStore;
        }

        public bool TryUpgradeLegacyAdminPlayerNames(IEnumerable<string> legacyAdminPlayerNames)
        {
            if (legacyAdminPlayerNames == null)
            {
                return false;
            }

            StaffRoster roster = staffAssignmentStore.LoadRoster();
            bool changed = false;

            foreach (string playerName in legacyAdminPlayerNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                bool alreadyManaged = roster.Assignments.Exists(assignment =>
                    string.Equals(assignment?.LastKnownPlayerName, playerName, StringComparison.OrdinalIgnoreCase));
                bool alreadyPending = roster.LegacyAssignments.Exists(assignment =>
                    string.Equals(assignment?.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));

                if (alreadyManaged || alreadyPending)
                {
                    continue;
                }

                roster.LegacyAssignments.Add(new LegacyStaffAssignment
                {
                    PlayerName = playerName,
                    Level = StaffLevel.Admin
                });
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            staffAssignmentStore.SaveRoster(roster);
            return true;
        }
    }
}
