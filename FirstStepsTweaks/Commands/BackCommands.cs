using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class BackCommands
    {
        private readonly ICoreServerAPI api;
        private readonly TeleportConfig teleportConfig;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly PlayerTeleportWarmupResolver warmupResolver;

        public BackCommands(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            PlayerTeleportWarmupResolver warmupResolver)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
            this.warmupResolver = warmupResolver;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("back")
                .WithDescription("Teleport back to your last location")
                .RequiresPlayer()
                .RequiresPrivilege("firststepstweaks.back")
                .HandleWith(Back);
        }

        public void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (!(entity is EntityPlayer entityPlayer))
            {
                return;
            }

            IServerPlayer player = entityPlayer.Player as IServerPlayer;
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            backLocationStore.Set(player.PlayerUID, new Vec3d(player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z));
        }

        private TextCommandResult Back(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            int effectiveWarmupSeconds = warmupResolver.Resolve(player, teleportConfig);

            if (!backLocationStore.TryGet(player.PlayerUID, out Vec3d lastLocation))
            {
                messenger.SendInfo(player, "No previous location recorded.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(player, "No previous location recorded.", GlobalConstants.AllChatGroups, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            if (effectiveWarmupSeconds > 0 && TeleportBypass.HasBypass(player))
            {
                TeleportBypass.NotifyBypassingCooldown(player, "/back warmup");

                Vec3d currentLocation = new Vec3d(player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);
                player.Entity.TeleportToDouble(lastLocation.X, lastLocation.Y, lastLocation.Z);
                backLocationStore.Set(player.PlayerUID, currentLocation);

                messenger.SendIngameError(player, "no_permission", "Teleported to your last location.");
                return TextCommandResult.Success();
            }

            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Teleporting to your previous location in {effectiveWarmupSeconds} seconds. Do not move.",
                CountdownTemplate = "Teleporting to your last location in {0}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = "Teleported to your last location.",
                BypassContext = "/back warmup",
                WarmupSeconds = effectiveWarmupSeconds,
                TickIntervalMs = teleportConfig.TickIntervalMs,
                CancelMoveThreshold = teleportConfig.CancelMoveThreshold,
                WarmupInfoChatType = (int)EnumChatType.CommandSuccess,
                WarmupGeneralGroupId = GlobalConstants.AllChatGroups,
                WarmupGeneralChatType = (int)EnumChatType.Notification,
                CancelInfoChatType = (int)EnumChatType.CommandSuccess,
                CancelGeneralChatType = (int)EnumChatType.Notification,
                ExecuteTeleport = () =>
                {
                    Vec3d currentLocation = new Vec3d(player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);
                    player.Entity.TeleportToDouble(lastLocation.X, lastLocation.Y, lastLocation.Z);
                    backLocationStore.Set(player.PlayerUID, currentLocation);
                }
            });

            return TextCommandResult.Success();
        }
    }
}
