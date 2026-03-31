using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Teleport
{
    public sealed class SpawnStore
    {
        private const string SpawnKey = "fst_spawnpos";
        private readonly ICoreServerAPI api;

        public SpawnStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void SetSpawn(IServerPlayer player)
        {
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            double[] spawnData =
            {
                player.Entity.Pos.X,
                player.Entity.Pos.Y,
                player.Entity.Pos.Z
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
