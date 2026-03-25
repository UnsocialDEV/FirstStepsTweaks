using System;
using System.Threading.Tasks;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordPlayerAvatarService
    {
        private readonly DiscordBridgeConfig config;
        private readonly IDiscordLinkedAccountStore linkedAccountStore;
        private readonly IDiscordUserProfileClient userProfileClient;
        private readonly DiscordAvatarUrlResolver avatarUrlResolver;

        public DiscordPlayerAvatarService(
            DiscordBridgeConfig config,
            IDiscordLinkedAccountStore linkedAccountStore,
            IDiscordUserProfileClient userProfileClient,
            DiscordAvatarUrlResolver avatarUrlResolver)
        {
            this.config = config;
            this.linkedAccountStore = linkedAccountStore;
            this.userProfileClient = userProfileClient;
            this.avatarUrlResolver = avatarUrlResolver;
        }

        public async Task<string> TryGetAvatarUrlAsync(string playerUid)
        {
            if (config == null || string.IsNullOrWhiteSpace(playerUid))
            {
                return null;
            }

            string discordUserId = linkedAccountStore.GetLinkedDiscordUserId(playerUid);
            if (string.IsNullOrWhiteSpace(discordUserId))
            {
                return null;
            }

            try
            {
                DiscordUserProfile profile = await userProfileClient.GetUserProfileAsync(config, discordUserId);
                return avatarUrlResolver.ResolveGlobalAvatarUrl(profile);
            }
            catch
            {
                return null;
            }
        }
    }
}
