using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class PlaytimeFormatterTests
{
    [Theory]
    [InlineData(0, "0.0")]
    [InlineData(1800, "0.5")]
    [InlineData(20000, "5.6")]
    public void FormatHours_FormatsToSingleDecimal(long totalPlayedSeconds, string expected)
    {
        var formatter = new PlaytimeFormatter();

        string result = formatter.FormatHours(totalPlayedSeconds);

        Assert.Equal(expected, result);
    }
}
