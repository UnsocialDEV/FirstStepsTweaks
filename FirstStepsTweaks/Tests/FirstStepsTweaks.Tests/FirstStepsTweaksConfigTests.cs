using FirstStepsTweaks.Config;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class FirstStepsTweaksConfigTests
{
    [Fact]
    public void FeatureToggles_DefaultToAgentBridgeDisabled()
    {
        var config = new FirstStepsTweaksConfig();

        Assert.False(config.Features.EnableAgentBridge);
    }
}
