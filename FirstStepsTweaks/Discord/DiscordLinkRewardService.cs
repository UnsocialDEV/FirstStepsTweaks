using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkRewardService
    {
        private readonly IDiscordLinkRewardStateStore stateStore;
        private readonly IDiscordLinkRewardItemGiver itemGiver;

        public DiscordLinkRewardService(
            IDiscordLinkRewardStateStore stateStore,
            IDiscordLinkRewardItemGiver itemGiver)
        {
            this.stateStore = stateStore;
            this.itemGiver = itemGiver;
        }

        public DiscordLinkRewardOutcome HandleSuccessfulLink(string playerUid, IServerPlayer onlinePlayer)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return DiscordLinkRewardOutcome.None;
            }

            if (stateStore.HasClaimed(playerUid))
            {
                stateStore.ClearPendingReward(playerUid);
                return DiscordLinkRewardOutcome.AlreadyClaimed;
            }

            if (onlinePlayer == null)
            {
                stateStore.MarkPendingReward(playerUid);
                return DiscordLinkRewardOutcome.QueuedForNextJoin;
            }

            itemGiver.Give(onlinePlayer);
            stateStore.ClearPendingReward(playerUid);
            stateStore.MarkClaimed(playerUid);
            return DiscordLinkRewardOutcome.GrantedImmediately;
        }

        public bool DeliverPendingReward(IServerPlayer player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PlayerUID))
            {
                return false;
            }

            if (stateStore.HasClaimed(player.PlayerUID))
            {
                stateStore.ClearPendingReward(player.PlayerUID);
                return false;
            }

            if (!stateStore.HasPendingReward(player.PlayerUID))
            {
                return false;
            }

            itemGiver.Give(player);
            stateStore.ClearPendingReward(player.PlayerUID);
            stateStore.MarkClaimed(player.PlayerUID);
            return true;
        }
    }
}
