using FirstStepsTweaks.Infrastructure.Coordinates;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Teleport
{
    public sealed class StormShelterStore
    {
        private const string StormShelterKey = "fst_stormshelter";
        private readonly ICoreServerAPI api;
        private readonly IWorldCoordinateReader coordinateReader;

        public StormShelterStore(ICoreServerAPI api)
            : this(api, new WorldCoordinateReader())
        {
        }

        public StormShelterStore(ICoreServerAPI api, IWorldCoordinateReader coordinateReader)
        {
            this.api = api;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public void SetStormShelter(IServerPlayer player)
        {
            Vec3d position = coordinateReader.GetExactPosition(player);
            if (position == null)
            {
                return;
            }

            double[] shelterData =
            {
                position.X,
                position.Y,
                position.Z
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
