using FirstStepsTweaks.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public class GravestoneService
    {
        private readonly ICoreServerAPI api;
        private readonly CorpseConfig config;
        private readonly GraveManager graveManager;
        private readonly object claimLock = new object();
        private readonly HashSet<string> claimInProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);


        private Block cachedGraveBlock;

        public GravestoneService(ICoreServerAPI api, FirstStepsTweaksConfig rootConfig)
        {
            this.api = api;
            config = rootConfig?.Corpse ?? new CorpseConfig();
            graveManager = new GraveManager(api);

            api.Event.OnEntityDeath += OnEntityDeath;
            api.Event.BreakBlock += OnBreakBlock;
            api.Event.DidBreakBlock += OnDidBreakBlock;
            api.Event.DidUseBlock += OnDidUseBlock;
            api.Event.GameWorldSave += OnGameWorldSave;
            api.Event.SaveGameLoaded += ReconcilePersistedGraves;
            api.Event.RegisterGameTickListener(_ => CleanupAndReconcile(), Math.Max(10000, config.GraveCleanupTickMs));
        }

        public string GraveBlockCode => config.GraveBlockCode;

        public List<GraveData> GetActiveGraves()
        {
            return graveManager.GetAll();
        }

        public bool IsPubliclyClaimable(GraveData grave)
        {
            if (grave == null)
            {
                return false;
            }

            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= grave.ProtectionEndsUnixMs;
        }

        public bool TryDuplicateGraveItemsToPlayer(string graveId, IServerPlayer targetPlayer, out string message)
        {
            message = string.Empty;

            if (targetPlayer == null)
            {
                message = "Target player is not online.";
                return false;
            }

            if (!graveManager.TryGetById(graveId, out GraveData grave))
            {
                message = $"Gravestone '{graveId}' was not found.";
                return false;
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

            message = stackCount > 0
                ? $"Duplicated {stackCount} stack(s) from gravestone '{graveId}' to {targetPlayer.PlayerName}."
                : $"Gravestone '{graveId}' had no item stacks to duplicate.";

            return true;
        }

        public bool TryAdminRestoreGraveToPlayer(string graveId, IServerPlayer targetPlayer, out string message)
        {
            return TryRestoreGrave(graveId, targetPlayer, bypassProtection: true, removeBlock: true, out message);
        }

        public bool TryRemoveGrave(string graveId, out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(graveId))
            {
                message = "Invalid gravestone id.";
                return false;
            }

            if (!TryBeginClaim(graveId))
            {
                message = $"Gravestone '{graveId}' is currently being processed.";
                return false;
            }

            try
            {
                if (!graveManager.Remove(graveId, out GraveData removedGrave) || removedGrave == null)
                {
                    message = $"Gravestone '{graveId}' was not found.";
                    return false;
                }

                RemoveGraveBlockIfPresent(removedGrave);
                message = $"Removed gravestone '{graveId}'.";
                return true;
            }
            finally
            {
                EndClaim(graveId);
            }
        }

        public bool TryResolveTargetedGraveId(IServerPlayer player, out string graveId, out string message)
        {
            graveId = string.Empty;
            message = string.Empty;

            if (player == null)
            {
                message = "Only players can resolve a gravestone from what they are looking at.";
                return false;
            }

            BlockSelection blockSelection = player.CurrentBlockSelection;
            if (blockSelection?.Position == null)
            {
                message = "Look directly at a valid gravestone or specify the grave ID.";
                return false;
            }

            if (!graveManager.TryGetByPosition(blockSelection.Position, out GraveData grave) || grave == null)
            {
                message = "The block you are looking at is not a tracked gravestone. Specify the grave ID instead.";
                return false;
            }

            graveId = grave.GraveId;
            return true;
        }

        public bool TryGetTeleportTarget(string graveId, out GraveData grave, out Vec3d target, out string message)
        {
            grave = null;
            target = null;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(graveId))
            {
                message = "Invalid gravestone id.";
                return false;
            }

            if (!graveManager.TryGetById(graveId, out grave) || grave == null)
            {
                message = $"Gravestone '{graveId}' was not found.";
                return false;
            }

            target = FindSafeTeleportTarget(grave);
            if (target == null)
            {
                message = $"Unable to find a safe teleport destination for gravestone '{graveId}'.";
                return false;
            }

            return true;
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (!(entity is EntityPlayer entityPlayer))
            {
                return;
            }

            IServerPlayer player = entityPlayer.Player as IServerPlayer;
            if (player?.InventoryManager == null || player.Entity?.Pos == null)
            {
                return;
            }

            Block graveBlock = ResolveGraveBlock();
            if (graveBlock == null)
            {
                return;
            }

            bool debugCaptureRequested = IsDebugTracePending();
            List<string> debugCaptureLines = debugCaptureRequested ? new List<string>() : null;

            List<GraveInventorySnapshot> snapshots = SnapshotRelevantInventories(player, debugCaptureLines);
            int capturedStacks = snapshots.Sum(snapshot => snapshot?.Slots?.Count ?? 0);

            if (capturedStacks <= 0)
            {
                return;
            }

            string graveId = Guid.NewGuid().ToString("N");
            bool debugCycleActive = TryStartDebugTraceCycle(graveId);

            BlockPos deathPos = player.Entity.Pos.AsBlockPos.Copy();
            GravePlacementResult placement = FindPlacementPosition(player, deathPos, graveBlock);
            BlockPos gravePos = placement.Position;
            long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var grave = new GraveData
            {
                GraveId = graveId,
                OwnerUid = player.PlayerUID,
                OwnerName = player.PlayerName,
                X = gravePos.X,
                Y = gravePos.Y,
                Z = gravePos.Z,
                Dimension = gravePos.dimension,
                CreatedUnixMs = nowUnixMs,
                ProtectionEndsUnixMs = nowUnixMs + Math.Max(60000L, config.GraveExpireMs),
                CreatedTotalDays = api.World.Calendar.TotalDays,
                Inventories = snapshots
            };

            if (!graveManager.Upsert(grave))
            {
                return;
            }

            if (debugCycleActive)
            {
                debugCaptureLines ??= new List<string>();
                debugCaptureLines.Insert(0, $"owner={player.PlayerName} ({player.PlayerUID}), capturedStacks={capturedStacks}, snapshotGroups={snapshots.Count}, deathPos={deathPos}, gravePos={gravePos}");
                LogDebugLines(graveId, "capture", debugCaptureLines);
            }

            RemoveSnapshottedItems(player, snapshots, graveId, debugCycleActive);
            EnsureGraveBlock(grave);

            if (placement.MovedOutsideForeignClaim)
            {
                string alertMessage = "You died inside a land claim you do not own, so your gravestone was placed outside the claim "
                    + $"at {gravePos.X}, {gravePos.Y}, {gravePos.Z}.";
                player.SendMessage(GlobalConstants.InfoLogChatGroup, alertMessage, EnumChatType.CommandSuccess);
                player.SendMessage(GlobalConstants.GeneralChatGroup, alertMessage, EnumChatType.Notification);
            }

            string message = "Your items were stored in a gravestone, they will be protected for 60 minutes";
            player.SendMessage(GlobalConstants.InfoLogChatGroup, message, EnumChatType.CommandSuccess);
            player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }

        private void OnBreakBlock(IServerPlayer byPlayer, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (blockSel?.Position == null)
            {
                return;
            }

            if (!graveManager.TryGetByPosition(blockSel.Position, out GraveData grave) || grave == null)
            {
                return;
            }

            if (byPlayer == null)
            {
                handling = EnumHandling.PreventDefault;
                dropQuantityMultiplier = 0f;
                return;
            }

            if (!CanPlayerClaim(byPlayer, grave, out string denialMessage))
            {
                byPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, denialMessage, EnumChatType.CommandSuccess);
                byPlayer.SendMessage(GlobalConstants.GeneralChatGroup, denialMessage, EnumChatType.Notification);
                handling = EnumHandling.PreventDefault;
                dropQuantityMultiplier = 0f;
            }
        }

        private void OnDidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (blockSel?.Position == null)
            {
                return;
            }

            Block graveBlock = ResolveGraveBlock();
            if (graveBlock == null || oldblockId != graveBlock.Id)
            {
                return;
            }

            if (!graveManager.TryGetByPosition(blockSel.Position, out GraveData grave) || grave == null)
            {
                return;
            }

            if (byPlayer == null)
            {
                EnsureGraveBlock(grave);
                return;
            }

            // BreakBlock already handles protected-owner denial messaging; do not send it again here.
            if (!CanPlayerClaim(byPlayer, grave, out _))
            {
                EnsureGraveBlock(grave);
                return;
            }

            if (!TryRestoreGrave(grave.GraveId, byPlayer, bypassProtection: false, removeBlock: true, out string resultMessage))
            {
                EnsureGraveBlock(grave);
                byPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, resultMessage, EnumChatType.CommandSuccess);
                byPlayer.SendMessage(GlobalConstants.GeneralChatGroup, resultMessage, EnumChatType.Notification);
                return;
            }

            byPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, resultMessage, EnumChatType.CommandSuccess);
            byPlayer.SendMessage(GlobalConstants.GeneralChatGroup, resultMessage, EnumChatType.Notification);
        }

        private void OnDidUseBlock(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer == null || blockSel?.Position == null)
            {
                return;
            }

            if (!graveManager.TryGetByPosition(blockSel.Position, out GraveData grave) || grave == null)
            {
                return;
            }

            if (CanPlayerClaim(byPlayer, grave, out string denialMessage))
            {
                string claimMessage = IsPubliclyClaimable(grave)
                    ? "This gravestone can be claimed by anyone. Break it to restore its items."
                    : "This gravestone is owner-protected. You can break it to restore the items.";

                byPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, claimMessage, EnumChatType.CommandSuccess);
                return;
            }

            byPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, denialMessage, EnumChatType.CommandSuccess);
        }

        private void OnGameWorldSave()
        {
            graveManager.Save();
        }

        private bool TryRestoreGrave(string graveId, IServerPlayer targetPlayer, bool bypassProtection, bool removeBlock, out string message)
        {
            message = string.Empty;

            if (targetPlayer == null)
            {
                message = "Target player is not online.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(graveId))
            {
                message = "Invalid gravestone id.";
                return false;
            }

            if (!TryBeginClaim(graveId))
            {
                message = $"Gravestone '{graveId}' is currently being processed.";
                return false;
            }

            bool debugRestore = IsDebugTraceActive(graveId);
            if (debugRestore)
            {
            }

            try
            {
                if (!graveManager.TryGetById(graveId, out GraveData grave) || grave == null)
                {
                    message = $"Gravestone '{graveId}' was not found.";
                    return false;
                }

                if (!bypassProtection && !CanPlayerClaim(targetPlayer, grave, out message))
                {
                    return false;
                }

                int transferredStacks = 0;
                int failedStacks = 0;
                BlockPos gravePos = grave.ToBlockPos();
                grave.Inventories ??= new List<GraveInventorySnapshot>();
                var seenRestoreSlotKeys = debugRestore
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : null;

                for (int inventoryIndex = 0; inventoryIndex < grave.Inventories.Count;)
                {
                    GraveInventorySnapshot inventory = grave.Inventories[inventoryIndex];
                    if (inventory?.Slots == null || inventory.Slots.Count == 0)
                    {
                        grave.Inventories.RemoveAt(inventoryIndex);
                        graveManager.Upsert(grave);
                        continue;
                    }

                    while (inventory.Slots.Count > 0)
                    {
                        GraveSlotSnapshot slot = inventory.Slots[0];
                        string savedSlotKey = BuildSavedSlotDebugKey(inventory, slot?.SlotId ?? -1);
                        string restoreTrace = string.Empty;

                        try
                        {
                            if (debugRestore
                                && seenRestoreSlotKeys != null
                                && !seenRestoreSlotKeys.Add(savedSlotKey))
                            {
                            }

                            if (RestoreToOriginalSlotOrFallback(targetPlayer, inventory, slot, savedSlotKey, gravePos, out restoreTrace))
                            {
                                transferredStacks++;
                            }
                            else
                            {
                                failedStacks++;
                            }

                            if (debugRestore && !string.IsNullOrWhiteSpace(restoreTrace))
                            {
                            }
                        }
                        catch (Exception ex)
                        {
                            failedStacks++;
                            api.Logger.Error($"[FirstStepsTweaks] Failed to restore one stack from gravestone '{graveId}': {ex}");
                        }
                        finally
                        {
                            // Always consume this stored slot so one bad item cannot block full grave recovery.
                            inventory.Slots.RemoveAt(0);
                            graveManager.Upsert(grave);
                        }
                    }

                    grave.Inventories.RemoveAt(inventoryIndex);
                    graveManager.Upsert(grave);
                }

                graveManager.Remove(graveId, out GraveData removedGrave);

                if (removeBlock)
                {
                    RemoveGraveBlockIfPresent(removedGrave ?? grave);
                }

                targetPlayer.InventoryManager.BroadcastHotbarSlot();
                message = "You recovered your items";

                if (debugRestore)
                {
                }

                return true;
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to restore gravestone '{graveId}': {ex}");

                if (graveManager.TryGetById(graveId, out GraveData grave))
                {
                    EnsureGraveBlock(grave);
                }

                message = $"Failed to restore gravestone '{graveId}'. It remains in storage for safety.";
                return false;
            }
            finally
            {
                if (debugRestore)
                {
                    CompleteDebugTraceCycle(graveId);
                }

                EndClaim(graveId);
            }
        }
        private List<GraveInventorySnapshot> SnapshotRelevantInventories(IServerPlayer player, List<string> debugEntries = null)
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
                    if (debugEntries != null)
                    {
                        debugEntries.Add($"SKIP duplicate inventory view key={key} invKind={fallbackClassName}");
                    }

                    return;
                }

                CaptureInventorySnapshot(
                    inventory,
                    fallbackClassName,
                    snapshots,
                    seenSlots,
                    seenStacks,
                    seenPhysicalSlotKeys,
                    seenSavedSlotKeys,
                    debugEntries
                );
            }

            capture(player.InventoryManager.GetHotbarInventory(), "hotbar");
            capture(ResolveBackpackInventory(player), "backpack");

            return snapshots;
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

            int captured = 0;
            int skippedEmpty = 0;
            int skippedSlotIdDup = 0;
            int skippedSlotRefDup = 0;
            int skippedStackRefDup = 0;
            int skippedPhysicalDup = 0;
            int skippedNoBytes = 0;
            int duplicateSavedKeyHits = 0;

            var seenSlotIds = new HashSet<int>();
            for (int slotId = 0; slotId < inventory.Count; slotId++)
            {
                ItemSlot slot = inventory[slotId];
                if (slot == null || slot.Empty || slot.Itemstack == null)
                {
                    skippedEmpty++;
                    continue;
                }

                if (!seenSlotIds.Add(slotId))
                {
                    skippedSlotIdDup++;
                    continue;
                }

                if (seenSlots != null && !seenSlots.Add(slot))
                {
                    skippedSlotRefDup++;
                    continue;
                }

                if (seenStacks != null && !seenStacks.Add(slot.Itemstack))
                {
                    skippedStackRefDup++;
                    continue;
                }

                string physicalSlotKey = BuildPhysicalSlotKey(slot);
                if (!string.IsNullOrWhiteSpace(physicalSlotKey)
                    && seenPhysicalSlotKeys != null
                    && !seenPhysicalSlotKeys.Add(physicalSlotKey))
                {
                    skippedPhysicalDup++;
                    continue;
                }

                byte[] stackBytes = slot.Itemstack.Clone()?.ToBytes();
                if (stackBytes == null || stackBytes.Length == 0)
                {
                    skippedNoBytes++;
                    continue;
                }

                string savedSlotKey = BuildSavedSlotDebugKey(snapshot, slotId);
                bool duplicateSavedKey = seenSavedSlotKeys != null && !seenSavedSlotKeys.Add(savedSlotKey);
                if (duplicateSavedKey)
                {
                    duplicateSavedKeyHits++;
                }

                snapshot.Slots.Add(new GraveSlotSnapshot
                {
                    SlotId = slotId,
                    StackBytes = stackBytes
                });

                captured++;

                if (debugEntries != null)
                {
                    string code = slot.Itemstack.Collectible?.Code?.ToString() ?? "unknown";
                    debugEntries.Add($"CAPTURE key={savedSlotKey} duplicateKey={duplicateSavedKey} invKind={snapshot.InventoryClassName} invId={snapshot.InventoryId} slot={slotId} phys={physicalSlotKey ?? "n/a"} stack={code} size={slot.Itemstack.StackSize}");
                }
            }

            if (snapshot.Slots.Count > 0)
            {
                target.Add(snapshot);
            }

            if (debugEntries != null)
            {
                debugEntries.Add(
                    $"CAPTURE_SUMMARY invKind={snapshot.InventoryClassName} invId={snapshot.InventoryId} captured={captured} duplicateSavedKeyHits={duplicateSavedKeyHits} skippedEmpty={skippedEmpty} skippedSlotIdDup={skippedSlotIdDup} skippedSlotRefDup={skippedSlotRefDup} skippedStackRefDup={skippedStackRefDup} skippedPhysicalDup={skippedPhysicalDup} skippedNoBytes={skippedNoBytes}"
                );
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
            if (slotId < 0)
            {
                return null;
            }

            return $"{inventory.InventoryID}:{slotId}";
        }
        private void RemoveSnapshottedItems(IServerPlayer player, List<GraveInventorySnapshot> snapshots, string graveId = null, bool debugTrace = false)
        {
            if (player?.InventoryManager == null || snapshots == null || snapshots.Count == 0)
            {
                return;
            }

            int removedCount = 0;
            int unresolvedCount = 0;

            foreach (GraveInventorySnapshot inventorySnapshot in snapshots)
            {
                IInventory inventory = ResolveInventory(player, inventorySnapshot);
                if (inventory == null)
                {
                    unresolvedCount += inventorySnapshot?.Slots?.Count ?? 0;

                    if (debugTrace)
                    {
                    }

                    continue;
                }

                foreach (GraveSlotSnapshot slotSnapshot in inventorySnapshot.Slots ?? Enumerable.Empty<GraveSlotSnapshot>())
                {
                    if (slotSnapshot == null || slotSnapshot.SlotId < 0 || slotSnapshot.SlotId >= inventory.Count)
                    {
                        unresolvedCount++;

                        if (debugTrace)
                        {
                        }

                        continue;
                    }

                    ItemSlot slot = inventory[slotSnapshot.SlotId];
                    if (slot == null)
                    {
                        unresolvedCount++;

                        if (debugTrace)
                        {
                        }

                        continue;
                    }

                    if (slot.Empty || slot.Itemstack == null)
                    {
                        if (debugTrace)
                        {
                        }

                        continue;
                    }

                    string beforeCode = slot.Itemstack.Collectible?.Code?.ToString() ?? "unknown";
                    int beforeSize = slot.Itemstack.StackSize;

                    // Force clear to avoid slot-level take restrictions during death processing.
                    slot.Itemstack = null;
                    slot.MarkDirty();

                    bool cleared = slot.Empty;
                    if (cleared)
                    {
                        removedCount++;
                    }
                    else
                    {
                        unresolvedCount++;
                    }

                    if (debugTrace)
                    {
                    }
                }
            }

            player.InventoryManager.BroadcastHotbarSlot();

            if (debugTrace)
            {
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
                if (inventory?.ClassName == null)
                {
                    continue;
                }

                if (inventory.ClassName.IndexOf("backpack", StringComparison.OrdinalIgnoreCase) >= 0)
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
                IInventory byId = player.InventoryManager.GetInventory(snapshot.InventoryId);
                if (byId != null)
                {
                    return byId;
                }
            }

            // Do not fallback to arbitrary class names; that can map backpack snapshots into hotbar slots.
            return null;
        }

        private bool IsDebugTracePending()
        {
            return false;
        }

        private bool TryStartDebugTraceCycle(string graveId)
        {
            return false;
        }

        private bool IsDebugTraceActive(string graveId)
        {
            return false;
        }

        private void CompleteDebugTraceCycle(string graveId)
        {
        }

        private void LogDebugLines(string graveId, string phase, IEnumerable<string> lines)
        {
        }

        private bool CanPlayerClaim(IServerPlayer player, GraveData grave, out string denialMessage)
        {
            denialMessage = string.Empty;

            if (player == null || grave == null)
            {
                denialMessage = "Gravestone interaction is not available right now.";
                return false;
            }

            if (string.Equals(player.PlayerUID, grave.OwnerUid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now >= grave.ProtectionEndsUnixMs)
            {
                return true;
            }

            long remainingMs = grave.ProtectionEndsUnixMs - now;
            int remainingMinutes = Math.Max(1, (int)Math.Ceiling(remainingMs / 60000d));
            string ownerName = string.IsNullOrWhiteSpace(grave.OwnerName) ? "its owner" : grave.OwnerName;
            denialMessage = $"This gravestone belongs to {ownerName} and is owner-protected for {remainingMinutes} more minute(s).";
            return false;
        }

        private bool TryBeginClaim(string graveId)
        {
            lock (claimLock)
            {
                if (string.IsNullOrWhiteSpace(graveId))
                {
                    return false;
                }

                return claimInProgress.Add(graveId);
            }
        }

        private void EndClaim(string graveId)
        {
            lock (claimLock)
            {
                claimInProgress.Remove(graveId);
            }
        }

        private bool RestoreToOriginalSlotOrFallback(
            IServerPlayer targetPlayer,
            GraveInventorySnapshot inventorySnapshot,
            GraveSlotSnapshot slotSnapshot,
            string savedSlotKey,
            BlockPos fallbackPos,
            out string trace)
        {
            trace = string.Empty;

            ItemStack stack = DeserializeItemStack(slotSnapshot);
            if (stack == null || stack.StackSize <= 0)
            {
                trace = $"SKIP invalid stack bytes key={savedSlotKey} invKind={inventorySnapshot?.InventoryClassName} invId={inventorySnapshot?.InventoryId} slot={slotSnapshot?.SlotId}";
                return false;
            }

            string stackCode = stack.Collectible?.Code?.ToString() ?? "unknown";

            IInventory inventory = ResolveInventory(targetPlayer, inventorySnapshot);
            if (inventory != null && slotSnapshot != null && slotSnapshot.SlotId >= 0 && slotSnapshot.SlotId < inventory.Count)
            {
                ItemSlot targetSlot = inventory[slotSnapshot.SlotId];
                if (targetSlot != null && targetSlot.Empty)
                {
                    targetSlot.Itemstack = stack;
                    targetSlot.MarkDirty();
                    trace = $"RESTORE_DIRECT key={savedSlotKey} invKind={inventorySnapshot.InventoryClassName} invId={inventory.InventoryID} slot={slotSnapshot.SlotId} stack={stackCode} size={stack.StackSize}";
                    return true;
                }

                if (targetSlot?.Itemstack != null && AreStacksEquivalent(targetSlot.Itemstack, stack))
                {
                    trace = $"RESTORE_ALREADY_PRESENT key={savedSlotKey} invKind={inventorySnapshot.InventoryClassName} invId={inventory.InventoryID} slot={slotSnapshot.SlotId} stack={stackCode} size={stack.StackSize}";
                    return true;
                }

                string occupiedBy = targetSlot?.Itemstack?.Collectible?.Code?.ToString() ?? "unknown";
                bool fallbackResult = TransferStackToPlayerOrWorld(targetPlayer, stack, fallbackPos);
                trace = $"RESTORE_FALLBACK_OCCUPIED key={savedSlotKey} invKind={inventorySnapshot.InventoryClassName} invId={inventory.InventoryID} slot={slotSnapshot.SlotId} occupiedBy={occupiedBy} stack={stackCode} size={stack.StackSize} success={fallbackResult}";
                return fallbackResult;
            }

            bool fallback = TransferStackToPlayerOrWorld(targetPlayer, stack, fallbackPos);
            trace = $"RESTORE_FALLBACK_NO_SLOT key={savedSlotKey} invKind={inventorySnapshot?.InventoryClassName} invId={inventorySnapshot?.InventoryId} slot={slotSnapshot?.SlotId} stack={stackCode} size={stack.StackSize} success={fallback}";
            return fallback;
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

            for (int i = 0; i < leftBytes.Length; i++)
            {
                if (leftBytes[i] != rightBytes[i])
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

        private void CleanupAndReconcile()
        {
            if (api?.World?.Calendar == null || api.World.BlockAccessor == null)
            {
                return;
            }

            double nowDays = api.World.Calendar.TotalDays;
            double cleanupAfterDays = Math.Max(1d, config.GraveCleanupInGameDays);

            List<GraveData> graves = graveManager.GetAll();
            foreach (GraveData grave in graves)
            {
                if (grave == null)
                {
                    continue;
                }

                if ((nowDays - grave.CreatedTotalDays) >= cleanupAfterDays)
                {
                    if (graveManager.Remove(grave.GraveId, out GraveData removed) && removed != null)
                    {
                        RemoveGraveBlockIfPresent(removed);
                    }

                    continue;
                }

                EnsureGraveBlock(grave);
            }
        }

        private void ReconcilePersistedGraves()
        {
            CleanupAndReconcile();
        }

        private Block ResolveGraveBlock()
        {
            if (cachedGraveBlock != null)
            {
                return cachedGraveBlock;
            }

            AssetLocation graveCode = new AssetLocation(config.GraveBlockCode ?? "firststepstweaks:gravestone");
            cachedGraveBlock = api.World.GetBlock(graveCode);

            if (cachedGraveBlock == null)
            {
                api.Logger.Error($"[FirstStepsTweaks] Grave block not found: {graveCode}");
            }

            return cachedGraveBlock;
        }

        private GravePlacementResult FindPlacementPosition(IServerPlayer player, BlockPos deathPos, Block graveBlock)
        {
            if (deathPos == null)
            {
                return new GravePlacementResult(new BlockPos(0), false);
            }

            bool diedInForeignClaim = IsForeignClaimAtPosition(player, deathPos);
            BlockPos fallback = deathPos.UpCopy(1);

            int[] xOffsets = { 0, 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] zOffsets = { 0, 0, 0, 1, -1, 1, -1, 1, -1 };

            if (!diedInForeignClaim)
            {
                for (int yOffset = 0; yOffset <= 3; yOffset++)
                {
                    for (int i = 0; i < xOffsets.Length; i++)
                    {
                        BlockPos candidate = deathPos.AddCopy(xOffsets[i], yOffset, zOffsets[i]);
                        if (CanPlaceGraveAt(player, candidate, graveBlock))
                        {
                            return new GravePlacementResult(candidate, false);
                        }
                    }
                }
            }

            const int searchRadius = 64;
            int[] ySearchOffsets = { 0, 1, 2, 3, -1, -2 };

            for (int radius = 1; radius <= searchRadius; radius++)
            {
                foreach (BlockPos edgePos in EnumerateSquareEdge(deathPos, radius))
                {
                    foreach (int yOffset in ySearchOffsets)
                    {
                        BlockPos candidate = edgePos.AddCopy(0, yOffset, 0);
                        if (!CanPlaceGraveAt(player, candidate, graveBlock))
                        {
                            continue;
                        }

                        bool movedOutsideForeignClaim = diedInForeignClaim
                            && !PositionsEqual(candidate, deathPos);

                        return new GravePlacementResult(candidate, movedOutsideForeignClaim);
                    }
                }
            }

            if (diedInForeignClaim)
            {
                for (int verticalOffset = 1; verticalOffset <= 4; verticalOffset++)
                {
                    BlockPos candidate = deathPos.UpCopy(verticalOffset);
                    if (CanPlaceGraveAt(player, candidate, graveBlock))
                    {
                        return new GravePlacementResult(candidate, false);
                    }
                }
            }

            return new GravePlacementResult(fallback, false);
        }

        private bool CanPlaceGraveAt(IServerPlayer player, BlockPos candidate, Block graveBlock)
        {
            if (candidate == null || graveBlock == null)
            {
                return false;
            }

            if (IsForeignClaimAtPosition(player, candidate))
            {
                return false;
            }

            Block existing = api.World.BlockAccessor.GetBlock(candidate);
            return existing == null || existing.IsReplacableBy(graveBlock);
        }

        private bool IsForeignClaimAtPosition(IServerPlayer player, BlockPos pos)
        {
            LandClaimInfo claim = GetClaimAtPosition(pos);
            if (!claim.Exists)
            {
                return false;
            }

            return player == null
                || string.IsNullOrWhiteSpace(claim.OwnerUid)
                || !string.Equals(claim.OwnerUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase);
        }

        private LandClaimInfo GetClaimAtPosition(BlockPos pos)
        {
            if (pos == null)
            {
                return LandClaimInfo.None;
            }

            object claimsApi = api.World?.GetType().GetProperty("Claims", BindingFlags.Instance | BindingFlags.Public)?.GetValue(api.World);
            if (claimsApi == null)
            {
                return LandClaimInfo.None;
            }

            object claim = ResolveClaim(claimsApi, pos);
            return claim == null ? LandClaimInfo.None : LandClaimInfo.FromClaim(claim);
        }

        private static object ResolveClaim(object claimsApi, BlockPos pos)
        {
            MethodInfo[] methods = claimsApi.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

            foreach (string methodName in new[] { "Get", "GetClaimsAt", "GetAt", "GetCurrentClaims" })
            {
                MethodInfo method = methods.FirstOrDefault(m =>
                    m.Name == methodName
                    && m.GetParameters().Length == 1
                    && IsPositionParameter(m.GetParameters()[0].ParameterType));

                if (method == null)
                {
                    continue;
                }

                object result = method.Invoke(claimsApi, new object[] { pos });
                object claim = PickSingleClaim(result);
                if (claim != null)
                {
                    return claim;
                }
            }

            return null;
        }

        private static bool IsPositionParameter(Type paramType)
        {
            return typeof(BlockPos).IsAssignableFrom(paramType)
                || paramType.Name.Contains("BlockPos", StringComparison.OrdinalIgnoreCase);
        }

        private static object PickSingleClaim(object result)
        {
            if (result == null || result is string)
            {
                return null;
            }

            if (result is System.Collections.IEnumerable enumerable)
            {
                foreach (object entry in enumerable)
                {
                    if (entry != null)
                    {
                        return entry;
                    }
                }

                return null;
            }

            return result;
        }

        private static IEnumerable<BlockPos> EnumerateSquareEdge(BlockPos center, int radius)
        {
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                yield return new BlockPos(x, center.Y, center.Z - radius, center.dimension);
                yield return new BlockPos(x, center.Y, center.Z + radius, center.dimension);
            }

            for (int z = center.Z - radius + 1; z <= center.Z + radius - 1; z++)
            {
                yield return new BlockPos(center.X - radius, center.Y, z, center.dimension);
                yield return new BlockPos(center.X + radius, center.Y, z, center.dimension);
            }
        }

        private static bool PositionsEqual(BlockPos left, BlockPos right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return left.dimension == right.dimension
                && left.X == right.X
                && left.Y == right.Y
                && left.Z == right.Z;
        }

        private void EnsureGraveBlock(GraveData grave)
        {
            if (grave == null)
            {
                return;
            }

            Block graveBlock = ResolveGraveBlock();
            if (graveBlock == null)
            {
                return;
            }

            BlockPos pos = grave.ToBlockPos();
            Block current = api.World.BlockAccessor.GetBlock(pos);
            if (current == null || current.Id != graveBlock.Id)
            {
                api.World.BlockAccessor.SetBlock(graveBlock.Id, pos);
            }
        }

        private void RemoveGraveBlockIfPresent(GraveData grave)
        {
            if (grave == null)
            {
                return;
            }

            Block graveBlock = ResolveGraveBlock();
            if (graveBlock == null)
            {
                return;
            }

            BlockPos pos = grave.ToBlockPos();
            Block current = api.World.BlockAccessor.GetBlock(pos);
            if (current != null && current.Id == graveBlock.Id)
            {
                api.World.BlockAccessor.SetBlock(0, pos);
            }
        }

        private Vec3d FindSafeTeleportTarget(GraveData grave)
        {
            if (grave == null)
            {
                return null;
            }

            BlockPos gravePos = grave.ToBlockPos();
            int[] xOffsets = { 0, 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] zOffsets = { 0, 0, 0, 1, -1, 1, -1, 1, -1 };

            for (int yOffset = 1; yOffset <= 4; yOffset++)
            {
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    BlockPos feetPos = gravePos.AddCopy(xOffsets[i], yOffset, zOffsets[i]);
                    BlockPos headPos = feetPos.UpCopy(1);
                    BlockPos groundPos = feetPos.DownCopy(1);

                    Block feetBlock = api.World.BlockAccessor.GetBlock(feetPos);
                    Block headBlock = api.World.BlockAccessor.GetBlock(headPos);
                    Block groundBlock = api.World.BlockAccessor.GetBlock(groundPos);

                    if (!IsPassableTeleportSpace(feetBlock) || !IsPassableTeleportSpace(headBlock))
                    {
                        continue;
                    }

                    if (!IsSafeTeleportGround(groundBlock))
                    {
                        continue;
                    }

                    return new Vec3d(feetPos.X + 0.5, feetPos.Y, feetPos.Z + 0.5);
                }
            }

            return new Vec3d(gravePos.X + 0.5, gravePos.Y + 1, gravePos.Z + 0.5);
        }

        private static bool IsPassableTeleportSpace(Block block)
        {
            return block != null && (block.BlockId == 0 || block.Replaceable >= 6000);
        }

        private static bool IsSafeTeleportGround(Block block)
        {
            return block != null && block.BlockId != 0 && block.Replaceable < 6000;
        }

        private readonly struct GravePlacementResult
        {
            public readonly BlockPos Position;
            public readonly bool MovedOutsideForeignClaim;

            public GravePlacementResult(BlockPos position, bool movedOutsideForeignClaim)
            {
                Position = position;
                MovedOutsideForeignClaim = movedOutsideForeignClaim;
            }
        }

        private readonly struct LandClaimInfo
        {
            public static LandClaimInfo None => new LandClaimInfo(null, null, null);

            public readonly string Key;
            public readonly string OwnerUid;
            public readonly string OwnerName;

            public bool Exists => !string.IsNullOrWhiteSpace(Key);

            private LandClaimInfo(string key, string ownerUid, string ownerName)
            {
                Key = key;
                OwnerUid = ownerUid;
                OwnerName = ownerName;
            }

            public static LandClaimInfo FromClaim(object claim)
            {
                string key = ReadStringOrNull(claim, "ClaimId", "Id", "ProtectionId", "LandClaimId");
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = claim?.GetHashCode().ToString() ?? string.Empty;
                }

                string ownerUid = ReadStringOrNull(claim, "OwnedByPlayerUid", "OwnerUid", "OwnerPlayerUid", "PlayerUid", "Uid");
                string ownerName = ReadStringOrNull(claim, "OwnedByPlayerName", "OwnerName", "OwnerPlayerName", "LastKnownOwnerName", "PlayerName");
                return new LandClaimInfo(key, ownerUid, ownerName);
            }

            private static string ReadStringOrNull(object obj, params string[] names)
            {
                object value = ReadObjectOrNull(obj, names);
                return value?.ToString();
            }

            private static object ReadObjectOrNull(object obj, params string[] names)
            {
                if (obj == null || names == null || names.Length == 0)
                {
                    return null;
                }

                Type type = obj.GetType();
                foreach (string name in names)
                {
                    PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (property != null)
                    {
                        return property.GetValue(obj);
                    }

                    FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null)
                    {
                        return field.GetValue(obj);
                    }
                }

                return null;
            }
        }
    }
}
