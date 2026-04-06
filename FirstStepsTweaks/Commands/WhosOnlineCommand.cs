using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using FirstStepsTweaks.Services;

namespace FirstStepsTweaks.Commands
{
    public sealed class WhosOnlineCommand
    {
        private readonly ICoreServerAPI api;
        private readonly IStaffStatusReader staffStatusReader;

        public WhosOnlineCommand(ICoreServerAPI api, IStaffStatusReader staffStatusReader)
        {
            this.api = api;
            this.staffStatusReader = staffStatusReader;
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

            var sortedPlayers = players
                .Cast<IServerPlayer>()
                .OrderBy(player => GetSortOrder(staffStatusReader.GetLevel(player)))
                .ThenBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string[] lines = sortedPlayers
                .Select((player, index) =>
                {
                    string staffTag = GetStaffTag(staffStatusReader.GetLevel(player));
                    int pingMs = (int)Math.Max(0, Math.Round(player.Ping * 1000d));
                    return $"{index + 1}. {player.PlayerName}{staffTag} ({pingMs}ms)";
                })
                .ToArray();

            string header = $"Online players ({sortedPlayers.Count}):";
            string playerList = string.Join(Environment.NewLine, lines);
            caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"{header}{Environment.NewLine}{playerList}", EnumChatType.CommandSuccess);
            caller.SendMessage(GlobalConstants.GeneralChatGroup, $"{header}{Environment.NewLine}{playerList}", EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private static int GetSortOrder(StaffLevel level)
        {
            return level switch
            {
                StaffLevel.Admin => 0,
                StaffLevel.Moderator => 1,
                _ => 2
            };
        }

        private static string GetStaffTag(StaffLevel level)
        {
            return level switch
            {
                StaffLevel.Admin => " [ADMIN]",
                StaffLevel.Moderator => " [MOD]",
                _ => string.Empty
            };
        }
    }
}
