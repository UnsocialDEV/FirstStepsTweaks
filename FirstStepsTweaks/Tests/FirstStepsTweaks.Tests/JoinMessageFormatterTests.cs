using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class JoinMessageFormatterTests
{
    [Fact]
    public void FormatFirstJoin_ReplacesPlayerToken()
    {
        var formatter = new JoinMessageFormatter();

        string result = formatter.FormatFirstJoin("Welcome {player}", "Ava");

        Assert.Equal("Welcome Ava", result);
    }

    [Fact]
    public void FormatReturningJoin_ReplacesAllTokens()
    {
        var formatter = new JoinMessageFormatter();

        string result = formatter.FormatReturningJoin("Welcome back {player} after {days} days with {playtime} hours", "Ava", 4, "12.5");

        Assert.Equal("Welcome back Ava after 4 days with 12.5 hours", result);
    }
}
