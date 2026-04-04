namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeResponse
{
    public string Type { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static AgentBridgeResponse CommandAccepted() =>
        new()
        {
            Type = "commandResult",
            Success = true,
            Message = "Command forwarded to the server console."
        };

    public static AgentBridgeResponse Error(string message) =>
        new()
        {
            Type = "error",
            Success = false,
            Message = message
        };
}
