using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Teleport
{
    public sealed class WarpStore
    {
        private const string WarpDataKey = "fst_warps";
        private readonly ICoreServerAPI api;

        public WarpStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public Dictionary<string, double[]> LoadWarps()
        {
            return api.WorldManager.SaveGame.GetData<Dictionary<string, double[]>>(WarpDataKey)
                ?? new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        }

        public void SaveWarps(Dictionary<string, double[]> warps)
        {
            api.WorldManager.SaveGame.StoreData(WarpDataKey, warps);
        }

        public void ClearWarps()
        {
            api.WorldManager.SaveGame.StoreData(WarpDataKey, (Dictionary<string, double[]>)null);
        }

        public string NormalizeWarpName(string warpName)
        {
            return (warpName ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
