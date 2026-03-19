using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class DiscordCommands
    {
        private readonly ICoreServerAPI api;
        private readonly DiscordCommandConfig discordCommandConfig;
        private readonly IPlayerMessenger messenger;

        public DiscordCommands(ICoreServerAPI api, FirstStepsTweaksConfig config, IPlayerMessenger messenger)
        {
            this.api = api;
            discordCommandConfig = config?.DiscordCommand ?? new DiscordCommandConfig();
            this.messenger = messenger;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("discord")
                .WithDescription("Displays the Discord invite")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Discord);
        }

        private TextCommandResult Discord(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string message = discordCommandConfig.InviteMessage;

            messenger.SendGeneral(player, message, GlobalConstants.GeneralChatGroup, (int)EnumChatType.AllGroups);
            messenger.SendInfo(player, message, GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            return TextCommandResult.Success();
        }
    }
}
