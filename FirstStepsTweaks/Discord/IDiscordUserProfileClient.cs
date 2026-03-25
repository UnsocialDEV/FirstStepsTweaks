using System.Threading.Tasks;

namespace FirstStepsTweaks.Discord
{
    public interface IDiscordUserProfileClient
    {
        Task<DiscordUserProfile> GetUserProfileAsync(DiscordBridgeConfig config, string discordUserId);
    }
}
