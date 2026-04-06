using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class StaffAssignmentStore : IStaffAssignmentStore
    {
        private const string StaffRosterKey = "fst_staffroster";
        private readonly ICoreServerAPI api;

        public StaffAssignmentStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public StaffRoster LoadRoster()
        {
            if (TryLoadJsonRoster(out StaffRoster roster))
            {
                Normalize(roster);
                return roster;
            }

            if (TryLoadLegacyRoster(out roster))
            {
                Normalize(roster);
                SaveRoster(roster);
                api.Logger.Notification($"[FirstStepsTweaks] Migrated legacy staff roster data stored under '{StaffRosterKey}' to JSON bytes.");
                return roster;
            }

            api.Logger.Warning($"[FirstStepsTweaks] Staff roster key '{StaffRosterKey}' contains invalid or unsupported data. Returning an empty roster.");
            roster = new StaffRoster();
            Normalize(roster);
            return roster;
        }

        public void SaveRoster(StaffRoster roster)
        {
            if (roster == null)
            {
                return;
            }

            Normalize(roster);
            SaveRawBytes(JsonSerializer.SerializeToUtf8Bytes(roster));
        }

        private bool TryLoadJsonRoster(out StaffRoster roster)
        {
            roster = null;

            if (!TryReadRawBytes(out byte[] data))
            {
                return false;
            }

            try
            {
                roster = JsonSerializer.Deserialize<StaffRoster>(data);
                return roster != null;
            }
            catch
            {
                return false;
            }
        }

        private bool TryReadRawBytes(out byte[] data)
        {
            data = null;

            try
            {
                data = api.WorldManager.SaveGame.GetData(StaffRosterKey);
                if (data != null && data.Length > 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                data = api.WorldManager.SaveGame.GetData<byte[]>(StaffRosterKey);
                return data != null && data.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool TryLoadLegacyRoster(out StaffRoster roster)
        {
            roster = null;

            try
            {
                roster = api.WorldManager.SaveGame.GetData<StaffRoster>(StaffRosterKey);
                return roster != null;
            }
            catch
            {
                return false;
            }
        }

        private void SaveRawBytes(byte[] data)
        {
            api.WorldManager.SaveGame.StoreData(StaffRosterKey, Array.Empty<byte>());
            api.WorldManager.SaveGame.StoreData(StaffRosterKey, data);
        }

        private static void Normalize(StaffRoster roster)
        {
            roster.Assignments ??= new List<StaffAssignment>();
            roster.LegacyAssignments ??= new List<LegacyStaffAssignment>();

            roster.Assignments = roster.Assignments
                .Where(assignment => assignment != null && !string.IsNullOrWhiteSpace(assignment.PlayerUid))
                .GroupBy(assignment => assignment.PlayerUid.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => NormalizeAssignment(group.Last()))
                .ToList();

            roster.LegacyAssignments = roster.LegacyAssignments
                .Where(assignment => assignment != null && !string.IsNullOrWhiteSpace(assignment.PlayerName))
                .GroupBy(assignment => assignment.PlayerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => NormalizeLegacyAssignment(group.Last()))
                .ToList();
        }

        private static StaffAssignment NormalizeAssignment(StaffAssignment assignment)
        {
            assignment.PlayerUid = (assignment.PlayerUid ?? string.Empty).Trim();
            assignment.LastKnownPlayerName = (assignment.LastKnownPlayerName ?? string.Empty).Trim();
            return assignment;
        }

        private static LegacyStaffAssignment NormalizeLegacyAssignment(LegacyStaffAssignment assignment)
        {
            assignment.PlayerName = (assignment.PlayerName ?? string.Empty).Trim();
            return assignment;
        }
    }
}
