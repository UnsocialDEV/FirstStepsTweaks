using System;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorRoleTransitionApplier
    {
        private readonly ICoreServerAPI api;
        private readonly IPlayerRoleCodeReader roleCodeReader;
        private readonly IPlayerRoleAssigner roleAssigner;
        private readonly IPlayerDefaultRoleResetter defaultRoleResetter;

        public DonatorRoleTransitionApplier(
            ICoreServerAPI api,
            IPlayerRoleCodeReader roleCodeReader,
            IPlayerRoleAssigner roleAssigner,
            IPlayerDefaultRoleResetter defaultRoleResetter)
        {
            this.api = api;
            this.roleCodeReader = roleCodeReader;
            this.roleAssigner = roleAssigner;
            this.defaultRoleResetter = defaultRoleResetter;
        }

        public DonatorRoleTransitionResult Apply(IServerPlayer player, string targetRoleCode)
        {
            if (player == null)
            {
                return new DonatorRoleTransitionResult(false, false, string.Empty);
            }

            string currentRoleCode = roleCodeReader.Read(player) ?? string.Empty;
            string effectiveRoleCode = string.IsNullOrWhiteSpace(targetRoleCode)
                ? defaultRoleResetter.GetDefaultRoleCode()
                : targetRoleCode;

            if (string.Equals(currentRoleCode, effectiveRoleCode, StringComparison.OrdinalIgnoreCase))
            {
                return new DonatorRoleTransitionResult(true, false, effectiveRoleCode);
            }

            try
            {
                if (string.IsNullOrWhiteSpace(targetRoleCode))
                {
                    defaultRoleResetter.Reset(player);
                }
                else
                {
                    roleAssigner.Assign(player, targetRoleCode);
                }

                return new DonatorRoleTransitionResult(true, true, effectiveRoleCode);
            }
            catch (Exception exception)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to set donor role '{effectiveRoleCode}' for {player.PlayerName}: {exception}");
                return new DonatorRoleTransitionResult(false, false, currentRoleCode);
            }
        }
    }
}
