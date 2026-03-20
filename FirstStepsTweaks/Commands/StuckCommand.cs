using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class StuckCommand
    {
        private readonly ICoreServerAPI api;
        private readonly TeleportConfig teleportConfig;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly LandClaimEscapeService escapeService;

        public StuckCommand(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            LandClaimEscapeService escapeService)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
            this.escapeService = escapeService;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("stuck")
                .WithDescription("Teleport to the nearest safe block outside all land claims")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Execute);
        }

        private TextCommandResult Execute(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (!escapeService.TryResolveDestination(player, out Vec3d destination, out string message))
            {
                messenger.SendDual(player, message, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            if (teleportConfig.WarmupSeconds > 0 && TeleportBypass.HasBypass(player))
            {
                TeleportBypass.NotifyBypassingCooldown(player, "/stuck warmup");
                backLocationStore.RecordCurrentLocation(player);
                player.Entity.TeleportToDouble(destination.X, destination.Y, destination.Z);
                messenger.SendIngameError(player, "no_permission", "Teleported outside the land claim.");
                return TextCommandResult.Success();
            }

            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Teleporting you outside the land claim in {teleportConfig.WarmupSeconds} seconds. Do not move.",
                CountdownTemplate = "Teleporting outside the land claim in {0}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = "Teleported outside the land claim.",
                BypassContext = "/stuck warmup",
                WarmupSeconds = teleportConfig.WarmupSeconds,
                TickIntervalMs = teleportConfig.TickIntervalMs,
                CancelMoveThreshold = teleportConfig.CancelMoveThreshold,
                WarmupInfoChatType = (int)EnumChatType.CommandSuccess,
                WarmupGeneralGroupId = GlobalConstants.GeneralChatGroup,
                WarmupGeneralChatType = (int)EnumChatType.Notification,
                CancelInfoChatType = (int)EnumChatType.CommandSuccess,
                CancelGeneralChatType = (int)EnumChatType.Notification,
                ExecuteTeleport = () =>
                {
                    backLocationStore.RecordCurrentLocation(player);
                    player.Entity.TeleportToDouble(destination.X, destination.Y, destination.Z);
                }
            });

            return TextCommandResult.Success();
        }
    }
}
