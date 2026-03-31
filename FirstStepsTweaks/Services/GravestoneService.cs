using FirstStepsTweaks.Config;
using FirstStepsTweaks.Gravestones;
using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class GravestoneService
    {
        private readonly ICoreServerAPI api;
        private readonly CorpseConfig config;
        private readonly IPlayerMessenger messenger;
        private readonly IGraveRepository graveManager;
        private readonly GraveClaimPolicy claimPolicy;
        private readonly IGraveBlockSynchronizer blockSynchronizer;
        private readonly IGravePlacementService placementService;
        private readonly IGraveSnapshotter snapshotter;
        private readonly IGraveRestorer restorer;
        private readonly object claimLock = new object();
        private readonly HashSet<string> claimInProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public GravestoneService(
            ICoreServerAPI api,
            FirstStepsTweaksConfig rootConfig,
            IPlayerMessenger messenger,
            ILandClaimAccessor landClaimAccessor)
        {
            this.api = api;
            config = rootConfig?.Corpse ?? new CorpseConfig();
            this.messenger = messenger ?? new PlayerMessenger();

            graveManager = new GraveManager(api);
            claimPolicy = new GraveClaimPolicy();
            blockSynchronizer = new GraveBlockSynchronizer(api, config);
            placementService = new GravePlacementService(api, landClaimAccessor ?? new ReflectionLandClaimAccessor(api));
            snapshotter = new GraveInventorySnapshotter();
            restorer = new GraveInventoryRestorer(api, graveManager, blockSynchronizer);

            api.Event.OnEntityDeath += OnEntityDeath;
            api.Event.BreakBlock += OnBreakBlock;
            api.Event.DidBreakBlock += OnDidBreakBlock;
            api.Event.DidUseBlock += OnDidUseBlock;
            api.Event.GameWorldSave += OnGameWorldSave;
            api.Event.SaveGameLoaded += ReconcilePersistedGraves;
            api.Event.RegisterGameTickListener(_ => CleanupAndReconcile(), Math.Max(10000, config.GraveCleanupTickMs));
        }

        public string GraveBlockCode => blockSynchronizer.GraveBlockCode;

        public List<GraveData> GetActiveGraves()
        {
            return graveManager.GetAll();
        }

        public bool IsPubliclyClaimable(GraveData grave)
        {
            return grave != null && claimPolicy.IsPubliclyClaimable(grave);
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

            int stackCount = restorer.DuplicateToPlayer(grave, targetPlayer);
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

                blockSynchronizer.RemoveIfPresent(removedGrave);
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

            target = placementService.FindSafeTeleportTarget(grave);
            if (target == null)
            {
                message = $"Unable to find a safe teleport destination for gravestone '{graveId}'.";
                return false;
            }

            return true;
        }

        public int ClearAllGraves()
        {
            int cleared = 0;
            foreach (GraveData grave in graveManager.GetAll())
            {
                if (grave == null)
                {
                    continue;
                }

                if (TryRemoveGrave(grave.GraveId, out _))
                {
                    cleared++;
                }
            }

            return cleared;
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

            Block graveBlock = blockSynchronizer.ResolveGraveBlock();
            if (graveBlock == null)
            {
                return;
            }

            bool debugCaptureRequested = IsDebugTracePending();
            List<string> debugCaptureLines = debugCaptureRequested ? new List<string>() : null;
            List<GraveInventorySnapshot> snapshots = snapshotter.SnapshotRelevantInventories(player, debugCaptureLines);
            int capturedStacks = snapshots.Sum(snapshot => snapshot?.Slots?.Count ?? 0);
            if (capturedStacks <= 0)
            {
                return;
            }

            string graveId = Guid.NewGuid().ToString("N");
            bool debugCycleActive = TryStartDebugTraceCycle(graveId);
            BlockPos deathPos = player.Entity.Pos.AsBlockPos.Copy();
            GravePlacementResult placement = placementService.FindPlacementPosition(player, deathPos, graveBlock);
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

            snapshotter.RemoveSnapshottedItems(player, snapshots, debugCycleActive);
            blockSynchronizer.Ensure(grave);

            if (placement.MovedOutsideForeignClaim)
            {
                string alertMessage = "You died inside a land claim you do not own, so your gravestone was placed outside the claim "
                    + $"at {gravePos.X}, {gravePos.Y}, {gravePos.Z}.";
                messenger.SendDual(player, alertMessage, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            }

            messenger.SendDual(player, "Your items were stored in a gravestone, they will be protected for 60 minutes", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
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

            if (!claimPolicy.CanPlayerClaim(byPlayer, grave, out string denialMessage))
            {
                messenger.SendDual(byPlayer, denialMessage, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
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

            Block graveBlock = blockSynchronizer.ResolveGraveBlock();
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
                blockSynchronizer.Ensure(grave);
                return;
            }

            if (!claimPolicy.CanPlayerClaim(byPlayer, grave, out _))
            {
                blockSynchronizer.Ensure(grave);
                return;
            }

            if (!TryRestoreGrave(grave.GraveId, byPlayer, bypassProtection: false, removeBlock: true, out string resultMessage))
            {
                blockSynchronizer.Ensure(grave);
                messenger.SendDual(byPlayer, resultMessage, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            messenger.SendDual(byPlayer, resultMessage, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
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

            if (claimPolicy.CanPlayerClaim(byPlayer, grave, out string denialMessage))
            {
                string claimMessage = IsPubliclyClaimable(grave)
                    ? "This gravestone can be claimed by anyone. Break it to restore its items."
                    : "This gravestone is owner-protected. You can break it to restore the items.";
                messenger.SendInfo(byPlayer, claimMessage, GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                return;
            }

            messenger.SendInfo(byPlayer, denialMessage, GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
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
            try
            {
                if (!graveManager.TryGetById(graveId, out GraveData grave) || grave == null)
                {
                    message = $"Gravestone '{graveId}' was not found.";
                    return false;
                }

                if (!bypassProtection && !claimPolicy.CanPlayerClaim(targetPlayer, grave, out message))
                {
                    return false;
                }

                if (!restorer.TryRestore(grave, targetPlayer, removeBlock, out _, out _))
                {
                    message = $"Failed to restore gravestone '{graveId}'. It remains in storage for safety.";
                    return false;
                }

                message = "You recovered your items";
                return true;
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

        private void CleanupAndReconcile()
        {
            if (api?.World?.Calendar == null || api.World.BlockAccessor == null)
            {
                return;
            }

            double nowDays = api.World.Calendar.TotalDays;
            double cleanupAfterDays = Math.Max(1d, config.GraveCleanupInGameDays);

            foreach (GraveData grave in graveManager.GetAll())
            {
                if (grave == null)
                {
                    continue;
                }

                if ((nowDays - grave.CreatedTotalDays) >= cleanupAfterDays)
                {
                    if (graveManager.Remove(grave.GraveId, out GraveData removed) && removed != null)
                    {
                        blockSynchronizer.RemoveIfPresent(removed);
                    }

                    continue;
                }

                blockSynchronizer.Ensure(grave);
            }
        }

        private void ReconcilePersistedGraves()
        {
            CleanupAndReconcile();
        }
    }
}
