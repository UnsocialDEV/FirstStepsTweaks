using System.Net;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.AgentBridge;

internal sealed class AgentBridgeTcpServerFactory : IAgentBridgeServerFactory
{
    public IAgentBridgeServer Create(IPEndPoint endpoint, ILogger logger, AgentBridgeConnectionHandler connectionHandler)
    {
        return new AgentBridgeTcpServer(endpoint, logger, connectionHandler);
    }
}
