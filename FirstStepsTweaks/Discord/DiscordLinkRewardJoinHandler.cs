using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkRewardJoinHandler
    {
        private const int DeliveryDelayMs = 2000;
        private readonly DiscordLinkRewardService rewardService;
        private readonly DelayedPlayerActionScheduler scheduler;
        private readonly IPlayerMessenger messenger;

        public DiscordLinkRewardJoinHandler(
            DiscordLinkRewardService rewardService,
            DelayedPlayerActionScheduler scheduler,
            IPlayerMessenger messenger)
        {
            this.rewardService = rewardService;
            this.scheduler = scheduler;
            this.messenger = messenger;
        }

        public void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PlayerUID))
            {
                return;
            }

            scheduler.Schedule(player.PlayerUID, DeliveryDelayMs, DeliverPendingReward);
        }

        private void DeliverPendingReward(IServerPlayer player)
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
