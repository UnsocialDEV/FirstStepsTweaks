using System;
using System.Collections.Generic;
using System.IO;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public class CorpseService
    {
        private readonly ICoreServerAPI api;
        private const string EmergencyBackupPrefix = "deathbones-emergency-";
        private const string GraveIndexKey = "deathbones-index";
        private const int GraveSearchRadius = 4;
        private readonly HashSet<BlockPos> suppressDropPositions = new HashSet<BlockPos>();
        public class GraveSummary
        {
            public int GraveId;
            public string OwnerName;
            public string OwnerUid;
            public BlockPos Position;
        }

        private class GraveRecord
        {
            public int GraveId;
            public string OwnerName;
            public string OwnerUid;
            public BlockPos Position;
        }

        private readonly Dictionary<string, GraveRecord> activeGravesByPos = new Dictionary<string, GraveRecord>();
        private readonly HashSet<int> removedOrClaimedGraveIds = new HashSet<int>();
        private readonly HashSet<string> deathProcessingPlayers = new HashSet<string>();
        private readonly object deathProcessingLock = new object();
        private bool suspendIndexPersistence;
        private int nextGraveId = 1;
        private int graveBlockId;
        private readonly CorpseConfig corpseConfig;

        public CorpseService(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            this.api = api;
            corpseConfig = config?.Corpse ?? new CorpseConfig();

            Block grave = api.World.GetBlock(new AssetLocation(corpseConfig.GraveBlockCode));
            graveBlockId = grave?.BlockId ?? 0;

            api.Event.RegisterGameTickListener(RemoveGraveDrops, corpseConfig.DropCleanupTickMs);
            api.Event.RegisterGameTickListener(EnforceGravesPresent, corpseConfig.EnforceGraveTickMs);

            LoadActiveGraveIndex();
        }

        private void RemoveGraveDrops(float dt)
        {
            if (suppressDropPositions.Count == 0) return;

            foreach (var gravePos in suppressDropPositions)
            {
                var entities = api.World.GetEntitiesAround(
                    gravePos.ToVec3d(),
                    2f,
                    2f,
                    e => e is EntityItem
                );

                foreach (var entity in entities)
                {
                    if (entity is EntityItem item &&
                        item.Itemstack?.Collectible?.Code?.Path == GetGravePath())
                    {
                        item.Die();
                        api.Logger.Warning("[GRAVE] Skull drop removed.");
                    }
                }
            }

            suppressDropPositions.Clear();
        }
        private void EnforceGravesPresent(float dt)
        {
            if (activeGravesByPos.Count == 0 || graveBlockId == 0) return;

            // Re-place skull if data exists and the block is missing
            foreach (var record in activeGravesByPos.Values)
            {
                BlockPos pos = record.Position;
                // If the skull is gone, restore it
                Block current = api.World.BlockAccessor.GetBlock(pos);
                if (current == null || current.Code == null) continue;

                if (current.Code.Path != GetGravePath())
                {
                    // Only restore if still tracked and the save key still exists.
                    if (!activeGravesByPos.ContainsKey(GetPositionKey(pos))) continue;

                    string key = GetGraveDataKey(pos);
                    byte[] raw = api.WorldManager.SaveGame.GetData(key);
                    if (raw == null || raw.Length == 0) continue;

                    int graveId = ReadGraveId(raw, pos);
                    if (graveId <= 0 || removedOrClaimedGraveIds.Contains(graveId)) continue;

                    PlaceGraveBlock(pos);
                }
            }
        }

        public void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (!(entity is EntityPlayer entityPlayer)) return;

            IPlayer player = entityPlayer.Player;
            if (player == null) return;

            if (!TryBeginDeathProcessing(player.PlayerUID))
            {
                api.Logger.Warning($"[GRAVE SAVE] Skipping overlapping death processing for {player.PlayerUID}");
                return;
            }

            try
            {
                // Only apply in Survival mode
                if (player.WorldData.CurrentGameMode != EnumGameMode.Survival)
                {
                    return;
                }

                var invManager = player.InventoryManager;
                if (invManager == null) return;

                List<ItemStack> savedStacks = new List<ItemStack>();
                CollectInventory(invManager.GetOwnInventory("hotbar"), savedStacks);
                CollectInventory(invManager.GetOwnInventory("backpack"), savedStacks);

                // Do nothing if no items
                if (savedStacks.Count == 0) return;

                BlockPos requestedPos = entity.Pos.AsBlockPos.Copy();
                BlockPos gravePos = ResolveGravePosition(requestedPos);
                if (gravePos == null)
                {
                    api.Logger.Error($"[GRAVE SAVE] No valid grave position available for {player.PlayerUID} at requested {requestedPos}");
                    return;
                }

                int graveId = GetOrCreateGraveId(gravePos);

                if (!gravePos.Equals(requestedPos))
                {
                    api.Logger.Warning($"[GRAVE SAVE] Requested position {requestedPos} is occupied. Using nearby position {gravePos} for graveId={graveId}.");
                }

                // Atomic-style ordering: backup -> clear inventory -> save grave -> spawn block
                if (!SaveEmergencyBackup(player, savedStacks, gravePos, graveId))
                {
                    api.Logger.Error($"[GRAVE SAVE] Failed emergency backup for {player.PlayerUID} graveId={graveId}. Inventory was not touched.");
                    return;
                }

                ClearInventory(invManager.GetOwnInventory("hotbar"));
                ClearInventory(invManager.GetOwnInventory("backpack"));

                if (!SaveToWorldData(player, savedStacks, gravePos, graveId))
                {
                    api.Logger.Error($"[GRAVE SAVE] Failed grave save for {player.PlayerUID} graveId={graveId}. Inventory remains recoverable from backup.");
                    return;
                }

                // Grave creation succeeded; clear emergency backup immediately.
                ClearEmergencyBackup(player.PlayerUID, graveId, "grave created");

                SpawnBones(gravePos, player.PlayerName);
            }
            finally
            {
                EndDeathProcessing(player.PlayerUID);
            }
        }

        private void CollectInventory(IInventory inventory, List<ItemStack> savedStacks)
        {
            if (inventory == null) return;

            foreach (var slot in inventory)
            {
                if (slot?.Itemstack != null)
                {
                    savedStacks.Add(slot.Itemstack.Clone());
                }
            }
        }

        private void ClearInventory(IInventory inventory)
        {
            if (inventory == null) return;

            foreach (var slot in inventory)
            {
                if (slot?.Itemstack != null)
                {
                    slot.Itemstack = null;
                    slot.MarkDirty();
                }
            }
        }

        private bool SaveEmergencyBackup(IPlayer player, List<ItemStack> stacks, BlockPos pos, int graveId)
        {
            byte[] raw = SerializeGraveData(player, stacks, pos, graveId);
            if (raw == null || raw.Length == 0) return false;

            string backupKey = GetEmergencyBackupKey(player.PlayerUID);
            api.WorldManager.SaveGame.StoreData(backupKey, raw);

            byte[] confirm = api.WorldManager.SaveGame.GetData(backupKey);
            bool ok = confirm != null && confirm.Length > 0;
            if (ok)
            {
                api.Logger.Warning($"[GRAVE BACKUP] Emergency backup updated for {player.PlayerUID} graveId={graveId} pos={pos}");
            }

            return ok;
        }

        private bool SaveToWorldData(IPlayer player, List<ItemStack> stacks, BlockPos pos, int graveId)
        {
            string key = GetGraveDataKey(pos);

            byte[] existingRaw = api.WorldManager.SaveGame.GetData(key);
            if (existingRaw != null && existingRaw.Length > 0)
            {
                api.Logger.Error($"[GRAVE SAVE] Refusing save for {player.PlayerUID} graveId={graveId}: active grave data already exists at {pos}");
                return false;
            }

            byte[] raw = SerializeGraveData(player, stacks, pos, graveId);
            if (raw == null || raw.Length == 0) return false;

            api.WorldManager.SaveGame.StoreData(key, raw);
            byte[] confirm = api.WorldManager.SaveGame.GetData(key);
            if (confirm == null || confirm.Length == 0)
            {
                return false;
            }

            TrackOrUpdateActiveGrave(pos, player.PlayerUID, player.PlayerName, ReadGraveId(raw, pos));
            api.Logger.Warning($"[GRAVE SAVE] Stored grave at {pos} with {stacks.Count} stack(s) graveId={graveId}");
            return true;
        }

        private byte[] SerializeGraveData(IPlayer player, List<ItemStack> stacks, BlockPos pos, int graveId = 0)
        {
            TreeAttribute tree = new TreeAttribute();

            tree.SetString("owner", player.PlayerUID);
            tree.SetString("ownerName", player.PlayerName);
            tree.SetInt("graveId", graveId > 0 ? graveId : GetOrCreateGraveId(pos));
            tree.SetLong("createdAtMs", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            tree.SetInt("x", pos.X);
            tree.SetInt("y", pos.Y);
            tree.SetInt("z", pos.Z);
            tree.SetBool("claimed", false);

            TreeAttribute invTree = new TreeAttribute();

            for (int i = 0; i < stacks.Count; i++)
            {
                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    stacks[i].ToBytes(writer);
                    invTree.SetBytes($"stack{i}", ms.ToArray());
                }
            }

            tree["inventory"] = invTree;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                tree.ToBytes(writer);
                return ms.ToArray();
            }
        }

        private List<ItemStack> LoadStacksFromRaw(byte[] raw)
        {
            TreeAttribute tree;
            using (var ms = new MemoryStream(raw))
            using (var reader = new BinaryReader(ms))
            {
                tree = new TreeAttribute();
                tree.FromBytes(reader);
            }

            return LoadInventoryFromTree(tree);
        }

        private string GetEmergencyBackupKey(string playerUid)
        {
            return $"{EmergencyBackupPrefix}{playerUid}";
        }

        private void SpawnBones(BlockPos pos, string ownerName)
        {
            Block bones = api.World.GetBlock(new AssetLocation(corpseConfig.GraveBlockCode));
            if (bones == null) return;

            PlaceGraveBlock(pos);
        }
        public void OnBlockBroken(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (byPlayer == null || blockSel == null) return;

            BlockPos pos = blockSel.Position;

            // Only care about our grave block breaks
            Block block = api.World.GetBlock(oldblockId);
            if (block == null || block.Code.Path != GetGravePath()) return;

            string key = GetGraveDataKey(pos);
            byte[] raw = api.WorldManager.SaveGame.GetData(key);

            // Failsafe: if grave entry is missing/corrupt, attempt emergency backup restore
            if (raw == null || raw.Length == 0)
            {
                TryRestoreEmergencyBackup(byPlayer, "missing grave data");
                return;
            }

            TreeAttribute tree;
            using (var ms = new MemoryStream(raw))
            using (var reader = new BinaryReader(ms))
            {
                tree = new TreeAttribute();
                tree.FromBytes(reader);
            }

            string owner = tree.GetString("owner");
            bool isOwner = owner == byPlayer.PlayerUID;
            bool isExpired = IsGraveExpired(tree);

            // NON-OWNER before expiration: put it back and delete the drop
            if (!isOwner && !isExpired)
            {
                long remainingMs = GetRemainingGraveTimeMs(tree);
                int remainingMinutes = (int)Math.Ceiling(remainingMs / 60000d);

                byPlayer.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    $"This grave is protected for {remainingMinutes} more minute(s).",
                    EnumChatType.Notification
                );
                byPlayer.SendMessage(
                    GlobalConstants.GeneralChatGroup,
                    $"This grave is protected for {remainingMinutes} more minute(s).",
                    EnumChatType.Notification
                );

                // Put the grave block back immediately
                if (graveBlockId != 0)
                {
                    PlaceGraveBlock(pos);
                }

                // Remove the dropped skull entity near this position
                suppressDropPositions.Add(pos.Copy());

                // Keep tracking it as an active grave
                EnsureTrackedFromRaw(pos, raw);
                return;
            }

            if (!isOwner && isExpired)
            {
                byPlayer.SendMessage(
                    GlobalConstants.GeneralChatGroup,
                    "This grave has expired. You recovered its items.",
                    EnumChatType.Notification
                );
            }

            // OWNER: restore items
            int graveId = tree.GetInt("graveId", 0);
            removedOrClaimedGraveIds.Add(graveId);

            // Delete persisted grave data before giving items back.
            api.WorldManager.SaveGame.StoreData(key, new byte[0]);
            RemoveTrackedGrave(pos);

            List<ItemStack> stacks = LoadInventoryFromTree(tree);
            if (stacks != null && stacks.Count > 0)
            {
                GiveItemsBack(byPlayer, stacks);
            }

            // Remove the grave block (already broken, but keep consistent)
            // (No need to set air; it is already air after DidBreakBlock)

            // Clear emergency backup once the owner successfully claims their grave
            if (isOwner)
            {
                ClearEmergencyBackup(byPlayer.PlayerUID, graveId, "grave claimed");
            }

            // Also remove the skull drop for owner breaks
            suppressDropPositions.Add(pos.Copy());

            api.Logger.Warning($"[GRAVE RESTORE] Restored graveId={graveId} at {pos} by {byPlayer.PlayerName} (owner={isOwner}, expired={isExpired})");
        }

        private void GiveItemsBack(IServerPlayer player, List<ItemStack> stacks)
        {

            foreach (var stack in stacks)
            {
                if (stack == null || stack.StackSize <= 0) continue;

                api.Logger.Warning($"[GRAVE GIVE] Attempting to give {stack.Collectible?.Code} x{stack.StackSize}");

                ItemStack giveStack = stack.Clone();

                bool fullyGiven = player.InventoryManager.TryGiveItemstack(giveStack, true);
                api.Logger.Warning($"[GRAVE GIVE] Remaining stack size after give: {giveStack.StackSize}");

                if (!fullyGiven && giveStack.StackSize > 0)
                {
                    api.World.SpawnItemEntity(giveStack, player.Entity.Pos.XYZ);
                }
            }
        }

        private List<ItemStack> LoadInventoryFromTree(TreeAttribute tree)
        {
            TreeAttribute invTree = tree["inventory"] as TreeAttribute;
            if (invTree == null) return null;

            List<ItemStack> stacks = new List<ItemStack>();

            int index = 0;

            while (true)
            {
                string key = $"stack{index}";
                if (!invTree.HasAttribute(key)) break;

                byte[] bytes = invTree.GetBytes(key);

                using (MemoryStream ms = new MemoryStream(bytes))
                using (BinaryReader reader = new BinaryReader(ms))
                {
                    ItemStack stack = new ItemStack();
                    stack.FromBytes(reader);
                    stack.ResolveBlockOrItem(api.World);

                    if (stack.Collectible != null && stack.StackSize > 0)
                    {
                        stacks.Add(stack);
                    }
                }

                index++;
            }

            return stacks;
        }

        private long GetCreatedAtMs(TreeAttribute tree)
        {
            long createdAt = tree.GetLong("createdAtMs", 0L);
            if (createdAt > 0) return createdAt;

            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private bool IsGraveExpired(TreeAttribute tree)
        {
            long expireMs = corpseConfig.GraveExpireMs;
            if (expireMs <= 0) return true;

            long createdAt = GetCreatedAtMs(tree);
            long ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - createdAt;
            return ageMs >= expireMs;
        }

        private long GetRemainingGraveTimeMs(TreeAttribute tree)
        {
            long expireMs = corpseConfig.GraveExpireMs;
            if (expireMs <= 0) return 0;

            long createdAt = GetCreatedAtMs(tree);
            long ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - createdAt;
            long remaining = expireMs - ageMs;
            return remaining > 0 ? remaining : 0;
        }

        private bool TryRestoreEmergencyBackup(IServerPlayer player, string reason)
        {
            string backupKey = GetEmergencyBackupKey(player.PlayerUID);
            byte[] backupRaw = api.WorldManager.SaveGame.GetData(backupKey);
            if (backupRaw == null || backupRaw.Length == 0) return false;

            TreeAttribute backupTree;
            using (var ms = new MemoryStream(backupRaw))
            using (var reader = new BinaryReader(ms))
            {
                backupTree = new TreeAttribute();
                backupTree.FromBytes(reader);
            }

            int backupGraveId = backupTree.GetInt("graveId", 0);
            BlockPos backupPos = new BlockPos(
                backupTree.GetInt("x", 0),
                backupTree.GetInt("y", 0),
                backupTree.GetInt("z", 0)
            );

            // Restore only when that exact grave is missing from tracking and world data.
            bool stillTracked = activeGravesByPos.ContainsKey(GetPositionKey(backupPos));
            byte[] linkedRaw = api.WorldManager.SaveGame.GetData(GetGraveDataKey(backupPos));
            bool hasLinkedData = linkedRaw != null && linkedRaw.Length > 0;
            if (stillTracked || hasLinkedData)
            {
                api.Logger.Warning($"[GRAVE BACKUP] Skipping restore for {player.PlayerUID} graveId={backupGraveId}; linked grave still exists at {backupPos}");
                return false;
            }

            List<ItemStack> backupStacks = LoadStacksFromRaw(backupRaw);
            if (backupStacks == null || backupStacks.Count == 0) return false;

            GiveItemsBack(player, backupStacks);
            ClearEmergencyBackup(player.PlayerUID, backupGraveId, reason);

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                $"Your corpse backup was restored ({reason}).",
                EnumChatType.Notification
            );
            api.Logger.Warning($"[GRAVE BACKUP] Restored emergency backup for {player.PlayerUID} graveId={backupGraveId} ({reason})");
            return true;
        }

        public List<GraveSummary> GetActiveGraves()
        {
            List<GraveSummary> graves = new List<GraveSummary>();

            foreach (var record in activeGravesByPos.Values)
            {
                graves.Add(new GraveSummary
                {
                    GraveId = record.GraveId,
                    OwnerName = record.OwnerName,
                    OwnerUid = record.OwnerUid,
                    Position = record.Position.Copy()
                });
            }

            graves.Sort((a, b) => a.GraveId.CompareTo(b.GraveId));
            return graves;
        }


        public bool TryRemoveGraveById(int graveId)
        {
            if (!TryGetPositionByGraveId(graveId, out BlockPos pos)) return false;
            return TryRemoveGrave(pos);
        }

        public bool TryDuplicateGraveItemsById(int graveId, IServerPlayer target)
        {
            if (!TryGetPositionByGraveId(graveId, out BlockPos pos)) return false;
            return TryDuplicateGraveItems(pos, target);
        }

        public bool TryGiveGraveItemsById(int graveId, IServerPlayer target)
        {
            if (!TryGetPositionByGraveId(graveId, out BlockPos pos)) return false;
            return TryGiveGraveItems(pos, target);
        }

        public bool TryRemoveGrave(BlockPos pos)
        {
            if (pos == null) return false;

            string key = GetGraveDataKey(pos);
            byte[] raw = api.WorldManager.SaveGame.GetData(key);

            if (raw == null || raw.Length == 0)
            {
                return false;
            }

            api.WorldManager.SaveGame.StoreData(key, new byte[0]);
            int graveId = ReadGraveId(raw, pos);
            removedOrClaimedGraveIds.Add(graveId);
            RemoveTrackedGrave(pos);

            Block current = api.World.BlockAccessor.GetBlock(pos);
            if (current != null && current.Code != null && current.Code.Path == GetGravePath())
            {
                api.World.BlockAccessor.SetBlock(0, pos);
            }

            return true;
        }

        public bool TryDuplicateGraveItems(BlockPos pos, IServerPlayer target)
        {
            if (pos == null || target == null) return false;
            if (!TryValidateGraveAccess(pos, out _, out _)) return false;

            List<ItemStack> stacks = LoadStacksAtPos(pos);
            if (stacks == null || stacks.Count == 0) return false;

            GiveItemsBack(target, stacks);
            return true;
        }

        public bool TryGiveGraveItems(BlockPos pos, IServerPlayer target)
        {
            if (pos == null || target == null) return false;
            if (!TryValidateGraveAccess(pos, out _, out _)) return false;

            List<ItemStack> stacks = LoadStacksAtPos(pos);
            if (stacks == null || stacks.Count == 0) return false;

            GiveItemsBack(target, stacks);
            return TryRemoveGrave(pos);
        }

        private List<ItemStack> LoadStacksAtPos(BlockPos pos)
        {
            string key = GetGraveDataKey(pos);
            byte[] raw = api.WorldManager.SaveGame.GetData(key);
            if (raw == null || raw.Length == 0) return null;

            return LoadStacksFromRaw(raw);
        }

        private bool TryValidateGraveAccess(BlockPos pos, out int graveId, out bool expired)
        {
            graveId = 0;
            expired = false;

            if (pos == null) return false;
            if (!activeGravesByPos.ContainsKey(GetPositionKey(pos))) return false;

            string key = GetGraveDataKey(pos);
            byte[] raw = api.WorldManager.SaveGame.GetData(key);
            if (raw == null || raw.Length == 0) return false;

            TreeAttribute tree;
            using (var ms = new MemoryStream(raw))
            using (var reader = new BinaryReader(ms))
            {
                tree = new TreeAttribute();
                tree.FromBytes(reader);
            }

            graveId = tree.GetInt("graveId", 0);
            if (graveId <= 0 || removedOrClaimedGraveIds.Contains(graveId)) return false;

            expired = IsGraveExpired(tree);
            if (expired) return false;

            return true;
        }

        private int GetOrCreateGraveId(BlockPos pos)
        {
            string posKey = GetPositionKey(pos);
            if (activeGravesByPos.TryGetValue(posKey, out GraveRecord existing))
            {
                return existing.GraveId;
            }

            int graveId = nextGraveId;
            nextGraveId++;
            return graveId;
        }

        private int ReadGraveId(byte[] raw, BlockPos pos)
        {
            TreeAttribute tree;
            using (var ms = new MemoryStream(raw))
            using (var reader = new BinaryReader(ms))
            {
                tree = new TreeAttribute();
                tree.FromBytes(reader);
            }

            int graveId = tree.GetInt("graveId", 0);
            return graveId > 0 ? graveId : GetOrCreateGraveId(pos);
        }

        private void TrackOrUpdateActiveGrave(BlockPos pos, string ownerUid, string ownerName, int graveId)
        {
            if (graveId >= nextGraveId)
            {
                nextGraveId = graveId + 1;
            }

            string posKey = GetPositionKey(pos);
            activeGravesByPos[posKey] = new GraveRecord
            {
                GraveId = graveId,
                OwnerUid = ownerUid,
                OwnerName = string.IsNullOrEmpty(ownerName) ? ownerUid : ownerName,
                Position = pos.Copy()
            };

            if (!suspendIndexPersistence)
            {
                SaveActiveGraveIndex();
            }
        }

        private void EnsureTrackedFromRaw(BlockPos pos, byte[] raw)
        {
            TreeAttribute tree;
            using (var ms = new MemoryStream(raw))
            using (var reader = new BinaryReader(ms))
            {
                tree = new TreeAttribute();
                tree.FromBytes(reader);
            }

            string ownerUid = tree.GetString("owner");
            string ownerName = tree.GetString("ownerName");
            int graveId = tree.GetInt("graveId", 0);

            if (graveId <= 0)
            {
                graveId = GetOrCreateGraveId(pos);
            }

            TrackOrUpdateActiveGrave(pos, ownerUid, ownerName, graveId);
        }


        private bool TryGetPositionByGraveId(int graveId, out BlockPos pos)
        {
            foreach (var record in activeGravesByPos.Values)
            {
                if (record.GraveId == graveId)
                {
                    pos = record.Position.Copy();
                    return true;
                }
            }

            pos = null;
            return false;
        }

        private void RemoveTrackedGrave(BlockPos pos)
        {
            if (activeGravesByPos.Remove(GetPositionKey(pos)) && !suspendIndexPersistence)
            {
                SaveActiveGraveIndex();
            }
        }

        private void SaveActiveGraveIndex()
        {
            TreeAttribute indexTree = new TreeAttribute();
            int i = 0;

            foreach (var record in activeGravesByPos.Values)
            {
                TreeAttribute recordTree = new TreeAttribute();
                recordTree.SetInt("graveId", record.GraveId);
                recordTree.SetString("ownerUid", record.OwnerUid ?? string.Empty);
                recordTree.SetString("ownerName", record.OwnerName ?? string.Empty);
                recordTree.SetInt("x", record.Position.X);
                recordTree.SetInt("y", record.Position.Y);
                recordTree.SetInt("z", record.Position.Z);
                indexTree[$"grave{i}"] = recordTree;
                i++;
            }

            indexTree.SetInt("count", i);

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                indexTree.ToBytes(writer);
                api.WorldManager.SaveGame.StoreData(GraveIndexKey, ms.ToArray());
            }
        }

        private void LoadActiveGraveIndex()
        {
            byte[] raw = api.WorldManager.SaveGame.GetData(GraveIndexKey);
            if (raw == null || raw.Length == 0) return;

            TreeAttribute indexTree;
            using (MemoryStream ms = new MemoryStream(raw))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                indexTree = new TreeAttribute();
                indexTree.FromBytes(reader);
            }

            int count = indexTree.GetInt("count", 0);
            bool shouldRewriteIndex = false;

            suspendIndexPersistence = true;
            for (int i = 0; i < count; i++)
            {
                if (!(indexTree[$"grave{i}"] is TreeAttribute recordTree)) continue;

                BlockPos pos = new BlockPos(
                    recordTree.GetInt("x", 0),
                    recordTree.GetInt("y", 0),
                    recordTree.GetInt("z", 0)
                );

                string key = GetGraveDataKey(pos);
                byte[] graveRaw = api.WorldManager.SaveGame.GetData(key);
                if (graveRaw == null || graveRaw.Length == 0)
                {
                    shouldRewriteIndex = true;
                    continue;
                }

                int graveId = recordTree.GetInt("graveId", 0);
                if (graveId <= 0)
                {
                    graveId = GetOrCreateGraveId(pos);
                    shouldRewriteIndex = true;
                }

                TrackOrUpdateActiveGrave(
                    pos,
                    recordTree.GetString("ownerUid"),
                    recordTree.GetString("ownerName"),
                    graveId
                );
            }
            suspendIndexPersistence = false;

            if (shouldRewriteIndex)
            {
                SaveActiveGraveIndex();
            }
        }

        private string GetPositionKey(BlockPos pos)
        {
            return $"{pos.X}:{pos.Y}:{pos.Z}";
        }

        private string GetGraveDataKey(BlockPos pos)
        {
            return $"deathbones-{pos.X}-{pos.Y}-{pos.Z}";
        }

        private BlockPos ResolveGravePosition(BlockPos requestedPos)
        {
            if (CanUseGravePosition(requestedPos))
            {
                return requestedPos.Copy();
            }

            int[] yOffsets = new[] { 0, 1, -1, 2, -2 };

            for (int radius = 1; radius <= GraveSearchRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dz) != radius) continue;

                        foreach (int yOffset in yOffsets)
                        {
                            BlockPos candidate = new BlockPos(requestedPos.X + dx, requestedPos.Y + yOffset, requestedPos.Z + dz);
                            if (CanUseGravePosition(candidate))
                            {
                                return candidate;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private bool CanUseGravePosition(BlockPos pos)
        {
            if (pos == null) return false;
            if (activeGravesByPos.ContainsKey(GetPositionKey(pos))) return false;

            byte[] raw = api.WorldManager.SaveGame.GetData(GetGraveDataKey(pos));
            if (raw != null && raw.Length > 0) return false;

            Block block = api.World.BlockAccessor.GetBlock(pos);
            if (block == null || block.Code == null) return false;
            if (block.Code.Path == GetGravePath()) return false;

            return block.Replaceable >= 6000;
        }

        private void ClearEmergencyBackup(string playerUid, int graveId, string reason)
        {
            api.WorldManager.SaveGame.StoreData(GetEmergencyBackupKey(playerUid), new byte[0]);
            api.Logger.Warning($"[GRAVE BACKUP] Cleared emergency backup for {playerUid} graveId={graveId} ({reason})");
        }

        private bool TryBeginDeathProcessing(string playerUid)
        {
            lock (deathProcessingLock)
            {
                return deathProcessingPlayers.Add(playerUid);
            }
        }

        private void EndDeathProcessing(string playerUid)
        {
            lock (deathProcessingLock)
            {
                deathProcessingPlayers.Remove(playerUid);
            }
        }

        private string GetGravePath()
        {
            return new AssetLocation(corpseConfig.GraveBlockCode).Path;
        }

        private void PlaceGraveBlock(BlockPos pos)
        {
            Block block = api.World.GetBlock(new AssetLocation("game:clutter"));
            if (block == null) return;

            ItemStack stack = new ItemStack(block);
            stack.Attributes.SetString("type", "gravestone-3");

            api.World.BlockAccessor.SetBlock(block.BlockId, pos, stack);    
            api.World.BlockAccessor.MarkBlockDirty(pos);

        }
    }
}
