using System;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

/* ***NOT IMPLEMENTED***
 * This one is a bit fucky, doesnt seem to want to work if you change the min/max radius
 * tbh, i dont even know if i want to implement it anyway
 */

namespace FirstStepsTweaks.Commands
{
    public sealed class RtpCommands
    {
        private readonly ICoreServerAPI api;
        private readonly Random random = new Random();
        private readonly TeleportConfig teleportConfig;
        private readonly RtpConfig rtpConfig;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly RtpCooldownStore cooldownStore;

        public RtpCommands(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            RtpCooldownStore cooldownStore)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            rtpConfig = config?.Rtp ?? new RtpConfig();
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
            this.cooldownStore = cooldownStore;
        }

        public void Register()
        {
            /* commented out so its not registered and cant be used
            api.ChatCommands
                .Create("rtp")
                .WithDescription("Teleport to a random location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(RandomTeleport);
            */
        }

        private TextCommandResult RandomTeleport(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bool hasBypassCooldown = TeleportBypass.HasBypass(player);

            if (rtpConfig.CooldownSeconds > 0 && cooldownStore.TryGetLastUse(player.PlayerUID, out long lastRtpMs))
            {
                long remainingMs = (rtpConfig.CooldownSeconds * 1000L) - (nowMs - lastRtpMs);
                if (remainingMs > 0)
                {
                    int remainingSeconds = (int)Math.Ceiling(remainingMs / 1000d);
                    if (hasBypassCooldown)
                    {
                        TeleportBypass.NotifyBypassingCooldown(player, $"/rtp wait {remainingSeconds}s");
                    }
                    else
                    {
                        messenger.SendInfo(player, $"You must wait {remainingSeconds}s before using /rtp again.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandError);
                        messenger.SendGeneral(player, $"You must wait {remainingSeconds}s before using /rtp again.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                        return TextCommandResult.Success();
                    }
                }
            }

            Vec3d destination = FindDestination(player);
            if (destination == null)
            {
                messenger.SendInfo(player, "Failed to find a safe random destination. Try again.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandError);
                messenger.SendGeneral(player, "Failed to find a safe random destination. Try again.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            if (rtpConfig.UseWarmup && teleportConfig.WarmupSeconds > 0 && !hasBypassCooldown)
            {
                StartWarmupTeleport(player, destination);
            }
            else
            {
                if (rtpConfig.UseWarmup && teleportConfig.WarmupSeconds > 0 && hasBypassCooldown)
                {
                    TeleportBypass.NotifyBypassingCooldown(player, "/rtp warmup");
                }

                backLocationStore.RecordCurrentLocation(player);
                player.Entity.TeleportToDouble(destination.X, destination.Y, destination.Z);
                messenger.SendInfo(player, "Teleported to a random location.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(player, "Teleported to a random location.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                cooldownStore.SetLastUse(player.PlayerUID, nowMs);
            }

            return TextCommandResult.Success();
        }

        private Vec3d FindDestination(IServerPlayer player)
        {
            int attempts = Math.Max(1, rtpConfig.MaxAttempts);
            int minRadius = Math.Max(0, rtpConfig.MinRadius);
            int maxRadius = Math.Max(minRadius, rtpConfig.MaxRadius);
            int horizontalChecksPerAttempt = 4;

            double centerX = rtpConfig.UsePlayerPositionAsCenter ? player.Entity.Pos.X : 0;
            double centerZ = rtpConfig.UsePlayerPositionAsCenter ? player.Entity.Pos.Z : 0;

            for (int i = 0; i < attempts; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2;
                double distance = minRadius + (random.NextDouble() * (maxRadius - minRadius));

                int baseX = (int)Math.Round(centerX + Math.Cos(angle) * distance);
                int baseZ = (int)Math.Round(centerZ + Math.Sin(angle) * distance);

                for (int sample = 0; sample < horizontalChecksPerAttempt; sample++)
                {
                    // Try a tiny local spread around the sampled point so one tree/cave column does not waste the whole attempt.
                    int x = baseX + random.Next(-4, 5);
                    int z = baseZ + random.Next(-4, 5);
                    Vec3d safeDestination = FindSafeDestinationInColumn(x, z);
                    if (safeDestination != null)
                    {
                        return safeDestination;
                    }
                }
            }

            return null;
        }

        private Vec3d FindSafeDestinationInColumn(int x, int z)
        {
            int terrainHeight = api.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
            if (terrainHeight <= 1)
            {
                return null;
            }

            // Terrain map height can hit treetops/leaves. Scan downward to find a true walkable surface.
            int scanStartY = terrainHeight + 6;
            int scanEndY = Math.Max(2, terrainHeight - 20);

            for (int y = scanStartY; y >= scanEndY; y--)
            {
                BlockPos feetPos = new BlockPos(x, y, z);
                BlockPos headPos = new BlockPos(x, y + 1, z);
                BlockPos groundPos = new BlockPos(x, y - 1, z);

                Block feetBlock = api.World.BlockAccessor.GetBlock(feetPos);
                Block headBlock = api.World.BlockAccessor.GetBlock(headPos);
                Block groundBlock = api.World.BlockAccessor.GetBlock(groundPos);

                if (feetBlock == null || headBlock == null || groundBlock == null) continue;
                if (!IsPassableTeleportSpace(feetBlock) || !IsPassableTeleportSpace(headBlock)) continue;
                if (!IsSafeTeleportGround(groundBlock)) continue;

                return new Vec3d(x + 0.5, y, z + 0.5);
            }

            return null;
        }

        private static bool IsPassableTeleportSpace(Block block)
        {
            // Allow air and highly-replaceable non-solid blocks (tallgrass, flowers, etc.) as valid player space.
            return block.BlockId == 0 || block.Replaceable >= 6000;
        }

        private static bool IsSafeTeleportGround(Block block)
        {
            // Ground must exist and be reasonably solid; exclude highly-replaceable blocks (flora/snow layers).
            return block.BlockId != 0 && block.Replaceable < 6000;
        }

        private void StartWarmupTeleport(IServerPlayer player, Vec3d destination)
        {
            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Teleporting to a random location in {teleportConfig.WarmupSeconds} seconds. Do not move.",
                CountdownTemplate = "Teleporting in {0}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = "Teleported to a random location.",
                BypassContext = "/rtp warmup",
                WarmupSeconds = teleportConfig.WarmupSeconds,
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
                    player.Entity.TeleportToDouble(destination.X, destination.Y, destination.Z);
                    cooldownStore.SetLastUse(player.PlayerUID, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }
            });
        }
    }
}
