using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeConnectionHandler
{
    private readonly AgentBridgeJsonSerializer serializer;
    private readonly AgentBridgeRequestProcessor processor;
    private readonly AgentBridgeTokenValidator tokenValidator;
    private readonly AgentBridgeSubscriptionRegistry subscriptions;

    public AgentBridgeConnectionHandler(
        AgentBridgeJsonSerializer serializer,
        AgentBridgeRequestProcessor processor,
        AgentBridgeTokenValidator tokenValidator,
        AgentBridgeSubscriptionRegistry subscriptions)
    {
        this.serializer = serializer;
        this.processor = processor;
        this.tokenValidator = tokenValidator;
        this.subscriptions = subscriptions;
    }

    public async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        var payload = await reader.ReadLineAsync(cancellationToken);
        var request = string.IsNullOrWhiteSpace(payload)
            ? null
            : serializer.DeserializeRequest(payload);

        // Command requests are one-shot RPC calls. "subscribe" keeps the socket open so
        // telemetry and lifecycle events can be streamed back over the same connection.
        if (request is not null && string.Equals(request.Type, "subscribe", StringComparison.OrdinalIgnoreCase))
        {
            await HandleSubscriptionAsync(request, stream, reader, writer, cancellationToken);
            return;
        }

        var response = await processor.ProcessAsync(request, cancellationToken);
        await writer.WriteLineAsync(serializer.SerializeResponse(response));
    }

    private async Task HandleSubscriptionAsync(
        AgentBridgeRequest request,
        Stream stream,
        StreamReader reader,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        if (!tokenValidator.IsAuthorized(request.Token))
        {
            await writer.WriteLineAsync(serializer.SerializeResponse(AgentBridgeResponse.Error("Shared token was missing or invalid.")));
            return;
        }

        var subscriberId = subscriptions.Add(stream);

        try
        {
            await writer.WriteLineAsync(serializer.SerializeResponse(new AgentBridgeResponse
            {
                Type = "subscribed",
                Success = true,
                Message = "Subscribed to telemetry and events."
            }));

            while (!cancellationToken.IsCancellationRequested)
            {
                var input = await reader.ReadLineAsync(cancellationToken);

                if (input is null)
                {
                    break;
                }
            }
        }
        finally
        {
            subscriptions.Remove(subscriberId);
        }
    }
}
