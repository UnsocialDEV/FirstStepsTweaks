using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class AdminModePriorRoleUpdater
    {
        private readonly ICoreServerAPI api;
        private readonly IAdminModeStore adminModeStore;

        public AdminModePriorRoleUpdater(ICoreServerAPI api, IAdminModeStore adminModeStore)
        {
            this.api = api;
            this.adminModeStore = adminModeStore;
        }

        public void UpdateIfActive(IServerPlayer player, string roleCode)
        {
            if (player == null || string.IsNullOrWhiteSpace(roleCode))
            {
                return;
            }

            if (!adminModeStore.TryLoad(player, out AdminModeState state, out string errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    api.Logger.Error($"[FirstStepsTweaks] Failed to update stored admin mode role for {player.PlayerName}: {errorMessage}");
                }

                return;
            }

            if (!state.IsActive || string.Equals(state.PriorRoleCode, roleCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            state.PriorRoleCode = roleCode;
            adminModeStore.Save(player, state);
        }
    }
}
