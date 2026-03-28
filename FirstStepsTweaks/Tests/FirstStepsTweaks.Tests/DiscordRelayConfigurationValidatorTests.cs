using FirstStepsTweaks.Discord;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class DiscordRelayConfigurationValidatorTests
{
    private readonly DiscordRelayConfigurationValidator validator = new();

    [Fact]
    public void IsReadyForDiscordToGame_DoesNotRequireWebhookUrl()
    {
        var config = new DiscordBridgeConfig
        {
            BotToken = "token",
            ChannelId = "channel"
        };

        bool result = validator.IsReadyForDiscordToGame(config);

        Assert.True(result);
    }

    [Fact]
    public void IsReadyForGameToDiscord_RequiresWebhookUrl()
    {
        var config = new DiscordBridgeConfig
        {
            BotToken = "token",
            ChannelId = "channel"
        };

        bool result = validator.IsReadyForGameToDiscord(config);

        Assert.False(result);
    }

    [Fact]
    public void IsReadyForDiscordToGame_RequiresBotTokenAndChannelId()
    {
        var config = new DiscordBridgeConfig
        {
            WebhookUrl = "https://example.invalid/webhook"
        };

        bool result = validator.IsReadyForDiscordToGame(config);

        Assert.False(result);
    }
}
