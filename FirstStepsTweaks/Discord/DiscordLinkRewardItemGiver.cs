using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkRewardItemGiver : IDiscordLinkRewardItemGiver
    {
        private const string RewardCode = "game:gear-rusty";
        private const int RewardQuantity = 10;
        private readonly ICoreServerAPI api;

        public DiscordLinkRewardItemGiver(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void Give(IServerPlayer player)
        {
            ItemService.GiveCollectible(api, player, RewardCode, RewardQuantity);
        }
    }
}
