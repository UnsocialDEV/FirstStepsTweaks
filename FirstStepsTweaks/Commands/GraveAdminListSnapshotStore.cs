using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminListSnapshotStore
    {
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, GraveAdminListSnapshot> snapshotsByPlayerUid =
            new Dictionary<string, GraveAdminListSnapshot>(StringComparer.OrdinalIgnoreCase);

        public void Save(IServerPlayer player, int radius, IReadOnlyList<GraveAdminListEntry> entries)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PlayerUID))
            {
                return;
            }

            lock (syncRoot)
            {
                snapshotsByPlayerUid[player.PlayerUID] = new GraveAdminListSnapshot(radius, entries);
            }
        }

        public bool TryGet(IServerPlayer player, out GraveAdminListSnapshot snapshot)
        {
            snapshot = null;
            if (player == null || string.IsNullOrWhiteSpace(player.PlayerUID))
            {
                return false;
            }

            lock (syncRoot)
            {
                return snapshotsByPlayerUid.TryGetValue(player.PlayerUID, out snapshot) && snapshot != null;
            }
        }
    }
}
