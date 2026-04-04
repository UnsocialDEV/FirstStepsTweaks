using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class AdminModeLoadoutService : IAdminModeLoadoutService
    {
        private readonly ICoreServerAPI api;
        private readonly IPlayerLoadoutManager loadoutManager;
        private readonly IWorldCoordinateReader coordinateReader;

        public AdminModeLoadoutService(
            ICoreServerAPI api,
            IPlayerLoadoutManager loadoutManager,
            IWorldCoordinateReader coordinateReader)
        {
            this.api = api;
            this.loadoutManager = loadoutManager;
            this.coordinateReader = coordinateReader;
        }

        public List<PlayerInventorySnapshot> SnapshotLiveLoadout(IServerPlayer player)
        {
            return loadoutManager?.Snapshot(player, PlayerLoadoutScope.AdminMode) ?? new List<PlayerInventorySnapshot>();
        }

        public List<PlayerInventorySnapshot> SnapshotInitialAdminLoadout(IServerPlayer player)
        {
            return loadoutManager?.Snapshot(player, PlayerLoadoutScope.AdminModeInitialSeed) ?? new List<PlayerInventorySnapshot>();
        }

        public bool TryEquipLoadout(
            IServerPlayer player,
            IReadOnlyCollection<PlayerInventorySnapshot> currentLoadout,
            IReadOnlyCollection<PlayerInventorySnapshot> targetLoadout)
        {
            if (player == null)
            {
                return false;
            }

            loadoutManager.Clear(player, currentLoadout);
            if (targetLoadout == null || targetLoadout.Count == 0)
            {
                return true;
            }

            if (loadoutManager.TryRestore(targetLoadout, player, coordinateReader.GetBlockPosition(player), out _, out _))
            {
                return true;
            }

            if (currentLoadout == null || currentLoadout.Count == 0)
            {
                return false;
            }

            if (loadoutManager.TryRestore(currentLoadout, player, coordinateReader.GetBlockPosition(player), out _, out _))
            {
                return false;
            }

            api?.Logger?.Error($"[FirstStepsTweaks] Failed to restore the original admin-mode loadout for {player.PlayerName} after a swap failure.");
            return false;
        }
    }
}
