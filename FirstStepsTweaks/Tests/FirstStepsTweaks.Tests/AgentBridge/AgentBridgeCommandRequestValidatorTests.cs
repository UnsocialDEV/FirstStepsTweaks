using FirstStepsTweaks.AgentBridge;
using Xunit;

namespace FirstStepsTweaks.Tests.AgentBridge;

public sealed class AgentBridgeCommandRequestValidatorTests
{
    [Fact]
    public void TryValidate_ReturnsFalse_WhenRequestIsNull()
    {
        var validator = new AgentBridgeCommandRequestValidator();

        var isValid = validator.TryValidate(null, out var message);

        Assert.False(isValid);
        Assert.Equal("Request body was empty or invalid JSON.", message);
    }

    [Fact]
    public void TryValidate_ReturnsFalse_WhenTypeIsNotCommand()
    {
        var validator = new AgentBridgeCommandRequestValidator();
        var request = new AgentBridgeRequest { Type = "metrics", Command = "/time" };

        var isValid = validator.TryValidate(request, out var message);

        Assert.False(isValid);
        Assert.Equal("Only 'command' requests are supported in the current phase.", message);
    }

    [Fact]
    public void TryValidate_ReturnsTrue_WhenRequestContainsCommandPayload()
    {
        var validator = new AgentBridgeCommandRequestValidator();
        var request = new AgentBridgeRequest { Type = "command", Command = "/time" };

        var isValid = validator.TryValidate(request, out var message);

        Assert.True(isValid);
        Assert.Equal(string.Empty, message);
    }
}
