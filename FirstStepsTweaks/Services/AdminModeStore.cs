using System;
using System.Text.Json;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class AdminModeStore : IAdminModeStore
    {
        private const string AdminModeKey = "fst_adminmode";

        public bool IsActive(IServerPlayer player)
        {
            return TryLoad(player, out AdminModeState state, out _) && state?.IsActive == true;
        }

        public bool TryLoad(IServerPlayer player, out AdminModeState state, out string errorMessage)
        {
            state = null;
            errorMessage = string.Empty;

            byte[] data = player?.GetModdata(AdminModeKey);
            if (data == null || data.Length == 0)
            {
                return false;
            }

            try
            {
                state = JsonSerializer.Deserialize<AdminModeState>(data);
                if (state == null)
                {
                    errorMessage = "Admin mode data is empty.";
                    return false;
                }

                NormalizeLegacyInventories(state);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Admin mode data is invalid: {exception.Message}";
                return false;
            }
        }

        public void Save(IServerPlayer player, AdminModeState state)
        {
            if (player == null || state == null)
            {
                return;
            }

            NormalizeLegacyInventories(state);
            player.SetModdata(AdminModeKey, JsonSerializer.SerializeToUtf8Bytes(state));
        }

        public void Clear(IServerPlayer player)
        {
            player?.SetModdata(AdminModeKey, null);
        }

        private static void NormalizeLegacyInventories(AdminModeState state)
        {
            if (state == null)
            {
                return;
            }

            state.SurvivalInventories ??= new();
            state.AdminInventories ??= new();

            if ((state.SurvivalInventories.Count == 0) && state.Inventories?.Count > 0)
            {
                state.SurvivalInventories = state.Inventories;
            }

            state.Inventories = null;
        }
    }
}
