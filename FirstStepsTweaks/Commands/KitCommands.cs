using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class KitCommands
    {
        private readonly ICoreServerAPI api;
        private readonly KitConfig kitConfig;
        private readonly KitClaimStore claimStore;
        private readonly KitItemConsolidator consolidator;
        private readonly IPlayerMessenger messenger;

        public KitCommands(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            KitClaimStore claimStore,
            KitItemConsolidator consolidator,
            IPlayerMessenger messenger)
        {
            this.api = api;
            kitConfig = config?.Kits ?? new KitConfig();
            this.claimStore = claimStore;
            this.consolidator = consolidator;
            this.messenger = messenger;
        }

        public void Register()
        {
            if (kitConfig.EnableStarterKit)
            {
                api.ChatCommands
                    .Create("starterkit")
                    .WithDescription("Gives starter items")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(StarterKit);
            }

            if (kitConfig.EnableWinterKit)
            {
                api.ChatCommands
                    .Create("winterkit")
                    .WithDescription("Gives winter starter kit")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(WinterKit);
            }

            if (kitConfig.EnableSupporterKit)
            {
                api.ChatCommands
                    .Create("supporterkit")
                    .WithDescription("Gives supporter thank-you kit")
                    .RequiresPlayer()
                    .RequiresPrivilege("firststepstweaks.supporterkit")
                    .HandleWith(SupporterKit);
            }
        }

        private TextCommandResult StarterKit(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            if (claimStore.HasStarterClaim(player))
            {
                messenger.SendDual(player, "You have already claimed your starter kit.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            GiveConfiguredItems(player, kitConfig.StarterItems);
            claimStore.MarkStarterClaimed(player);
            messenger.SendDual(player, "You have received your starter kit!", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private TextCommandResult WinterKit(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            if (claimStore.HasWinterClaim(player))
            {
                messenger.SendDual(player, "You have already claimed your winter kit.", (int)EnumChatType.CommandError, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            GiveConfiguredItems(player, kitConfig.WinterItems);
            claimStore.MarkWinterClaimed(player);
            messenger.SendDual(player, "You have received your winter kit!", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private TextCommandResult SupporterKit(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            if (claimStore.HasSupporterClaim(player))
            {
                messenger.SendDual(player, "You have already claimed your Supporter kit.", (int)EnumChatType.CommandError, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            GiveConfiguredItems(player, kitConfig.SupporterItems);
            claimStore.MarkSupporterClaimed(player);
            messenger.SendDual(player, "You have received your Supporter kit!", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private void GiveConfiguredItems(IServerPlayer player, System.Collections.Generic.List<KitItemConfig> items)
        {
            foreach (var item in consolidator.Consolidate(items))
            {
                ItemService.GiveCollectible(api, player, item.Key, item.Value);
            }
        }
    }
}
