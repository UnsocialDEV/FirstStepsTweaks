using System;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpTeleportService
    {
        private const int ChunkLoadRetryDelayMs = 750;
        private const int MaxChunkLoadRetriesPerBatch = 3;

        private readonly TeleportConfig teleportConfig;
        private readonly RtpConfig rtpConfig;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly IPlayerTeleporter playerTeleporter;
        private readonly PlayerTeleportWarmupResolver warmupResolver;
        private readonly RtpCooldownStore cooldownStore;
        private readonly IRtpDestinationResolver destinationResolver;
        private readonly IDelayedPlayerActionScheduler delayedPlayerActionScheduler;
        private readonly ILogger logger;

        public RtpTeleportService(
            FirstStepsTweaksConfig config,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            IPlayerTeleporter playerTeleporter,
            PlayerTeleportWarmupResolver warmupResolver,
            RtpCooldownStore cooldownStore,
            IRtpDestinationResolver destinationResolver,
            IDelayedPlayerActionScheduler delayedPlayerActionScheduler,
            ILogger logger = null)
        {
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            rtpConfig = config?.Rtp ?? new RtpConfig();
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
            this.playerTeleporter = playerTeleporter;
            this.warmupResolver = warmupResolver;
            this.cooldownStore = cooldownStore;
            this.destinationResolver = destinationResolver;
            this.delayedPlayerActionScheduler = delayedPlayerActionScheduler;
            this.logger = logger;
        }

        public void Execute(IServerPlayer player)
        {
            Execute(player, null);
        }

        private void Execute(IServerPlayer player, RtpSearchSession searchSession)
        {
            if (player == null)
            {
                return;
            }

            bool initialSearch = searchSession == null;
            if (initialSearch && !CanStartSearch(player))
            {
                return;
            }

            if (initialSearch)
            {
                messenger.SendInfo(player, "Searching for a safe place to drop you. This can take a few seconds.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(player, "Searching for a safe place to drop you. This can take a few seconds.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
            }

            RtpDestinationResolutionResult resolution = destinationResolver.ResolveDestination(player, searchSession);
            if (resolution.Success)
            {
                CompleteTeleport(player, resolution.Destination);
                return;
            }

            RtpSearchSession session = resolution.SearchSession;
            if (session != null && resolution.PendingChunkCount > 0 && session.BatchRetryCount < MaxChunkLoadRetriesPerBatch && delayedPlayerActionScheduler != null)
            {
                delayedPlayerActionScheduler.Schedule(
                    player.PlayerUID,
                    ChunkLoadRetryDelayMs,
                    retryPlayer => Execute(retryPlayer, session.AdvanceRetry()));
                return;
            }

            if (session != null && session.HasNextBatch)
            {
                Execute(player, session.AdvanceBatch(resolution.PendingChunkCount, resolution.UnsafeTerrainCount, resolution.ClaimRejectedCount));
                return;
            }

            if (session != null)
            {
                LogFinalFailure(player.PlayerUID, session, resolution);
            }

            messenger.SendInfo(player, "Couldn't find a safe place this time. Please try again in a moment.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandError);
            messenger.SendGeneral(player, "Couldn't find a safe place this time. Please try again in a moment.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
        }

        private bool CanStartSearch(IServerPlayer player)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bool hasCooldownBypass = TeleportBypass.HasBypass(player);

            if (rtpConfig.CooldownSeconds > 0 && cooldownStore.TryGetLastUse(player.PlayerUID, out long lastRtpMs))
            {
                long remainingMs = (rtpConfig.CooldownSeconds * 1000L) - (nowMs - lastRtpMs);
                if (remainingMs > 0)
                {
                    if (hasCooldownBypass)
                    {
                        TeleportBypass.NotifyBypassingRtpCooldown(player);
                    }
                    else
                    {
                        int remainingSeconds = (int)Math.Ceiling(remainingMs / 1000d);
                        messenger.SendInfo(player, $"You must wait {remainingSeconds}s before using /rtp again.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandError);
                        messenger.SendGeneral(player, $"You must wait {remainingSeconds}s before using /rtp again.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                        return false;
                    }
                }
            }

            return true;
        }

        private void CompleteTeleport(IServerPlayer player, Vec3d destination)
        {
            int effectiveWarmupSeconds = warmupResolver.Resolve(player, teleportConfig);
            if (rtpConfig.UseWarmup && effectiveWarmupSeconds > 0)
            {
                StartWarmupTeleport(player, destination, effectiveWarmupSeconds);
                return;
            }

            backLocationStore.RecordCurrentLocation(player);
            playerTeleporter.Teleport(player, destination);
            messenger.SendInfo(player, "Teleported you to a safe random location.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
            messenger.SendGeneral(player, "Teleported you to a safe random location.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
            cooldownStore.SetLastUse(player.PlayerUID, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        private void StartWarmupTeleport(IServerPlayer player, Vec3d destination, int warmupSeconds)
        {
            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Found a safe spot. Teleporting in {warmupSeconds} seconds. Don't move.",
                CountdownTemplate = "Teleporting in {0}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = "Teleported you to a safe random location.",
                BypassContext = "/rtp cooldown",
                WarmupSeconds = warmupSeconds,
                TickIntervalMs = teleportConfig.TickIntervalMs,
                CancelMoveThreshold = teleportConfig.CancelMoveThreshold,
                WarmupInfoChatType = (int)EnumChatType.CommandSuccess,
                WarmupGeneralGroupId = GlobalConstants.GeneralChatGroup,
                WarmupGeneralChatType = (int)EnumChatType.Notification,
                CancelInfoChatType = (int)EnumChatType.CommandError,
                CancelGeneralChatType = (int)EnumChatType.Notification,
                AllowBypass = false,
                ExecuteTeleport = () =>
                {
                    backLocationStore.RecordCurrentLocation(player);
                    playerTeleporter.Teleport(player, destination);
                    cooldownStore.SetLastUse(player.PlayerUID, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }
            });
        }

        private void LogFinalFailure(string playerUid, RtpSearchSession session, RtpDestinationResolutionResult resolution)
        {
            int totalPendingChunks = session.PendingChunkCount + resolution.PendingChunkCount;
            int totalUnsafeTerrain = session.UnsafeTerrainCount + resolution.UnsafeTerrainCount;
            int totalClaimRejected = session.ClaimRejectedCount + resolution.ClaimRejectedCount;
            int attemptedBatches = session.CompletedBatchCount + (session.HasCurrentBatch ? 1 : 0);

            logger?.Warning(
                $"[FirstStepsTweaks][RTP] Failed to find destination for player '{playerUid}'. " +
                $"dimension={session.Dimension}, currentPosition={FormatVec3(session.CurrentPosition)}, center={FormatVec2(session.Center)}, " +
                $"batchesTried={attemptedBatches}/{session.TotalBatchCount}, pendingChunks={totalPendingChunks}, " +
                $"unsafeTerrain={totalUnsafeTerrain}, claimRejected={totalClaimRejected}.");
        }

        private static string FormatVec2(Vec2d value)
        {
            return value == null ? "<null>" : $"{value.X:0.##},{value.Y:0.##}";
        }

        private static string FormatVec3(Vec3d value)
        {
            return value == null ? "<null>" : $"{value.X:0.##},{value.Y:0.##},{value.Z:0.##}";
        }
    }
}
