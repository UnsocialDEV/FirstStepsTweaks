using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class AdminModeCommand
    {
        private readonly ICoreServerAPI api;
        private readonly AdminModeService adminModeService;

        public AdminModeCommand(ICoreServerAPI api, AdminModeService adminModeService)
        {
            this.api = api;
            this.adminModeService = adminModeService;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("adminmode")
                .WithDescription("Toggle a persisted creative admin mode that swaps between survival and admin loadouts")
                .RequiresPlayer()
                .RequiresPrivilege(StaffPrivilegeCatalog.AdminPrivilege)
                .HandleWith(Toggle);
        }

        private TextCommandResult Toggle(TextCommandCallingArgs args)
        {
            adminModeService.Toggle((IServerPlayer)args.Caller.Player);
            return TextCommandResult.Success();
        }
    }
}
