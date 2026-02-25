using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class KitCommands
    {
        private const string StarterKey = "fst_starterclaimed";
        private const string WinterKey = "fst_winterclaimed";
        private static KitConfig kitConfig = new KitConfig();

        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            kitConfig = config?.Kits ?? new KitConfig();

            if (kitConfig.EnableStarterKit)
            {
                api.ChatCommands
                    .Create("starterkit")
                    .WithDescription("Gives starter items")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(args => StarterKit(api, args));
            }

            if (kitConfig.EnableWinterKit)
            {
                api.ChatCommands
                    .Create("winterkit")
                    .WithDescription("Gives winter starter kit")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(args => WinterKit(api, args));
            }
        }

        private static TextCommandResult StarterKit(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            if (player.GetModdata(StarterKey) != null)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You have already claimed your starter kit.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            GiveConfiguredItems(api, player, kitConfig.StarterItems);
            player.SetModdata(StarterKey, new byte[] { 1 });
            player.SendMessage(GlobalConstants.InfoLogChatGroup, "You have received your starter kit!", EnumChatType.CommandSuccess);

            return TextCommandResult.Success();
        }

        private static TextCommandResult WinterKit(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            if (player.GetModdata(WinterKey) != null)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You have already claimed your winter kit.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            GiveConfiguredItems(api, player, kitConfig.WinterItems);
            player.SetModdata(WinterKey, new byte[] { 1 });
            player.SendMessage(GlobalConstants.InfoLogChatGroup, "You have received your winter kit!", EnumChatType.CommandSuccess);

            return TextCommandResult.Success();
        }

        private static void GiveConfiguredItems(ICoreServerAPI api, IServerPlayer player, System.Collections.Generic.List<KitItemConfig> items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Code) || item.Quantity <= 0) continue;
                ItemService.GiveCollectible(api, player, item.Code, item.Quantity);
            }
        }
    }
}
