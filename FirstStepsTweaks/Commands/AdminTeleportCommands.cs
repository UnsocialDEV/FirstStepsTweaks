using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class AdminTeleportCommands
    {
        private readonly ICoreServerAPI api;
        private readonly AdminTeleportService teleportService;

        public AdminTeleportCommands(ICoreServerAPI api, AdminTeleportService teleportService)
        {
            this.api = api;
            this.teleportService = teleportService;
        }

        public void Register()
        {
            api.ChatCommands.Create("tpto")
                .WithDescription("Admin teleport: instantly teleport yourself to another online player")
                .WithArgs(api.ChatCommands.Parsers.Word("player"))
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(TpTo);

            api.ChatCommands.Create("tphere")
                .WithDescription("Admin teleport: instantly teleport another online player to you")
                .WithArgs(api.ChatCommands.Parsers.Word("player"))
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(TpHere);
        }

        private TextCommandResult TpTo(TextCommandCallingArgs args)
        {
            teleportService.TeleportCallerToTarget((IServerPlayer)args.Caller.Player, (string)args[0]);
            return TextCommandResult.Success();
        }

        private TextCommandResult TpHere(TextCommandCallingArgs args)
        {
            teleportService.TeleportTargetToCaller((IServerPlayer)args.Caller.Player, (string)args[0]);
            return TextCommandResult.Success();
        }
    }
}
