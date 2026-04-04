using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public sealed class GraveInventorySnapshotter : IGraveSnapshotter
    {
        private readonly IPlayerLoadoutManager loadoutManager;

        public GraveInventorySnapshotter(IPlayerLoadoutManager loadoutManager)
        {
            this.loadoutManager = loadoutManager;
        }

        public List<PlayerInventorySnapshot> SnapshotRelevantInventories(IServerPlayer player, List<string> debugEntries = null)
        {
            return loadoutManager.Snapshot(player, debugEntries);
        }

        public void RemoveSnapshottedItems(IServerPlayer player, List<PlayerInventorySnapshot> snapshots, bool debugTrace = false)
        {
            loadoutManager.Clear(player, snapshots);
        }
    }
}
