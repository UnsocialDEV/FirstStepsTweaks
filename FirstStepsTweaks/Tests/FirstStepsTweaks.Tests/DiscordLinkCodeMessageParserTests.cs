using FirstStepsTweaks.Discord;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordLinkCodeMessageParserTests
{
    private readonly DiscordLinkCodeMessageParser parser = new();

    [Fact]
    public void TryParsePendingCode_ReturnsFalseForUnknownCode()
    {
        bool result = parser.TryParsePendingCode("Please link ABC123", new[] { "XYZ789" }, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParsePendingCode_ReturnsPendingCodeFromMessage()
    {
        bool result = parser.TryParsePendingCode("Please link abc123", new[] { "ABC123" }, out string code);

        Assert.True(result);
        Assert.Equal("ABC123", code);
    }
}
