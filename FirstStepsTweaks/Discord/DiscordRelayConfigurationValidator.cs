namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordRelayConfigurationValidator
    {
        public bool IsReadyForDiscordToGame(DiscordBridgeConfig config)
        {
            return config != null
                && !string.IsNullOrWhiteSpace(config.BotToken)
                && !string.IsNullOrWhiteSpace(config.ChannelId);
        }

        public bool IsReadyForGameToDiscord(DiscordBridgeConfig config)
        {
            return IsReadyForDiscordToGame(config)
                && !string.IsNullOrWhiteSpace(config.WebhookUrl);
        }
    }
}
