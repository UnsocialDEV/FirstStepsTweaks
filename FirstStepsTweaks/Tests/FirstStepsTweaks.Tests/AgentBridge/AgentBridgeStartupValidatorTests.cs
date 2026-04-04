using FirstStepsTweaks.AgentBridge;
using FirstStepsTweaks.Config;
using Xunit;

namespace FirstStepsTweaks.Tests.AgentBridge;

public sealed class AgentBridgeStartupValidatorTests
{
    [Fact]
    public void TryValidate_ReturnsFalse_WhenSharedTokenIsMissing()
    {
        var validator = new AgentBridgeStartupValidator();
        var config = new AgentBridgeConfig
        {
            Host = "127.0.0.1",
            Port = 8765,
            SharedToken = string.Empty
        };

        var isValid = validator.TryValidate(config, out var message);

        Assert.False(isValid);
        Assert.Equal("AgentBridge.SharedToken must be configured before the listener can start.", message);
    }

    [Fact]
    public void TryValidate_ReturnsTrue_ForValidConfig()
    {
        var validator = new AgentBridgeStartupValidator();
        var config = new AgentBridgeConfig
        {
            Host = "127.0.0.1",
            Port = 8765,
            SharedToken = "test-token"
        };

        var isValid = validator.TryValidate(config, out var message);

        Assert.True(isValid);
        Assert.Equal(string.Empty, message);
    }
}
