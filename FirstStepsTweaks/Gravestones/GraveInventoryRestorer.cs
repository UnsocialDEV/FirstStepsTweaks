using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public sealed class GraveInventoryRestorer : IGraveRestorer
    {
        private readonly ICoreServerAPI api;
        private readonly IGraveRepository graveRepository;
        private readonly IGraveBlockSynchronizer blockSynchronizer;

        public GraveInventoryRestorer(ICoreServerAPI api, IGraveRepository graveRepository, IGraveBlockSynchronizer blockSynchronizer)
        {
            this.api = api;
            this.graveRepository = graveRepository;
            this.blockSynchronizer = blockSynchronizer;
        }

        public int DuplicateToPlayer(GraveData grave, IServerPlayer targetPlayer)
        {
            if (grave == null || targetPlayer == null)
            {
                return 0;
            }

            int stackCount = 0;
            foreach (GraveInventorySnapshot inventory in grave.Inventories ?? Enumerable.Empty<GraveInventorySnapshot>())
            {
                foreach (GraveSlotSnapshot slot in inventory.Slots ?? Enumerable.Empty<GraveSlotSnapshot>())
                {
                    ItemStack stack = DeserializeItemStack(slot);
                    if (stack == null || stack.StackSize <= 0)
                    {
                        continue;
                    }

                    if (TransferStackToPlayerOrWorld(targetPlayer, stack, grave.ToBlockPos()))
                    {
                        stackCount++;
                    }
                }
            }

            return stackCount;
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
                BlockPos gravePos = grave.ToBlockPos();
                grave.Inventories ??= new List<GraveInventorySnapshot>();

                for (int inventoryIndex = 0; inventoryIndex < grave.Inventories.Count;)
                {
                    GraveInventorySnapshot inventory = grave.Inventories[inventoryIndex];
                    if (inventory?.Slots == null || inventory.Slots.Count == 0)
                    {
                        grave.Inventories.RemoveAt(inventoryIndex);
                        graveRepository.Upsert(grave);
                        continue;
                    }

                    while (inventory.Slots.Count > 0)
                    {
                        GraveSlotSnapshot slot = inventory.Slots[0];

                        try
                        {
                            if (RestoreToOriginalSlotOrFallback(targetPlayer, inventory, slot, gravePos))
                            {
                                transferredStacks++;
                            }
                            else
                            {
                                failedStacks++;
                            }
                        }
                        catch (Exception ex)
                        {
                            failedStacks++;
                            api.Logger.Error($"[FirstStepsTweaks] Failed to restore one stack from gravestone '{grave.GraveId}': {ex}");
                        }
                        finally
                        {
                            inventory.Slots.RemoveAt(0);
                            graveRepository.Upsert(grave);
                        }
                    }

                    grave.Inventories.RemoveAt(inventoryIndex);
                    graveRepository.Upsert(grave);
                }

                graveRepository.Remove(grave.GraveId, out GraveData removedGrave);

                if (removeBlock)
                {
                    blockSynchronizer.RemoveIfPresent(removedGrave ?? grave);
                }

                targetPlayer.InventoryManager.BroadcastHotbarSlot();
                return true;
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to restore gravestone '{grave.GraveId}': {ex}");

                if (graveRepository.TryGetById(grave.GraveId, out GraveData persistedGrave))
                {
                    blockSynchronizer.Ensure(persistedGrave);
                }

                return false;
            }
        }

        private bool RestoreToOriginalSlotOrFallback(
            IServerPlayer targetPlayer,
            GraveInventorySnapshot inventorySnapshot,
            GraveSlotSnapshot slotSnapshot,
            BlockPos fallbackPos)
        {
            ItemStack stack = DeserializeItemStack(slotSnapshot);
            if (stack == null || stack.StackSize <= 0)
            {
                return false;
            }

            IInventory inventory = ResolveInventory(targetPlayer, inventorySnapshot);
            if (inventory != null && slotSnapshot != null && slotSnapshot.SlotId >= 0 && slotSnapshot.SlotId < inventory.Count)
            {
                ItemSlot targetSlot = inventory[slotSnapshot.SlotId];
                if (targetSlot != null && targetSlot.Empty)
                {
                    targetSlot.Itemstack = stack;
                    targetSlot.MarkDirty();
                    return true;
                }

                if (targetSlot?.Itemstack != null && AreStacksEquivalent(targetSlot.Itemstack, stack))
                {
                    return true;
                }
            }

            return TransferStackToPlayerOrWorld(targetPlayer, stack, fallbackPos);
        }

        private static bool AreStacksEquivalent(ItemStack left, ItemStack right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            byte[] leftBytes = left.Clone()?.ToBytes();
            byte[] rightBytes = right.Clone()?.ToBytes();
            if (leftBytes == null || rightBytes == null || leftBytes.Length != rightBytes.Length)
            {
                return false;
            }

            for (int index = 0; index < leftBytes.Length; index++)
            {
                if (leftBytes[index] != rightBytes[index])
                {
                    return false;
                }
            }

            return true;
        }

        private bool TransferStackToPlayerOrWorld(IServerPlayer targetPlayer, ItemStack stack, BlockPos fallbackPos)
        {
            if (targetPlayer?.InventoryManager == null || stack == null || stack.StackSize <= 0)
            {
                return false;
            }

            if (stack.Collectible == null)
            {
                api.Logger.Warning("[FirstStepsTweaks] Skipping unresolved item stack during gravestone restore.");
                return false;
            }

            try
            {
                bool fullyGiven = targetPlayer.InventoryManager.TryGiveItemstack(stack, true);
                if (fullyGiven || stack.StackSize <= 0)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to insert stack into inventory, dropping instead: {ex}");
            }

            try
            {
                Vec3d dropPos = targetPlayer.Entity?.Pos?.XYZ ?? fallbackPos.ToVec3d().Add(0.5, 0.5, 0.5);
                api.World.SpawnItemEntity(stack, dropPos);
                return true;
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to drop unresolved stack from gravestone restore: {ex}");
                return false;
            }
        }

        private ItemStack DeserializeItemStack(GraveSlotSnapshot snapshot)
        {
            if (snapshot?.StackBytes == null || snapshot.StackBytes.Length == 0)
            {
                return null;
            }

            try
            {
                using var ms = new MemoryStream(snapshot.StackBytes, writable: false);
                using var reader = new BinaryReader(ms);
                ItemStack stack = new ItemStack(reader, api.World);
                return stack?.Collectible == null ? null : stack;
            }
            catch
            {
                return null;
            }
        }

        private static IInventory ResolveBackpackInventory(IServerPlayer player)
        {
            if (player?.InventoryManager == null)
            {
                return null;
            }

            IInventory backpack = player.InventoryManager.GetOwnInventory("backpack");
            if (backpack != null)
            {
                return backpack;
            }

            foreach (InventoryBase inventory in player.InventoryManager.InventoriesOrdered)
            {
                if (inventory?.ClassName != null && inventory.ClassName.IndexOf("backpack", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return inventory;
                }
            }

            return null;
        }

        private static IInventory ResolveInventory(IServerPlayer player, GraveInventorySnapshot snapshot)
        {
            if (player?.InventoryManager == null || snapshot == null)
            {
                return null;
            }

            if (string.Equals(snapshot.InventoryClassName, "hotbar", StringComparison.OrdinalIgnoreCase))
            {
                return player.InventoryManager.GetHotbarInventory();
            }

            if (string.Equals(snapshot.InventoryClassName, "backpack", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveBackpackInventory(player);
            }

            if (!string.IsNullOrWhiteSpace(snapshot.InventoryId))
            {
                return player.InventoryManager.GetInventory(snapshot.InventoryId);
            }

            return null;
        }
    }
}
