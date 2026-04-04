using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Coordinates;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class BackLocationStore : IBackLocationStore
    {
        private readonly IWorldCoordinateReader coordinateReader;
        private readonly Dictionary<string, Vec3d> lastPositionsByPlayerUid = new Dictionary<string, Vec3d>();

        public BackLocationStore()
            : this(new WorldCoordinateReader())
        {
        }

        public BackLocationStore(IWorldCoordinateReader coordinateReader)
        {
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public void RecordCurrentLocation(IServerPlayer player)
        {
            Vec3d currentPosition = coordinateReader.GetExactPosition(player);
            if (string.IsNullOrWhiteSpace(player?.PlayerUID) || currentPosition == null)
            {
                return;
            }

            lastPositionsByPlayerUid[player.PlayerUID] = currentPosition;
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
