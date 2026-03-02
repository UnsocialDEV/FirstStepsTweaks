using System;
using System.Collections.Generic;
using System.Text;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class WarpCommands
    {
        private const string WarpDataKey = "fst_warps";

        private static TeleportConfig teleportConfig = new TeleportConfig();

        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            teleportConfig = config?.Teleport ?? new TeleportConfig();

            api.ChatCommands
                .Create("setwarp")
                .WithDescription("Set a named warp at your current location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.Word("name"))
                .HandleWith(args => SetWarp(api, args));

            api.ChatCommands
                .Create("delwarp")
                .WithDescription("Delete a named warp")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.Word("name"))
                .HandleWith(args => DelWarp(api, args));

            api.ChatCommands
                .Create("warp")
                .WithDescription("Teleport to a named warp")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(api.ChatCommands.Parsers.Word("name"))
                .HandleWith(args => WarpTo(api, args));

            api.ChatCommands
                .Create("warps")
                .WithDescription("List all available warps")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => ListWarps(api, args));
        }

        private static TextCommandResult SetWarp(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string warpName = NormalizeWarpName((string)args[0]);

            if (string.IsNullOrWhiteSpace(warpName))
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "Warp name cannot be empty.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            Dictionary<string, double[]> warps = LoadWarps(api);
            var pos = player.Entity.Pos;
            bool updated = warps.ContainsKey(warpName);

            warps[warpName] = new[] { pos.X, pos.Y, pos.Z };
            SaveWarps(api, warps);

            string action = updated ? "updated" : "set";
            player.SendMessage(GlobalConstants.InfoLogChatGroup, $"Warp '{warpName}' {action}.", EnumChatType.CommandSuccess);
            return TextCommandResult.Success();
        }

        private static TextCommandResult DelWarp(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string warpName = NormalizeWarpName((string)args[0]);

            if (string.IsNullOrWhiteSpace(warpName))
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "Warp name cannot be empty.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            Dictionary<string, double[]> warps = LoadWarps(api);
            if (!warps.Remove(warpName))
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, $"Warp '{warpName}' does not exist.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            SaveWarps(api, warps);
            player.SendMessage(GlobalConstants.InfoLogChatGroup, $"Warp '{warpName}' deleted.", EnumChatType.CommandSuccess);
            return TextCommandResult.Success();
        }

        private static TextCommandResult WarpTo(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string warpName = NormalizeWarpName((string)args[0]);

            Dictionary<string, double[]> warps = LoadWarps(api);
            if (!warps.TryGetValue(warpName, out double[] target) || target == null || target.Length != 3)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, $"Warp '{warpName}' does not exist.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            double targetX = target[0];
            double targetY = target[1];
            double targetZ = target[2];

            double startX = player.Entity.Pos.X;
            double startY = player.Entity.Pos.Y;
            double startZ = player.Entity.Pos.Z;

            int secondsRemaining = teleportConfig.WarmupSeconds;
            long listenerId = 0;

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"Teleporting to warp '{warpName}' in {teleportConfig.WarmupSeconds} seconds. Do not move.",
                EnumChatType.Notification
            );

            listenerId = api.Event.RegisterGameTickListener((dt) =>
            {
                if (player?.Entity == null)
                {
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                double dx = Math.Abs(player.Entity.Pos.X - startX);
                double dy = Math.Abs(player.Entity.Pos.Y - startY);
                double dz = Math.Abs(player.Entity.Pos.Z - startZ);

                if (dx > teleportConfig.CancelMoveThreshold || dy > teleportConfig.CancelMoveThreshold || dz > teleportConfig.CancelMoveThreshold)
                {
                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        "Teleport cancelled because you moved.",
                        EnumChatType.CommandError
                    );
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                if (secondsRemaining > 0)
                {
                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        $"Teleporting to warp '{warpName}' in {secondsRemaining}...",
                        EnumChatType.Notification
                    );
                    secondsRemaining--;
                }
                else
                {
                    BackCommands.RecordCurrentLocation(player);
                    player.Entity.TeleportToDouble(targetX, targetY, targetZ);
                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        $"Teleported to warp '{warpName}'.",
                        EnumChatType.CommandSuccess
                    );
                    api.Event.UnregisterGameTickListener(listenerId);
                }
            }, teleportConfig.TickIntervalMs);

            return TextCommandResult.Success();
        }

        private static TextCommandResult ListWarps(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            Dictionary<string, double[]> warps = LoadWarps(api);

            if (warps.Count == 0)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "No warps have been set.", EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Warps ({warps.Count}):");

            foreach (var pair in warps)
            {
                if (pair.Value == null || pair.Value.Length != 3) continue;
                sb.AppendLine($"- {pair.Key}: {pair.Value[0]:0.##}, {pair.Value[1]:0.##}, {pair.Value[2]:0.##}");
            }

            player.SendMessage(GlobalConstants.InfoLogChatGroup, sb.ToString().TrimEnd(), EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private static Dictionary<string, double[]> LoadWarps(ICoreServerAPI api)
        {
            return api.WorldManager.SaveGame.GetData<Dictionary<string, double[]>>(WarpDataKey)
                ?? new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        }

        private static void SaveWarps(ICoreServerAPI api, Dictionary<string, double[]> warps)
        {
            api.WorldManager.SaveGame.StoreData(WarpDataKey, warps);
        }

        private static string NormalizeWarpName(string warpName)
        {
            return (warpName ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
