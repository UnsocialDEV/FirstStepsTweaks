using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly HashSet<BlockPos> suppressDropPositions = new HashSet<BlockPos>();
        private readonly HashSet<BlockPos> activeGraves = new HashSet<BlockPos>();
        private int graveBlockId;

        public CorpseService(ICoreServerAPI api)
        {
            this.api = api;

            Block grave = api.World.GetBlock(new AssetLocation("game:figurehead-skull"));
            graveBlockId = grave?.BlockId ?? 0;

            api.Event.RegisterGameTickListener(RemoveGraveDrops, 50);
            api.Event.RegisterGameTickListener(EnforceGravesPresent, 200); // optional but recommended
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
                        item.Itemstack?.Collectible?.Code?.Path == "figurehead-skull")
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
            if (activeGraves.Count == 0 || graveBlockId == 0) return;

            // Re-place skull if data exists and the block is missing
            foreach (var pos in activeGraves)
            {
                // If the skull is gone, restore it
                Block current = api.World.BlockAccessor.GetBlock(pos);
                if (current == null || current.Code == null) continue;

                if (current.Code.Path != "figurehead-skull")
                {
                    // Only restore if the save key still exists
                    string key = $"deathbones-{pos.X}-{pos.Y}-{pos.Z}";
                    byte[] raw = api.WorldManager.SaveGame.GetData(key);

                    if (raw != null && raw.Length > 0)
                    {
                        api.World.BlockAccessor.SetBlock(graveBlockId, pos);
                    }
                }
            }
        }

        public void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (!(entity is EntityPlayer entityPlayer)) return;

            IPlayer player = entityPlayer.Player;
            if (player == null) return;

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

            BlockPos pos = entity.Pos.AsBlockPos.Copy();

            if (!SaveEmergencyBackup(player, savedStacks, pos))
            {
                api.Logger.Error($"[GRAVE SAVE] Failed emergency backup for {player.PlayerUID}. Inventory was not touched.");
                return;
            }

            if (!SaveToWorldData(player, savedStacks, pos))
            {
                api.Logger.Error($"[GRAVE SAVE] Failed grave save for {player.PlayerUID}. Inventory was not touched.");
                return;
            }

            // Save succeeded, now clear source inventories
            ClearInventory(invManager.GetOwnInventory("hotbar"));
            ClearInventory(invManager.GetOwnInventory("backpack"));

            SpawnBones(pos);
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

        private bool SaveEmergencyBackup(IPlayer player, List<ItemStack> stacks, BlockPos pos)
        {
            byte[] raw = SerializeGraveData(player, stacks, pos);
            if (raw == null || raw.Length == 0) return false;

            string backupKey = GetEmergencyBackupKey(player.PlayerUID);
            api.WorldManager.SaveGame.StoreData(backupKey, raw);

            byte[] confirm = api.WorldManager.SaveGame.GetData(backupKey);
            bool ok = confirm != null && confirm.Length > 0;
            if (ok)
            {
                api.Logger.Warning($"[GRAVE BACKUP] Emergency backup updated for {player.PlayerUID}");
            }

            return ok;
        }

        private bool SaveToWorldData(IPlayer player, List<ItemStack> stacks, BlockPos pos)
        {
            string key = $"deathbones-{pos.X}-{pos.Y}-{pos.Z}";

            List<ItemStack> mergedStacks = new List<ItemStack>();
            byte[] existingRaw = api.WorldManager.SaveGame.GetData(key);
            if (existingRaw != null && existingRaw.Length > 0)
            {
                var existing = LoadStacksFromRaw(existingRaw);
                if (existing != null && existing.Count > 0)
                {
                    mergedStacks.AddRange(existing);
                    api.Logger.Warning($"[GRAVE SAVE] Merging {existing.Count} existing stacks at {pos}");
                }
            }

            mergedStacks.AddRange(stacks);

            byte[] raw = SerializeGraveData(player, mergedStacks, pos);
            if (raw == null || raw.Length == 0) return false;

            api.WorldManager.SaveGame.StoreData(key, raw);
            byte[] confirm = api.WorldManager.SaveGame.GetData(key);
            if (confirm == null || confirm.Length == 0)
            {
                return false;
            }

            activeGraves.Add(pos.Copy());
            api.Logger.Warning($"[GRAVE SAVE] Stored grave at {pos} with {mergedStacks.Count} stack(s)");
            return true;
        }

        private byte[] SerializeGraveData(IPlayer player, List<ItemStack> stacks, BlockPos pos)
        {
            TreeAttribute tree = new TreeAttribute();

            tree.SetString("owner", player.PlayerUID);
            tree.SetInt("x", pos.X);
            tree.SetInt("y", pos.Y);
            tree.SetInt("z", pos.Z);

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

        private void SpawnBones(BlockPos pos)
        {
            Block bones = api.World.GetBlock(new AssetLocation("game:figurehead-skull"));
            if (bones == null) return;

            api.World.BlockAccessor.SetBlock(bones.BlockId, pos);
        }
        public void OnBlockBroken(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (byPlayer == null || blockSel == null) return;

            BlockPos pos = blockSel.Position;

            // Only care about our grave block breaks
            Block block = api.World.GetBlock(oldblockId);
            if (block == null || block.Code.Path != "figurehead-skull") return;

            string key = $"deathbones-{pos.X}-{pos.Y}-{pos.Z}";
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

            // NON-OWNER: put it back and delete the drop
            if (owner != byPlayer.PlayerUID)
            {
                byPlayer.SendMessage(
                    GlobalConstants.GeneralChatGroup,
                    "This is not your grave.",
                    EnumChatType.Notification
                );

                // Put the grave block back immediately
                if (graveBlockId != 0)
                {
                    api.World.BlockAccessor.SetBlock(graveBlockId, pos);
                }

                // Remove the dropped skull entity near this position
                suppressDropPositions.Add(pos.Copy());

                // Keep tracking it as an active grave
                activeGraves.Add(pos.Copy());
                return;
            }

            // OWNER: restore items
            List<ItemStack> stacks = LoadInventoryFromTree(tree);
            if (stacks != null && stacks.Count > 0)
            {
                GiveItemsBack(byPlayer, stacks);
            }

            // Remove the grave block (already broken, but keep consistent)
            // (No need to set air; it is already air after DidBreakBlock)

            // Clear saved data (your build has no DeleteData)
            api.WorldManager.SaveGame.StoreData(key, new byte[0]);

            // Clear emergency backup once a grave is successfully claimed
            api.WorldManager.SaveGame.StoreData(GetEmergencyBackupKey(byPlayer.PlayerUID), new byte[0]);

            // Stop tracking as an active grave
            activeGraves.RemoveWhere(p => p.Equals(pos));

            // Also remove the skull drop for owner breaks
            suppressDropPositions.Add(pos.Copy());

            api.Logger.Warning($"[GRAVE] Restored grave at {pos}");
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

        private bool TryRestoreEmergencyBackup(IServerPlayer player, string reason)
        {
            string backupKey = GetEmergencyBackupKey(player.PlayerUID);
            byte[] backupRaw = api.WorldManager.SaveGame.GetData(backupKey);
            if (backupRaw == null || backupRaw.Length == 0) return false;

            List<ItemStack> backupStacks = LoadStacksFromRaw(backupRaw);
            if (backupStacks == null || backupStacks.Count == 0) return false;

            GiveItemsBack(player, backupStacks);
            api.WorldManager.SaveGame.StoreData(backupKey, new byte[0]);

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                $"Your corpse backup was restored ({reason}).",
                EnumChatType.Notification
            );
            api.Logger.Warning($"[GRAVE BACKUP] Restored emergency backup for {player.PlayerUID} ({reason})");
            return true;
        }
    }
}
