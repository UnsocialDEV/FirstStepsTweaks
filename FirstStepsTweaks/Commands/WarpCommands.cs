using System;
using System.Collections.Generic;
using System.Text;
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
    public sealed class WarpCommands
    {
        private readonly ICoreServerAPI api;
        private readonly TeleportConfig teleportConfig;
        private readonly WarpStore warpStore;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;

        public WarpCommands(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            WarpStore warpStore,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.warpStore = warpStore;
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("setwarp")
                .WithDescription("Set a named warp at your current location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.Word("name"))
                .HandleWith(SetWarp);

            api.ChatCommands
                .Create("delwarp")
                .WithDescription("Delete a named warp")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.Word("name"))
                .HandleWith(DelWarp);

            api.ChatCommands
                .Create("warp")
                .WithDescription("Teleport to a named warp")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(api.ChatCommands.Parsers.Word("name"))
                .HandleWith(WarpTo);

            api.ChatCommands
                .Create("warps")
                .WithDescription("List all available warps")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ListWarps);
        }

        private TextCommandResult SetWarp(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string warpName = warpStore.NormalizeWarpName((string)args[0]);

            if (string.IsNullOrWhiteSpace(warpName))
            {
                messenger.SendDual(player, "Warp name cannot be empty.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            Dictionary<string, double[]> warps = warpStore.LoadWarps();
            var pos = player.Entity.Pos;
            bool updated = warps.ContainsKey(warpName);

            warps[warpName] = new[] { pos.X, pos.Y, pos.Z };
            warpStore.SaveWarps(warps);

            string action = updated ? "updated" : "set";
            messenger.SendDual(player, $"Warp '{warpName}' {action}.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult DelWarp(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string warpName = warpStore.NormalizeWarpName((string)args[0]);

            if (string.IsNullOrWhiteSpace(warpName))
            {
                messenger.SendDual(player, "Warp name cannot be empty.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            Dictionary<string, double[]> warps = warpStore.LoadWarps();
            if (!warps.Remove(warpName))
            {
                messenger.SendDual(player, $"Warp '{warpName}' does not exist.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            warpStore.SaveWarps(warps);
            messenger.SendDual(player, $"Warp '{warpName}' deleted.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult WarpTo(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string warpName = warpStore.NormalizeWarpName((string)args[0]);

            Dictionary<string, double[]> warps = warpStore.LoadWarps();
            if (!warps.TryGetValue(warpName, out double[] target) || target == null || target.Length != 3)
            {
                messenger.SendDual(player, $"Warp '{warpName}' does not exist.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            double targetX = target[0];
            double targetY = target[1];
            double targetZ = target[2];

            if (teleportConfig.WarmupSeconds > 0 && TeleportBypass.HasBypass(player))
            {
                TeleportBypass.NotifyBypassingCooldown(player, $"/warp {warpName} warmup");
                backLocationStore.RecordCurrentLocation(player);
                player.Entity.TeleportToDouble(targetX, targetY, targetZ);
                messenger.SendIngameError(player, "no_permission", $"Teleported to warp '{warpName}'.");
                return TextCommandResult.Success();
            }

            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Teleporting to warp '{warpName}' in {teleportConfig.WarmupSeconds} seconds. Do not move.",
                CountdownTemplate = $"Teleporting to warp '{warpName}' in {{0}}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = $"Teleported to warp '{warpName}'.",
                BypassContext = $"/warp {warpName} warmup",
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
                    player.Entity.TeleportToDouble(targetX, targetY, targetZ);
                }
            });

            return TextCommandResult.Success();
        }

        private TextCommandResult ListWarps(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            Dictionary<string, double[]> warps = warpStore.LoadWarps();

            if (warps.Count == 0)
            {
                messenger.SendDual(player, "No warps have been set.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Warps ({warps.Count}):");

            foreach (var pair in warps)
            {
                if (pair.Value == null || pair.Value.Length != 3) continue;
                sb.AppendLine($"- {pair.Key}: {pair.Value[0]:0.##}, {pair.Value[1]:0.##}, {pair.Value[2]:0.##}");
            }

            messenger.SendInfo(player, sb.ToString().TrimEnd(), GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            return TextCommandResult.Success();
        }
    }
}
