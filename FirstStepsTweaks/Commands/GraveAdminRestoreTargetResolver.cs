using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminRestoreTargetResolver
    {
        private readonly IPlayerLookup playerLookup;

        public GraveAdminRestoreTargetResolver(IPlayerLookup playerLookup)
        {
            this.playerLookup = playerLookup;
        }

        public bool TryResolve(string explicitTargetQuery, GraveData grave, out IServerPlayer targetPlayer, out string message)
        {
            targetPlayer = null;
            message = string.Empty;

            if (!string.IsNullOrWhiteSpace(explicitTargetQuery))
            {
                targetPlayer = playerLookup.FindOnlinePlayerByName(explicitTargetQuery) ?? playerLookup.FindOnlinePlayerByUid(explicitTargetQuery);
                if (targetPlayer != null)
                {
                    return true;
                }

                message = "Target player is not online.";
                return false;
            }

            if (grave == null)
            {
                message = "Gravestone was not found.";
                return false;
            }

            targetPlayer = playerLookup.FindOnlinePlayerByUid(grave.OwnerUid) ?? playerLookup.FindOnlinePlayerByName(grave.OwnerName);
            if (targetPlayer != null)
            {
                return true;
            }

            string ownerName = string.IsNullOrWhiteSpace(grave.OwnerName) ? grave.OwnerUid : grave.OwnerName;
            message = $"Grave owner '{ownerName}' must be online to restore without specifying a player.";
            return false;
        }
    }
}
