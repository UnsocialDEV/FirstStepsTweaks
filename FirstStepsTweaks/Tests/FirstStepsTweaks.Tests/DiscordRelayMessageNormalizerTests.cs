using FirstStepsTweaks.Discord;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordRelayMessageNormalizerTests
{
    private readonly DiscordRelayMessageNormalizer normalizer = new();

    [Fact]
    public void NormalizePlayerChat_StripsPlainPlayerPrefix()
    {
        string result = normalizer.NormalizePlayerChat("Unsocial", "Unsocial: test");

        Assert.Equal("test", result);
    }

    [Fact]
    public void NormalizePlayerChat_StripsTierPrefixAndPlayerPrefix()
    {
        string result = normalizer.NormalizePlayerChat("Unsocial", "[Sponsor] Unsocial: test");

        Assert.Equal("test", result);
    }

    [Fact]
    public void NormalizePlayerChat_LeavesMessageAloneWhenNoPlayerPrefixExists()
    {
        string result = normalizer.NormalizePlayerChat("Unsocial", "test");

        Assert.Equal("test", result);
    }
}
