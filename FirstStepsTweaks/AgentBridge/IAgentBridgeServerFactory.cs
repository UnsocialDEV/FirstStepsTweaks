using System.Net;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.AgentBridge;

internal interface IAgentBridgeServerFactory
{
    IAgentBridgeServer Create(IPEndPoint endpoint, ILogger logger, AgentBridgeConnectionHandler connectionHandler);
}
