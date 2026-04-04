using System;

namespace FirstStepsTweaks.AgentBridge;

internal interface IAgentBridgeServer : IDisposable
{
    void Start();
}
