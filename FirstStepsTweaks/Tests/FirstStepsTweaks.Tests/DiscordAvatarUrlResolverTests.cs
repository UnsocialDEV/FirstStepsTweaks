using FirstStepsTweaks.Discord;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordAvatarUrlResolverTests
{
    private readonly DiscordAvatarUrlResolver resolver = new();

    [Fact]
    public void ResolveGlobalAvatarUrl_ReturnsExpectedPngUrl()
    {
        string? result = resolver.ResolveGlobalAvatarUrl(new DiscordUserProfile("12345", "avatarhash"));

        Assert.Equal("https://cdn.discordapp.com/avatars/12345/avatarhash.png?size=128", result);
    }

    [Fact]
    public void ResolveGlobalAvatarUrl_ReturnsExpectedGifUrlForAnimatedAvatar()
    {
        string? result = resolver.ResolveGlobalAvatarUrl(new DiscordUserProfile("12345", "a_avatarhash"));

        Assert.Equal("https://cdn.discordapp.com/avatars/12345/a_avatarhash.gif?size=128", result);
    }

    [Fact]
    public void ResolveGlobalAvatarUrl_ReturnsNullWhenAvatarHashIsMissing()
    {
        string? result = resolver.ResolveGlobalAvatarUrl(new DiscordUserProfile("12345", null));

        Assert.Null(result);
    }
}
