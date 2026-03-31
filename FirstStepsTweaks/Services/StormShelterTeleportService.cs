using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class StormShelterTeleportService
    {
        private readonly StormShelterStore stormShelterStore;
        private readonly IBackLocationStore backLocationStore;

        public StormShelterTeleportService(StormShelterStore stormShelterStore, IBackLocationStore backLocationStore)
        {
            this.stormShelterStore = stormShelterStore;
            this.backLocationStore = backLocationStore;
        }

        public StormShelterTeleportResult TryTeleport(IServerPlayer player, System.Action<double, double, double> teleport)
        {
            if (!stormShelterStore.TryGetStormShelter(out var target))
            {
                return StormShelterTeleportResult.NotSet;
            }

            backLocationStore.RecordCurrentLocation(player);
            teleport(target.X, target.Y, target.Z);

            return StormShelterTeleportResult.Success;
        }
    }

    public enum StormShelterTeleportResult
    {
        NotSet = 0,
        Success = 1
    }
}
