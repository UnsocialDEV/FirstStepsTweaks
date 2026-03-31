using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public interface IDiscordLinkRewardItemGiver
    {
        void Give(IServerPlayer player);
    }
}
