using System.Collections.Generic;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public interface IGraveSnapshotter
    {
        List<GraveInventorySnapshot> SnapshotRelevantInventories(IServerPlayer player, List<string> debugEntries = null);

        void RemoveSnapshottedItems(IServerPlayer player, List<GraveInventorySnapshot> snapshots, bool debugTrace = false);
    }
}
