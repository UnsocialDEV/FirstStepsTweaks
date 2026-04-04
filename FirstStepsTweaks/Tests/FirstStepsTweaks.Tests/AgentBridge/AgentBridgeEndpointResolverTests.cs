using System.Net;
using FirstStepsTweaks.AgentBridge;
using Xunit;

namespace FirstStepsTweaks.Tests.AgentBridge;

public sealed class AgentBridgeEndpointResolverTests
{
    [Fact]
    public void TryResolve_ResolvesLocalhostToLoopback()
    {
        var resolver = new AgentBridgeEndpointResolver();

        bool isValid = resolver.TryResolve("localhost", 8765, out IPEndPoint? endpoint, out string message);

        Assert.True(isValid);
        Assert.NotNull(endpoint);
        Assert.Equal(IPAddress.Loopback, endpoint!.Address);
        Assert.Equal(8765, endpoint.Port);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryResolve_ReturnsFalse_WhenHostCannotBeResolved()
    {
        var resolver = new AgentBridgeEndpointResolver();

        bool isValid = resolver.TryResolve("invalid host name ###", 8765, out IPEndPoint? endpoint, out string message);

        Assert.False(isValid);
        Assert.Null(endpoint);
        Assert.Contains("could not be resolved", message);
    }
}
