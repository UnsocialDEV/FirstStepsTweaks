using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public static class ItemService
    {
        public static void GiveCollectible(ICoreServerAPI api, IServerPlayer player, string code, int quantity)
        {
            if (string.IsNullOrWhiteSpace(code) || quantity <= 0)
                return;

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

            player.InventoryManager.TryGiveItemstack(stack, true);

            if (stack.StackSize > 0)
            {
                api.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }
        }
    }
}
