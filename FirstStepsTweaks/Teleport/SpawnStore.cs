using FirstStepsTweaks.Infrastructure.Coordinates;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Teleport
{
    public sealed class SpawnStore
    {
        private const string SpawnKey = "fst_spawnpos";
        private readonly ICoreServerAPI api;
        private readonly IWorldCoordinateReader coordinateReader;

        public SpawnStore(ICoreServerAPI api)
            : this(api, new WorldCoordinateReader())
        {
        }

        public SpawnStore(ICoreServerAPI api, IWorldCoordinateReader coordinateReader)
        {
            this.api = api;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public void SetSpawn(IServerPlayer player)
        {
            Vec3d position = coordinateReader.GetExactPosition(player);
            if (position == null)
            {
                return;
            }

            double[] spawnData =
            {
                position.X,
                position.Y,
                position.Z
            };

            api.WorldManager.SaveGame.StoreData(SpawnKey, spawnData);
        }

        public bool TryGetSpawn(out Vec3d position)
        {
            position = null;
            double[] spawnData = api.WorldManager.SaveGame.GetData<double[]>(SpawnKey);
            if (spawnData == null || spawnData.Length != 3)
            {
                return false;
            }

            position = new Vec3d(spawnData[0], spawnData[1], spawnData[2]);
            return true;
        }

        public void ClearSpawn()
        {
            api.WorldManager.SaveGame.StoreData(SpawnKey, (double[])null);
        }
    }
}
