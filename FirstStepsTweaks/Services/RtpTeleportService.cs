using System;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
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
        private readonly TeleportConfig teleportConfig;
        private readonly RtpConfig rtpConfig;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly IPlayerTeleporter playerTeleporter;
        private readonly PlayerTeleportWarmupResolver warmupResolver;
        private readonly RtpCooldownStore cooldownStore;
        private readonly IRtpDestinationResolver destinationResolver;

        public RtpTeleportService(
            FirstStepsTweaksConfig config,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            IPlayerTeleporter playerTeleporter,
            PlayerTeleportWarmupResolver warmupResolver,
            RtpCooldownStore cooldownStore,
            IRtpDestinationResolver destinationResolver)
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
        }

        public void Execute(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            int effectiveWarmupSeconds = warmupResolver.Resolve(player, teleportConfig);
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
                        return;
                    }
                }
            }

            if (!destinationResolver.TryResolveDestination(player, out Vec3d destination))
            {
                messenger.SendInfo(player, "Failed to find a safe random destination. Try again.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandError);
                messenger.SendGeneral(player, "Failed to find a safe random destination. Try again.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                return;
            }

            if (rtpConfig.UseWarmup && effectiveWarmupSeconds > 0)
            {
                StartWarmupTeleport(player, destination, effectiveWarmupSeconds);
                return;
            }

            backLocationStore.RecordCurrentLocation(player);
            playerTeleporter.Teleport(player, destination);
            messenger.SendInfo(player, "Teleported to a random location.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
            messenger.SendGeneral(player, "Teleported to a random location.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
            cooldownStore.SetLastUse(player.PlayerUID, nowMs);
        }

        private void StartWarmupTeleport(IServerPlayer player, Vec3d destination, int warmupSeconds)
        {
            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Teleporting to a random location in {warmupSeconds} seconds. Do not move.",
                CountdownTemplate = "Teleporting in {0}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = "Teleported to a random location.",
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
    }
}
