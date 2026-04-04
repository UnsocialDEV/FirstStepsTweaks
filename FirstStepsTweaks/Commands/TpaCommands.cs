using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class TpaCommands
    {
        private readonly ICoreServerAPI api;
        private readonly TpaRequestCreator requestCreator;
        private readonly TpaRequestAccepter requestAccepter;
        private readonly TpaRequestDenier requestDenier;
        private readonly TpaRequestCanceller requestCanceller;
        private readonly TpaToggleService toggleService;

        public TpaCommands(
            ICoreServerAPI api,
            TpaRequestCreator requestCreator,
            TpaRequestAccepter requestAccepter,
            TpaRequestDenier requestDenier,
            TpaRequestCanceller requestCanceller,
            TpaToggleService toggleService)
        {
            this.api = api;
            this.requestCreator = requestCreator;
            this.requestAccepter = requestAccepter;
            this.requestDenier = requestDenier;
            this.requestCanceller = requestCanceller;
            this.toggleService = toggleService;
        }

        public void Register()
        {
            api.ChatCommands.Create("tpa")
                .WithDescription("Request to teleport yourself to another online player")
                .WithArgs(api.ChatCommands.Parsers.Word("player"))
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Tpa);

            api.ChatCommands.Create("tpahere")
                .WithDescription("Request to teleport another online player to your location")
                .WithArgs(api.ChatCommands.Parsers.Word("player"))
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpaHere);

            api.ChatCommands.Create("tpaccept")
                .WithDescription("Accept your oldest pending /tpa or /tpahere request")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpAccept);

            api.ChatCommands.Create("tpadeny")
                .WithDescription("Deny your oldest pending /tpa or /tpahere request")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpDeny);

            api.ChatCommands.Create("tpacancel")
                .WithDescription("Cancel your oldest outgoing /tpa or /tpahere request")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpCancel);

            api.ChatCommands.Create("tpatoggle")
                .WithDescription("Toggle whether other players can send you /tpa or /tpahere requests")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpaToggle);
        }

        private TextCommandResult Tpa(TextCommandCallingArgs args)
        {
            requestCreator.Create((IServerPlayer)args.Caller.Player, (string)args[0], TpaRequestDirection.RequesterToTarget);
            return TextCommandResult.Success();
        }

        private TextCommandResult TpaHere(TextCommandCallingArgs args)
        {
            requestCreator.Create((IServerPlayer)args.Caller.Player, (string)args[0], TpaRequestDirection.TargetToRequester);
            return TextCommandResult.Success();
        }

        private TextCommandResult TpAccept(TextCommandCallingArgs args)
        {
            requestAccepter.Accept((IServerPlayer)args.Caller.Player);
            return TextCommandResult.Success();
        }

        private TextCommandResult TpDeny(TextCommandCallingArgs args)
        {
            requestDenier.Deny((IServerPlayer)args.Caller.Player);
            return TextCommandResult.Success();
        }

        private TextCommandResult TpCancel(TextCommandCallingArgs args)
        {
            requestCanceller.Cancel((IServerPlayer)args.Caller.Player);
            return TextCommandResult.Success();
        }

        private TextCommandResult TpaToggle(TextCommandCallingArgs args)
        {
            toggleService.Toggle((IServerPlayer)args.Caller.Player);
            return TextCommandResult.Success();
        }
    }
}
