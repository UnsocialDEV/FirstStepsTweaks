using System;
using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public sealed class GraveInventorySnapshotter : IGraveSnapshotter
    {
        public List<GraveInventorySnapshot> SnapshotRelevantInventories(IServerPlayer player, List<string> debugEntries = null)
        {
            var snapshots = new List<GraveInventorySnapshot>();
            var seenInventoryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSlots = new HashSet<ItemSlot>(ReferenceEqualityComparer.Instance);
            var seenStacks = new HashSet<ItemStack>(ReferenceEqualityComparer.Instance);
            var seenPhysicalSlotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSavedSlotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void capture(IInventory inventory, string fallbackClassName)
            {
                if (inventory == null)
                {
                    return;
                }

                string key = !string.IsNullOrWhiteSpace(inventory.InventoryID)
                    ? inventory.InventoryID
                    : (inventory.ClassName ?? fallbackClassName ?? string.Empty);

                if (!seenInventoryKeys.Add(key))
                {
                    debugEntries?.Add($"SKIP duplicate inventory view key={key} invKind={fallbackClassName}");
                    return;
                }

                CaptureInventorySnapshot(inventory, fallbackClassName, snapshots, seenSlots, seenStacks, seenPhysicalSlotKeys, seenSavedSlotKeys, debugEntries);
            }

            capture(player.InventoryManager.GetHotbarInventory(), "hotbar");
            capture(ResolveBackpackInventory(player), "backpack");
            return snapshots;
        }

        public void RemoveSnapshottedItems(IServerPlayer player, List<GraveInventorySnapshot> snapshots, bool debugTrace = false)
        {
            if (player?.InventoryManager == null || snapshots == null || snapshots.Count == 0)
            {
                return;
            }

            foreach (GraveInventorySnapshot inventorySnapshot in snapshots)
            {
                IInventory inventory = ResolveInventory(player, inventorySnapshot);
                if (inventory == null)
                {
                    continue;
                }

                foreach (GraveSlotSnapshot slotSnapshot in inventorySnapshot.Slots ?? Enumerable.Empty<GraveSlotSnapshot>())
                {
                    if (slotSnapshot == null || slotSnapshot.SlotId < 0 || slotSnapshot.SlotId >= inventory.Count)
                    {
                        continue;
                    }

                    ItemSlot slot = inventory[slotSnapshot.SlotId];
                    if (slot == null || slot.Empty || slot.Itemstack == null)
                    {
                        continue;
                    }

                    slot.Itemstack = null;
                    slot.MarkDirty();
                }
            }

            player.InventoryManager.BroadcastHotbarSlot();
        }

        private static void CaptureInventorySnapshot(
            IInventory inventory,
            string fallbackClassName,
            List<GraveInventorySnapshot> target,
            HashSet<ItemSlot> seenSlots,
            HashSet<ItemStack> seenStacks,
            HashSet<string> seenPhysicalSlotKeys,
            HashSet<string> seenSavedSlotKeys,
            List<string> debugEntries)
        {
            if (inventory == null || target == null)
            {
                return;
            }

            var snapshot = new GraveInventorySnapshot
            {
                InventoryClassName = fallbackClassName ?? string.Empty,
                InventoryId = inventory.InventoryID,
                Slots = new List<GraveSlotSnapshot>()
            };

            var seenSlotIds = new HashSet<int>();
            for (int slotId = 0; slotId < inventory.Count; slotId++)
            {
                ItemSlot slot = inventory[slotId];
                if (slot == null || slot.Empty || slot.Itemstack == null)
                {
                    continue;
                }

                if (!seenSlotIds.Add(slotId))
                {
                    continue;
                }

                if (seenSlots != null && !seenSlots.Add(slot))
                {
                    continue;
                }

                if (seenStacks != null && !seenStacks.Add(slot.Itemstack))
                {
                    continue;
                }

                string physicalSlotKey = BuildPhysicalSlotKey(slot);
                if (!string.IsNullOrWhiteSpace(physicalSlotKey)
                    && seenPhysicalSlotKeys != null
                    && !seenPhysicalSlotKeys.Add(physicalSlotKey))
                {
                    continue;
                }

                byte[] stackBytes = slot.Itemstack.Clone()?.ToBytes();
                if (stackBytes == null || stackBytes.Length == 0)
                {
                    continue;
                }

                string savedSlotKey = BuildSavedSlotDebugKey(snapshot, slotId);
                if (seenSavedSlotKeys != null)
                {
                    seenSavedSlotKeys.Add(savedSlotKey);
                }

                snapshot.Slots.Add(new GraveSlotSnapshot
                {
                    SlotId = slotId,
                    StackBytes = stackBytes
                });

                if (debugEntries != null)
                {
                    string code = slot.Itemstack.Collectible?.Code?.ToString() ?? "unknown";
                    debugEntries.Add($"CAPTURE key={savedSlotKey} invKind={snapshot.InventoryClassName} invId={snapshot.InventoryId} slot={slotId} phys={physicalSlotKey ?? "n/a"} stack={code} size={slot.Itemstack.StackSize}");
                }
            }

            if (snapshot.Slots.Count > 0)
            {
                target.Add(snapshot);
            }
        }

        private static string BuildSavedSlotDebugKey(GraveInventorySnapshot inventorySnapshot, int slotId)
        {
            string inventoryClass = inventorySnapshot?.InventoryClassName ?? "unknown";
            string inventoryId = inventorySnapshot?.InventoryId ?? "no-inv-id";
            return $"{inventoryClass}|{inventoryId}|{slotId}";
        }

        private static string BuildPhysicalSlotKey(ItemSlot slot)
        {
            InventoryBase inventory = slot?.Inventory;
            if (inventory == null || string.IsNullOrWhiteSpace(inventory.InventoryID))
            {
                return null;
            }

            int slotId = inventory.GetSlotId(slot);
            return slotId < 0 ? null : $"{inventory.InventoryID}:{slotId}";
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
