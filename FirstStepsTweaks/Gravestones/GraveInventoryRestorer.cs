using System;
using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public sealed class GraveInventoryRestorer : IGraveRestorer
    {
        private readonly ICoreServerAPI api;
        private readonly IGraveRepository graveRepository;
        private readonly IGraveBlockSynchronizer blockSynchronizer;
        private readonly IPlayerLoadoutManager loadoutManager;

        public GraveInventoryRestorer(
            ICoreServerAPI api,
            IGraveRepository graveRepository,
            IGraveBlockSynchronizer blockSynchronizer,
            IPlayerLoadoutManager loadoutManager)
        {
            this.api = api;
            this.graveRepository = graveRepository;
            this.blockSynchronizer = blockSynchronizer;
            this.loadoutManager = loadoutManager;
        }

        public int DuplicateToPlayer(GraveData grave, IServerPlayer targetPlayer)
        {
            return grave == null
                ? 0
                : loadoutManager.DuplicateToPlayer(grave.Inventories, targetPlayer, grave.ToBlockPos());
        }

        public bool TryRestore(GraveData grave, IServerPlayer targetPlayer, bool removeBlock, out int transferredStacks, out int failedStacks)
        {
            transferredStacks = 0;
            failedStacks = 0;

            if (grave == null || targetPlayer == null)
            {
                return false;
            }

            try
            {
                if (!loadoutManager.TryRestore(grave.Inventories, targetPlayer, grave.ToBlockPos(), out transferredStacks, out failedStacks))
                {
                    return false;
                }

                graveRepository.Remove(grave.GraveId, out GraveData removedGrave);

                if (removeBlock)
                {
                    blockSynchronizer.RemoveIfPresent(removedGrave ?? grave);
                }

                return true;
            }
            catch (Exception exception)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to restore gravestone '{grave.GraveId}': {exception}");

                if (graveRepository.TryGetById(grave.GraveId, out GraveData persistedGrave))
                {
                    blockSynchronizer.Ensure(persistedGrave);
                }

                return false;
            }
        }
    }
}
