using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public sealed class PlayerLoadoutManager : IPlayerLoadoutManager
    {
        private static readonly FieldInfo CharacterSlotTypeField = typeof(ItemSlotCharacter).GetField("Type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly ICoreServerAPI api;

        public PlayerLoadoutManager(ICoreServerAPI api)
        {
            this.api = api;
        }

        public List<PlayerInventorySnapshot> Snapshot(IServerPlayer player, List<string> debugEntries = null)
        {
            return Snapshot(player, PlayerLoadoutScope.Gravestone, debugEntries);
        }

        public List<PlayerInventorySnapshot> Snapshot(IServerPlayer player, PlayerLoadoutScope scope, List<string> debugEntries = null)
        {
            var snapshots = new List<PlayerInventorySnapshot>();
            var seenInventoryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSlots = new HashSet<ItemSlot>(ReferenceEqualityComparer.Instance);
            var seenStacks = new HashSet<ItemStack>(ReferenceEqualityComparer.Instance);
            var seenPhysicalSlotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSavedSlotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((IInventory inventory, string inventoryClassName) in ResolveTrackedInventories(player))
            {
                if (inventory == null)
                {
                    continue;
                }

                string key = !string.IsNullOrWhiteSpace(inventory.InventoryID)
                    ? inventory.InventoryID
                    : (inventory.ClassName ?? inventoryClassName ?? string.Empty);

                if (!seenInventoryKeys.Add(key))
                {
                    debugEntries?.Add($"SKIP duplicate inventory view key={key} invKind={inventoryClassName}");
                    continue;
                }

                CaptureInventorySnapshot(
                    inventory,
                    inventoryClassName,
                    scope,
                    snapshots,
                    seenSlots,
                    seenStacks,
                    seenPhysicalSlotKeys,
                    seenSavedSlotKeys,
                    debugEntries);
            }

            return snapshots;
        }

        public void Clear(IServerPlayer player, IReadOnlyCollection<PlayerInventorySnapshot> snapshots)
        {
            if (player?.InventoryManager == null || snapshots == null || snapshots.Count == 0)
            {
                return;
            }

            foreach (PlayerInventorySnapshot inventorySnapshot in snapshots)
            {
                IInventory inventory = ResolveInventory(player, inventorySnapshot);
                if (inventory == null)
                {
                    continue;
                }

                foreach (PlayerInventorySlotSnapshot slotSnapshot in inventorySnapshot.Slots ?? Enumerable.Empty<PlayerInventorySlotSnapshot>())
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
            player.BroadcastPlayerData();
        }

        public bool HasAnyItems(IServerPlayer player)
        {
            foreach ((IInventory inventory, string inventoryClassName) in ResolveTrackedInventories(player))
            {
                if (inventory == null)
                {
                    continue;
                }

                for (int slotIndex = 0; slotIndex < inventory.Count; slotIndex++)
                {
                    ItemSlot slot = inventory[slotIndex];
                    if (slot?.Itemstack != null && !slot.Empty && ShouldTrackSlot(inventoryClassName, slot, PlayerLoadoutScope.Gravestone))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public int DuplicateToPlayer(IReadOnlyCollection<PlayerInventorySnapshot> snapshots, IServerPlayer targetPlayer, BlockPos fallbackPos)
        {
            if (snapshots == null || targetPlayer == null)
            {
                return 0;
            }

            int stackCount = 0;
            foreach (PlayerInventorySnapshot inventory in snapshots)
            {
                foreach (PlayerInventorySlotSnapshot slot in inventory?.Slots ?? Enumerable.Empty<PlayerInventorySlotSnapshot>())
                {
                    ItemStack stack = DeserializeItemStack(slot);
                    if (stack == null || stack.StackSize <= 0)
                    {
                        continue;
                    }

                    if (TransferStackToPlayerOrWorld(targetPlayer, stack, fallbackPos))
                    {
                        stackCount++;
                    }
                }
            }

            return stackCount;
        }

        public bool TryRestore(IReadOnlyCollection<PlayerInventorySnapshot> snapshots, IServerPlayer targetPlayer, BlockPos fallbackPos, out int transferredStacks, out int failedStacks)
        {
            transferredStacks = 0;
            failedStacks = 0;

            if (snapshots == null || targetPlayer == null)
            {
                return false;
            }

            try
            {
                foreach (PlayerInventorySnapshot inventory in snapshots)
                {
                    foreach (PlayerInventorySlotSnapshot slot in inventory?.Slots ?? Enumerable.Empty<PlayerInventorySlotSnapshot>())
                    {
                        try
                        {
                            if (RestoreToOriginalSlotOrFallback(targetPlayer, inventory, slot, fallbackPos))
                            {
                                transferredStacks++;
                            }
                            else
                            {
                                failedStacks++;
                            }
                        }
                        catch (Exception exception)
                        {
                            failedStacks++;
                            api.Logger.Error($"[FirstStepsTweaks] Failed to restore one player loadout stack: {exception}");
                        }
                    }
                }

                targetPlayer.InventoryManager.BroadcastHotbarSlot();
                targetPlayer.BroadcastPlayerData();
                return true;
            }
            catch (Exception exception)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to restore player loadout: {exception}");
                return false;
            }
        }

        private static void CaptureInventorySnapshot(
            IInventory inventory,
            string inventoryClassName,
            PlayerLoadoutScope scope,
            List<PlayerInventorySnapshot> target,
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

            var snapshot = new PlayerInventorySnapshot
            {
                InventoryClassName = inventoryClassName ?? string.Empty,
                InventoryId = inventory.InventoryID,
                Slots = new List<PlayerInventorySlotSnapshot>()
            };

            var seenSlotIds = new HashSet<int>();
            for (int slotId = 0; slotId < inventory.Count; slotId++)
            {
                ItemSlot slot = inventory[slotId];
                if (slot == null || slot.Empty || slot.Itemstack == null)
                {
                    continue;
                }

                if (!ShouldTrackSlot(inventoryClassName, slot, scope))
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
                seenSavedSlotKeys?.Add(savedSlotKey);

                snapshot.Slots.Add(new PlayerInventorySlotSnapshot
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

        private static string BuildSavedSlotDebugKey(PlayerInventorySnapshot inventorySnapshot, int slotId)
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

        private bool RestoreToOriginalSlotOrFallback(
            IServerPlayer targetPlayer,
            PlayerInventorySnapshot inventorySnapshot,
            PlayerInventorySlotSnapshot slotSnapshot,
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
                api.Logger.Warning("[FirstStepsTweaks] Skipping unresolved item stack during loadout restore.");
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
            catch (Exception exception)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to insert stack into inventory, dropping instead: {exception}");
            }

            try
            {
                Vec3d dropPos = targetPlayer.Entity?.Pos?.XYZ ?? fallbackPos?.ToVec3d()?.Add(0.5, 0.5, 0.5);
                if (dropPos == null)
                {
                    return false;
                }

                api.World.SpawnItemEntity(stack, dropPos);
                return true;
            }
            catch (Exception exception)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to drop unresolved stack from loadout restore: {exception}");
                return false;
            }
        }

        private ItemStack DeserializeItemStack(PlayerInventorySlotSnapshot snapshot)
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

        private IEnumerable<(IInventory Inventory, string InventoryClassName)> ResolveTrackedInventories(IServerPlayer player)
        {
            if (player?.InventoryManager == null)
            {
                yield break;
            }

            yield return (player.InventoryManager.GetHotbarInventory(), "hotbar");
            yield return (ResolveOwnInventory(player, "backpack"), "backpack");
            yield return (ResolveOwnInventory(player, "character"), "character");
        }

        private static IInventory ResolveOwnInventory(IServerPlayer player, string inventoryClassName)
        {
            if (player?.InventoryManager == null || string.IsNullOrWhiteSpace(inventoryClassName))
            {
                return null;
            }

            IInventory directInventory = player.InventoryManager.GetOwnInventory(inventoryClassName);
            if (directInventory != null)
            {
                return directInventory;
            }

            foreach (InventoryBase inventory in player.InventoryManager.InventoriesOrdered)
            {
                if (inventory?.ClassName != null
                    && inventory.ClassName.IndexOf(inventoryClassName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return inventory;
                }
            }

            return null;
        }

        private static IInventory ResolveInventory(IServerPlayer player, PlayerInventorySnapshot snapshot)
        {
            if (player?.InventoryManager == null || snapshot == null)
            {
                return null;
            }

            if (string.Equals(snapshot.InventoryClassName, "hotbar", StringComparison.OrdinalIgnoreCase))
            {
                return player.InventoryManager.GetHotbarInventory();
            }

            if (string.Equals(snapshot.InventoryClassName, "backpack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(snapshot.InventoryClassName, "character", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveOwnInventory(player, snapshot.InventoryClassName);
            }

            if (!string.IsNullOrWhiteSpace(snapshot.InventoryId))
            {
                return player.InventoryManager.GetInventory(snapshot.InventoryId);
            }

            return null;
        }

        internal static bool ShouldTrackSlot(string inventoryClassName, ItemSlot slot, PlayerLoadoutScope scope)
        {
            if (scope == PlayerLoadoutScope.AdminModeInitialSeed)
            {
                return string.Equals(inventoryClassName, "character", StringComparison.OrdinalIgnoreCase)
                    && IsClothingCharacterSlot(slot);
            }

            if (!string.Equals(inventoryClassName, "character", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return scope == PlayerLoadoutScope.AdminMode || IsArmorCharacterSlot(slot);
        }

        private static bool IsClothingCharacterSlot(ItemSlot slot)
        {
            if (slot is not ItemSlotCharacter || CharacterSlotTypeField == null)
            {
                return false;
            }

            if (CharacterSlotTypeField.GetValue(slot) is not EnumCharacterDressType dressType)
            {
                return false;
            }

            return dressType != EnumCharacterDressType.ArmorHead
                && dressType != EnumCharacterDressType.ArmorBody
                && dressType != EnumCharacterDressType.ArmorLegs;
        }

        private static bool IsArmorCharacterSlot(ItemSlot slot)
        {
            if (slot is not ItemSlotCharacter || CharacterSlotTypeField == null)
            {
                return false;
            }

            if (CharacterSlotTypeField.GetValue(slot) is not EnumCharacterDressType dressType)
            {
                return false;
            }

            return dressType == EnumCharacterDressType.ArmorHead
                || dressType == EnumCharacterDressType.ArmorBody
                || dressType == EnumCharacterDressType.ArmorLegs;
        }
    }
}
