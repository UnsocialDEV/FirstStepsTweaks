using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class LandClaimMessageFormatterTests
{
    [Fact]
    public void BuildMessage_UsesYourForOwnedClaims()
    {
        var formatter = new LandClaimMessageFormatter();

        string result = formatter.BuildMessage("You entered {owner} land claim. ({claim})", "Base", "uid-1", "Player Ava", "uid-1", "Ava");

        Assert.Equal("You entered your land claim. (Base)", result);
    }

    [Fact]
    public void NormalizeOwnerName_RemovesPlayerPrefix()
    {
        var formatter = new LandClaimMessageFormatter();

        string result = formatter.NormalizeOwnerName("Player Ava");

        Assert.Equal("Ava", result);
    }
}
