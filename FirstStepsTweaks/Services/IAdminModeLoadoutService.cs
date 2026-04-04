using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public interface IAdminModeLoadoutService
    {
        List<PlayerInventorySnapshot> SnapshotLiveLoadout(IServerPlayer player);

        List<PlayerInventorySnapshot> SnapshotInitialAdminLoadout(IServerPlayer player);

        bool TryEquipLoadout(
            IServerPlayer player,
            IReadOnlyCollection<PlayerInventorySnapshot> currentLoadout,
            IReadOnlyCollection<PlayerInventorySnapshot> targetLoadout);
    }
}
