using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public class DiscordCommands
    {
        private static DiscordCommandConfig discordCommandConfig = new DiscordCommandConfig();

        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            discordCommandConfig = config?.DiscordCommand ?? new DiscordCommandConfig();

            api.ChatCommands
                .Create("discord")
                .WithDescription("Displays the Discord invite")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Discord(api, args));
        }

        private static TextCommandResult Discord(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            var message = discordCommandConfig.InviteMessage;

            player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.AllGroups);
            player.SendMessage(GlobalConstants.InfoLogChatGroup, message, EnumChatType.AllGroups);

            return TextCommandResult.Success();
        }
    }
}
