using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class DebugCommands
    {
        private readonly ICoreServerAPI api;

        public DebugCommands(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("fsdebug")
                .WithDescription("Debug command for First Steps dev")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("chattypes")
                    .WithDescription("Sends a message for each chat type to test formatting")
                    .HandleWith(fsDebugChatTypes)
                .EndSubCommand()
                .HandleWith(fsDebug);
        }

        private TextCommandResult fsDebug(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success("Debug command for first steps tweaks");
        }

        private TextCommandResult fsDebugChatTypes(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            player.SendMessage(GlobalConstants.GeneralChatGroup, "AllGroups", EnumChatType.AllGroups);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "CommandError", EnumChatType.CommandError);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "CommandSuccess", EnumChatType.CommandSuccess);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "GroupInvite", EnumChatType.GroupInvite);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "JoinLeave", EnumChatType.JoinLeave);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Macro", EnumChatType.Macro);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Notification", EnumChatType.Notification);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "OthersMessage", EnumChatType.OthersMessage);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "OwnMessage", EnumChatType.OwnMessage);

            return TextCommandResult.Success();
        }
    }
}
