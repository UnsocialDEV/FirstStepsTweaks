using FirstStepsTweaks.Infrastructure.Messaging;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkRewardJoinHandler
    {
        private readonly DiscordLinkRewardService rewardService;
        private readonly IPlayerMessenger messenger;

        public DiscordLinkRewardJoinHandler(
            DiscordLinkRewardService rewardService,
            IPlayerMessenger messenger)
        {
            this.rewardService = rewardService;
            this.messenger = messenger;
        }

        public void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (!rewardService.DeliverPendingReward(player))
            {
                return;
            }

            messenger.SendDual(
                player,
                "Discord account linked. You received 10 rusty gears.",
                GlobalConstants.InfoLogChatGroup,
                (int)EnumChatType.Notification,
                GlobalConstants.GeneralChatGroup,
                (int)EnumChatType.Notification);
        }
    }
}
