using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public sealed class StaffRoster
    {
        public List<StaffAssignment> Assignments { get; set; } = new List<StaffAssignment>();

        public List<LegacyStaffAssignment> LegacyAssignments { get; set; } = new List<LegacyStaffAssignment>();
    }
}
