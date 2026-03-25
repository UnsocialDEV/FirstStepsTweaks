using System;
using System.Text.Json;
using System.Threading.Tasks;
using FirstStepsTweaks.Discord.Transport;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordUserProfileClient : IDiscordUserProfileClient
    {
        private readonly IDiscordWebhookClient webhookClient;

        public DiscordUserProfileClient(IDiscordWebhookClient webhookClient)
        {
            this.webhookClient = webhookClient;
        }

        public async Task<DiscordUserProfile> GetUserProfileAsync(DiscordBridgeConfig config, string discordUserId)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrWhiteSpace(config.BotToken) || string.IsNullOrWhiteSpace(discordUserId))
            {
                return null;
            }

            DiscordHttpResponse response = await webhookClient.GetAsync(
                $"https://discord.com/api/v10/users/{discordUserId}",
                config.BotToken);

            if (response.StatusCode == 404)
            {
                return null;
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                throw new InvalidOperationException($"Discord user profile request returned status code {response.StatusCode}.");
            }

            using JsonDocument document = JsonDocument.Parse(response.Body);
            string userId = document.RootElement.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
            string avatarHash = document.RootElement.TryGetProperty("avatar", out JsonElement avatarElement) ? avatarElement.GetString() : null;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return new DiscordUserProfile(userId, avatarHash);
        }
    }
}
