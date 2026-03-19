using System;
using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class WhosOnlineCommand
    {
        private readonly ICoreServerAPI api;
        private readonly UtilityConfig utilityConfig;

        public WhosOnlineCommand(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            this.api = api;
            utilityConfig = config?.Utility ?? new UtilityConfig();
        }

        public void Register()
        {
            api.ChatCommands
                .Create("whosonline")
                .WithDescription("Shows the current player list")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(WhosOnline);
        }

        private TextCommandResult WhosOnline(TextCommandCallingArgs args)
        {
            var players = api.World.AllOnlinePlayers;
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;

            if (players == null || players.Length == 0)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "No players online.", EnumChatType.Notification);
                caller.SendMessage(GlobalConstants.GeneralChatGroup, "No players online.", EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            var adminNames = new HashSet<string>(utilityConfig.AdminPlayerNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var sortedPlayers = players
                .Cast<IServerPlayer>()
                .OrderByDescending(player => adminNames.Contains(player.PlayerName))
                .ThenBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string[] lines = sortedPlayers
                .Select((player, index) =>
                {
                    bool isAdmin = adminNames.Contains(player.PlayerName);
                    string adminTag = isAdmin ? " [ADMIN]" : string.Empty;
                    int pingMs = (int)Math.Max(0, Math.Round(player.Ping * 1000d));
                    return $"{index + 1}. {player.PlayerName}{adminTag} ({pingMs}ms)";
                })
                .ToArray();

            string header = $"Online players ({sortedPlayers.Count}):";
            string playerList = string.Join(Environment.NewLine, lines);
            caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"{header}{Environment.NewLine}{playerList}", EnumChatType.CommandSuccess);
            caller.SendMessage(GlobalConstants.GeneralChatGroup, $"{header}{Environment.NewLine}{playerList}", EnumChatType.Notification);
            return TextCommandResult.Success();
        }
    }
}
