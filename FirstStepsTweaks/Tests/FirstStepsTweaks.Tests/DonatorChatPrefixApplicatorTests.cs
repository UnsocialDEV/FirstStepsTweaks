using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class DonatorChatPrefixApplicatorTests
{
    private readonly DonatorChatPrefixApplicator applicator = new();

    [Fact]
    public void Apply_LeavesMessageUnchanged_WhenNoPrivilegesMatch()
    {
        var config = new ChatConfig();

        string result = applicator.Apply("Ava: hello", _ => false, config);

        Assert.Equal("Ava: hello", result);
    }

    [Fact]
    public void Apply_LeavesCommandUnchanged_WhenMessageStartsWithSlash()
    {
        var config = new ChatConfig();

        string result = applicator.Apply("/home", _ => true, config);

        Assert.Equal("/home", result);
    }

    [Fact]
    public void Apply_PrependsHighestTierPrefix_WhenPrivilegeMatches()
    {
        var config = new ChatConfig();

        string result = applicator.Apply("Ava: hello", privilege => privilege == "firststepstweaks.patron", config);

        Assert.Equal("•P Ava: hello", result);
    }
}
