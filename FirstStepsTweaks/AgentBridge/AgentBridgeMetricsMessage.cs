namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeMetricsMessage
{
    public string Type { get; init; } = "metrics";

    public int Players { get; init; }

    public long UptimeSeconds { get; init; }

    public double TickMs { get; init; }
}
