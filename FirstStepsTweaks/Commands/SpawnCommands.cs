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
    public sealed class SpawnCommands
    {
        private readonly ICoreServerAPI api;
        private readonly TeleportConfig teleportConfig;
        private readonly SpawnStore spawnStore;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly PlayerTeleportWarmupResolver warmupResolver;

        public SpawnCommands(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            SpawnStore spawnStore,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            PlayerTeleportWarmupResolver warmupResolver)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.spawnStore = spawnStore;
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
            this.warmupResolver = warmupResolver;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("setspawn")
                .WithDescription("Sets server spawn to your current location")
                .RequiresPlayer()
                .RequiresPrivilege(StaffPrivilegeCatalog.AdminPrivilege)
                .HandleWith(SetSpawn);

            api.ChatCommands
                .Create("spawn")
                .WithDescription("Teleport to server spawn")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Spawn);
        }

        private TextCommandResult SetSpawn(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            spawnStore.SetSpawn(player);
            messenger.SendDual(player, "Spawn position set.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult Spawn(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            int effectiveWarmupSeconds = warmupResolver.Resolve(player, teleportConfig);

            if (!spawnStore.TryGetSpawn(out var target))
            {
                messenger.SendDual(player, "Spawn has not been set yet.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            if (effectiveWarmupSeconds > 0 && TeleportBypass.HasBypass(player))
            {
                TeleportBypass.NotifyBypassingCooldown(player, "/spawn warmup");
                backLocationStore.RecordCurrentLocation(player);
                player.Entity.TeleportToDouble(target.X, target.Y, target.Z);
                messenger.SendIngameError(player, "no_permission", "Teleported to spawn.");
                return TextCommandResult.Success();
            }

            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Teleporting you in {effectiveWarmupSeconds} seconds. Do not move.",
                CountdownTemplate = "Teleporting to spawn in {0}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = "Teleported to spawn.",
                BypassContext = "/spawn warmup",
                WarmupSeconds = effectiveWarmupSeconds,
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
    }
}
