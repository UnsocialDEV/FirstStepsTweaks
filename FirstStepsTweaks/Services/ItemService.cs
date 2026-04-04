using FirstStepsTweaks.Infrastructure.Coordinates;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public static class ItemService
    {
        private static readonly IWorldCoordinateReader CoordinateReader = new WorldCoordinateReader();

        public static void GiveCollectible(ICoreServerAPI api, IServerPlayer player, string code, int quantity)
        {
            if (string.IsNullOrWhiteSpace(code) || quantity <= 0)
            {
                return;
            }

            AssetLocation asset = new AssetLocation(code);

            CollectibleObject collectible =
                api.World.GetItem(asset) as CollectibleObject ??
                api.World.GetBlock(asset) as CollectibleObject;

            if (collectible == null)
            {
                api.Logger.Error($"[FirstStepsTweaks] Collectible not found -> {code}");
                return;
            }

            ItemStack stack = new ItemStack(collectible, quantity);

            bool fullyGiven = player.InventoryManager.TryGiveItemstack(stack, true);

            if (!fullyGiven && stack.StackSize > 0)
            {
                var position = CoordinateReader.GetExactPosition(player);
                if (position != null)
                {
                    api.World.SpawnItemEntity(stack, position);
                }
            }
        }
    }
}
