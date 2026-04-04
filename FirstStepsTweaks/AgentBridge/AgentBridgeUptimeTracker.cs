using System;
using System.Diagnostics;

namespace FirstStepsTweaks.AgentBridge;

public sealed class AgentBridgeUptimeTracker
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public long GetUptimeSeconds()
    {
        return Convert.ToInt64(stopwatch.Elapsed.TotalSeconds);
    }
}
