using System;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class HomeCommands
    {
        private readonly ICoreServerAPI api;
        private readonly TeleportConfig teleportConfig;
        private readonly HomeStore homeStore;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;

        public HomeCommands(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            HomeStore homeStore,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.homeStore = homeStore;
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("sethome")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(SetHome);

            api.ChatCommands
                .Create("home")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Home);

            api.ChatCommands
                .Create("delhome")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(DelHome);
        }

        private TextCommandResult SetHome(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            if (homeStore.HasHome(player))
            {
                messenger.SendDual(player, "You already have a home set. Use /delhome first.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            homeStore.SetHome(player);
            messenger.SendDual(player, "Home set.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult Home(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            if (!homeStore.TryGetHome(player, out var target))
            {
                messenger.SendDual(player, "You do not have a valid home set.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            if (teleportConfig.WarmupSeconds > 0 && TeleportBypass.HasBypass(player))
            {
                TeleportBypass.NotifyBypassingCooldown(player, "/home warmup");
                backLocationStore.RecordCurrentLocation(player);
                player.Entity.TeleportToDouble(target.X, target.Y, target.Z);
                messenger.SendIngameError(player, "no_permission", "Teleported home.");
                return TextCommandResult.Success();
            }

            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Teleporting you home in {teleportConfig.WarmupSeconds} seconds. Do not move.",
                CountdownTemplate = "Teleporting in {0}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = "Teleported home.",
                BypassContext = "/home warmup",
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
                    player.Entity.TeleportToDouble(target.X, target.Y, target.Z);
                }
            });

            return TextCommandResult.Success();
        }

        private TextCommandResult DelHome(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            if (!homeStore.HasHome(player))
            {
                messenger.SendDual(player, "You do not have a home set.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            homeStore.ClearHome(player);
            messenger.SendInfo(player, "Home deleted.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
            messenger.SendGeneral(player, "Home deleted.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }
    }
}
