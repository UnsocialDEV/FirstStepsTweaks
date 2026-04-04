using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public interface IGraveSnapshotter
    {
        List<PlayerInventorySnapshot> SnapshotRelevantInventories(IServerPlayer player, List<string> debugEntries = null);

        void RemoveSnapshottedItems(IServerPlayer player, List<PlayerInventorySnapshot> snapshots, bool debugTrace = false);
    }
}
