using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class RtpCommands
    {
        private readonly ICoreServerAPI api;
        private readonly RtpTeleportService teleportService;

        public RtpCommands(ICoreServerAPI api, RtpTeleportService teleportService)
        {
            this.api = api;
            this.teleportService = teleportService;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("rtp")
                .WithDescription("Teleport to a random safe location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(RandomTeleport);
        }

        private TextCommandResult RandomTeleport(TextCommandCallingArgs args)
        {
            teleportService.Execute((IServerPlayer)args.Caller.Player);
            return TextCommandResult.Success();
        }
    }
}
