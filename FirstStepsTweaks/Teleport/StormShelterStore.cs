using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Teleport
{
    public sealed class StormShelterStore
    {
        private const string StormShelterKey = "fst_stormshelter";
        private readonly ICoreServerAPI api;

        public StormShelterStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void SetStormShelter(IServerPlayer player)
        {
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            double[] shelterData =
            {
                player.Entity.Pos.X,
                player.Entity.Pos.Y,
                player.Entity.Pos.Z
            };

            api.WorldManager.SaveGame.StoreData(StormShelterKey, shelterData);
        }

        public bool TryGetStormShelter(out Vec3d position)
        {
            position = null;
            double[] shelterData = api.WorldManager.SaveGame.GetData<double[]>(StormShelterKey);
            if (shelterData == null || shelterData.Length != 3)
            {
                return false;
            }

            position = new Vec3d(shelterData[0], shelterData[1], shelterData[2]);
            return true;
        }

        public void ClearStormShelter()
        {
            api.WorldManager.SaveGame.StoreData(StormShelterKey, (double[])null);
        }
    }
}
