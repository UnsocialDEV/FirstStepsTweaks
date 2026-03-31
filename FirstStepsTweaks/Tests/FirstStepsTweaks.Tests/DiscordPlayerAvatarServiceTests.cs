using FirstStepsTweaks.Discord;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordPlayerAvatarServiceTests
{
    [Fact]
    public async Task TryGetAvatarUrlAsync_ReturnsNullForUnlinkedPlayer()
    {
        var service = new DiscordPlayerAvatarService(
            new DiscordBridgeConfig { BotToken = "token" },
            new FakeLinkedAccountStore(),
            new FakeDiscordUserProfileClient(),
            new DiscordAvatarUrlResolver());

        string? result = await service.TryGetAvatarUrlAsync("player-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetAvatarUrlAsync_ReturnsAvatarUrlForLinkedPlayer()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var service = new DiscordPlayerAvatarService(
            new DiscordBridgeConfig { BotToken = "token" },
            linkedStore,
            new FakeDiscordUserProfileClient(new DiscordUserProfile("discord-1", "avatarhash")),
            new DiscordAvatarUrlResolver());

        string? result = await service.TryGetAvatarUrlAsync("player-1");

        Assert.Equal("https://cdn.discordapp.com/avatars/discord-1/avatarhash.png?size=128", result);
    }

    [Fact]
    public async Task TryGetAvatarUrlAsync_ReturnsNullWhenLookupFails()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var service = new DiscordPlayerAvatarService(
            new DiscordBridgeConfig { BotToken = "token" },
            linkedStore,
            new ThrowingDiscordUserProfileClient(),
            new DiscordAvatarUrlResolver());

        string? result = await service.TryGetAvatarUrlAsync("player-1");

        Assert.Null(result);
    }

    private sealed class FakeLinkedAccountStore : IDiscordLinkedAccountStore
    {
        private readonly Dictionary<string, string> links = new(StringComparer.OrdinalIgnoreCase);

        public string GetLinkedDiscordUserId(string playerUid)
        {
            return links.TryGetValue(playerUid, out string? discordUserId) ? discordUserId : null;
        }

        public IReadOnlyDictionary<string, string> GetAllLinkedDiscordUserIds()
        {
            return links;
        }

        public void SetLinkedDiscordUserId(string playerUid, string discordUserId)
        {
            links[playerUid] = discordUserId;
        }

        public void ClearLinkedDiscordUserId(string playerUid)
        {
            links.Remove(playerUid);
        }
    }

    private sealed class FakeDiscordUserProfileClient : IDiscordUserProfileClient
    {
        private readonly DiscordUserProfile? profile;

        public FakeDiscordUserProfileClient(DiscordUserProfile? profile = null)
        {
            this.profile = profile;
        }

        public Task<DiscordUserProfile> GetUserProfileAsync(DiscordBridgeConfig config, string discordUserId)
        {
            return Task.FromResult(profile);
        }
    }

    private sealed class ThrowingDiscordUserProfileClient : IDiscordUserProfileClient
    {
        public Task<DiscordUserProfile> GetUserProfileAsync(DiscordBridgeConfig config, string discordUserId)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
