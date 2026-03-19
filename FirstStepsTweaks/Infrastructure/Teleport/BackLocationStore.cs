using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class BackLocationStore : IBackLocationStore
    {
        private readonly Dictionary<string, Vec3d> lastPositionsByPlayerUid = new Dictionary<string, Vec3d>();

        public void RecordCurrentLocation(IServerPlayer player)
        {
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            lastPositionsByPlayerUid[player.PlayerUID] = new Vec3d(player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);
        }

        public bool TryGet(string playerUid, out Vec3d location)
        {
            return lastPositionsByPlayerUid.TryGetValue(playerUid, out location);
        }

        public void Set(string playerUid, Vec3d location)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || location == null)
            {
                return;
            }

            lastPositionsByPlayerUid[playerUid] = location;
        }
    }
}
