using System;
using System.Linq;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminSelectorResolver
    {
        private const string CurrentLocationSelector = "currentloc";

        private readonly GraveAdminListSnapshotStore snapshotStore;
        private readonly IGraveAdminGraveResolver graveResolver;

        public GraveAdminSelectorResolver(GraveAdminListSnapshotStore snapshotStore, IGraveAdminGraveResolver graveResolver)
        {
            this.snapshotStore = snapshotStore;
            this.graveResolver = graveResolver;
        }

        public bool TryResolve(IServerPlayer caller, string selector, out string graveId, out string message)
        {
            graveId = string.Empty;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(selector))
            {
                message = "Specify a grave number, grave ID, or use currentloc while looking at a gravestone.";
                return false;
            }

            if (selector.Equals(CurrentLocationSelector, StringComparison.OrdinalIgnoreCase))
            {
                return graveResolver.TryResolveTargetedGraveId(caller, out graveId, out message);
            }

            if (!int.TryParse(selector, out int index))
            {
                graveId = selector;
                return true;
            }

            if (index <= 0)
            {
                message = "Grave number must be a positive whole number.";
                return false;
            }

            if (!snapshotStore.TryGet(caller, out GraveAdminListSnapshot snapshot) || snapshot?.Entries == null || snapshot.Entries.Count == 0)
            {
                message = "Run /graveadmin list first, then use the grave number from that list.";
                return false;
            }

            GraveAdminListEntry match = snapshot.Entries.FirstOrDefault(entry => entry.Index == index);
            if (match?.Grave == null)
            {
                message = $"Grave number {index} is not on your last /graveadmin list. Valid range is 1-{snapshot.Entries.Count}.";
                return false;
            }

            graveId = match.Grave.GraveId;
            return true;
        }
    }
}
