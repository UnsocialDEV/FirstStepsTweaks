using System;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeEventMessage
{
    public string Type { get; init; } = "event";

    public string EventType { get; init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; init; }

    public string? PlayerUid { get; init; }

    public string? PlayerName { get; init; }

    public string? Detail { get; init; }
}
