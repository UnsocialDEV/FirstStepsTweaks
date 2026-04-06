using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class StormShelterCommands
    {
        private readonly ICoreServerAPI api;
        private readonly StormShelterStore stormShelterStore;
        private readonly IPlayerMessenger messenger;
        private readonly StormShelterTeleportService stormShelterTeleportService;

        public StormShelterCommands(
            ICoreServerAPI api,
            StormShelterStore stormShelterStore,
            IPlayerMessenger messenger,
            StormShelterTeleportService stormShelterTeleportService)
        {
            this.api = api;
            this.stormShelterStore = stormShelterStore;
            this.messenger = messenger;
            this.stormShelterTeleportService = stormShelterTeleportService;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("setstormshelter")
                .WithDescription("Sets the storm shelter to your current location")
                .RequiresPlayer()
                .RequiresPrivilege(StaffPrivilegeCatalog.AdminPrivilege)
                .HandleWith(SetStormShelter);

            api.ChatCommands
                .Create("stormshelter")
                .WithDescription("Teleport to the server storm shelter")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(StormShelter);
        }

        private TextCommandResult SetStormShelter(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            stormShelterStore.SetStormShelter(player);
            messenger.SendDual(player, "Storm shelter position set.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult StormShelter(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            StormShelterTeleportResult result = stormShelterTeleportService.TryTeleport(
                player,
                (x, y, z) => player.Entity.TeleportToDouble(x, y, z));

            if (result == StormShelterTeleportResult.NotSet)
            {
                messenger.SendDual(player, "Storm shelter has not been set yet.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            messenger.SendIngameError(player, "no_permission", "Teleported to storm shelter.");
            return TextCommandResult.Success();
        }
    }
}
