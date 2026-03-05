using System;
using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class UtilityCommands
    {
        private static UtilityConfig utilityConfig = new UtilityConfig();
        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            utilityConfig = config?.Utility ?? new UtilityConfig();

            api.ChatCommands
                .Create("whosonline")
                .WithDescription("Shows the current player list")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => WhosOnline(api, args));

            api.ChatCommands
                .Create("wind")
                .WithDescription("Shows the current wind speed at your location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Wind(api, args));
        }

        private static string GetWindCategory(float wind)
        {
            if (wind >= utilityConfig.HurricaneThreshold) return "Hurricane";
            if (wind >= utilityConfig.StormThreshold) return "Storm";
            if (wind >= utilityConfig.StrongWindThreshold) return "Strong Wind";
            if (wind >= utilityConfig.BreezyThreshold) return "Breezy";
            return "Calm";
        }

        private static TextCommandResult Wind(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            Vec3d windVec = api.World.BlockAccessor.GetWindSpeedAt(player.Entity.Pos.XYZ);
            float windStrength = (float)windVec.Length();

            string category = GetWindCategory(windStrength);

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"Wind: {windStrength:0.00} ({category})",
                EnumChatType.CommandSuccess
            );

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                $"Wind: {windStrength:0.00} ({category})",
                EnumChatType.Notification
            );

            return TextCommandResult.Success();
        }

        private static TextCommandResult WhosOnline(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var players = api.World.AllOnlinePlayers;
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;

            if (players == null || players.Length == 0)
            {
                caller.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "No players online.",
                    EnumChatType.Notification
                );

                caller.SendMessage(
                    GlobalConstants.GeneralChatGroup,
                    "No players online.",
                    EnumChatType.Notification
                );
                return TextCommandResult.Success();
            }

            var adminNames = new HashSet<string>(utilityConfig.AdminPlayerNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            var sortedPlayers = players
                .Cast<IServerPlayer>()
                .OrderByDescending(p => adminNames.Contains(p.PlayerName))
                .ThenBy(p => p.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string[] lines = sortedPlayers
                .Select((player, index) =>
                {
                    bool isAdmin = adminNames.Contains(player.PlayerName);
                    string adminTag = isAdmin ? " [ADMIN]" : string.Empty;
                    return $"{index + 1}. {player.PlayerName}{adminTag} ({player.Ping}ms)";
                })
                .ToArray();

            string header = $"Online players ({sortedPlayers.Count}):";
            string playerList = string.Join(Environment.NewLine, lines);

            caller.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"{header}{Environment.NewLine}{playerList}",
                EnumChatType.CommandSuccess
            );

            caller.SendMessage(
                GlobalConstants.GeneralChatGroup,
                $"{header}{Environment.NewLine}{playerList}",
                EnumChatType.Notification
            );

            return TextCommandResult.Success();
        }
    }
}