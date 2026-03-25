using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class DonatorChatMessageFormatterTests
{
    private readonly DonatorChatMessageFormatter formatter = new();

    [Fact]
    public void Format_UsesLightweightBadgePrefix_ForDefaultConfig()
    {
        var config = new ChatConfig();

        string result = formatter.Format("Ava: hello", "Supporter", config);

        Assert.Equal("•S Ava: hello", result);
    }

    [Fact]
    public void Format_UsesConfiguredTemplate()
    {
        var config = new ChatConfig
        {
            DonatorPrefixFormat = "<{tier}>"
        };

        string result = formatter.Format("Ava: hello", "Founder", config);

        Assert.Equal("<•F> Ava: hello", result);
    }

    [Fact]
    public void Format_TreatsLegacyBracketTemplateAsBareBadgePrefix()
    {
        var config = new ChatConfig
        {
            DonatorPrefixFormat = "[{tier}]"
        };

        string result = formatter.Format("Ava: hello", "Founder", config);

        Assert.Equal("•F Ava: hello", result);
    }

    [Fact]
    public void Format_UsesConfiguredTierSpecificPrefix()
    {
        var config = new ChatConfig
        {
            SponsorPrefix = "~VIP~"
        };

        string result = formatter.Format("Ava: hello", "Sponsor", config);

        Assert.Equal("~VIP~ Ava: hello", result);
    }
}
