using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public class DebugCommands
    {
        public static void Register(ICoreServerAPI api)
        {
            api.ChatCommands
                .Create("fsdebug")
                .WithDescription("Debug command for First Steps dev")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => fsDebug(api, args));
        }

        private static TextCommandResult fsDebug(ICoreServerAPI api, TextCommandCallingArgs args) 
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                "AllGroups",
                EnumChatType.AllGroups
                );

            return TextCommandResult.Success();
        }
    }
}