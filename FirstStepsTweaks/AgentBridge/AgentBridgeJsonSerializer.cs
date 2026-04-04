using System.Text.Json;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgentBridgeRequest? DeserializeRequest(string payload)
    {
        return JsonSerializer.Deserialize<AgentBridgeRequest>(payload, Options);
    }

    public string SerializeResponse(AgentBridgeResponse response)
    {
        return JsonSerializer.Serialize(response, Options);
    }

    public string Serialize(AgentBridgeMetricsMessage metrics)
    {
        return JsonSerializer.Serialize(metrics, Options);
    }

    public string Serialize(AgentBridgeEventMessage gameEvent)
    {
        return JsonSerializer.Serialize(gameEvent, Options);
    }
}
