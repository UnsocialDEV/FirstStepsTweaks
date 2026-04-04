using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeTcpServer : IAgentBridgeServer
{
    private readonly TcpListener listener;
    private readonly ILogger logger;
    private readonly AgentBridgeConnectionHandler connectionHandler;
    private readonly CancellationTokenSource stopSource = new();
    private Task? acceptLoop;

    public AgentBridgeTcpServer(
        IPEndPoint endpoint,
        ILogger logger,
        AgentBridgeConnectionHandler connectionHandler)
    {
        listener = new TcpListener(endpoint);
        this.logger = logger;
        this.connectionHandler = connectionHandler;
    }

    public void Start()
    {
        listener.Start();
        logger.Notification("[FirstStepsTweaks] Agent bridge listener started.");
        acceptLoop = AcceptLoopAsync(stopSource.Token);
    }

    public void Dispose()
    {
        stopSource.Cancel();
        listener.Stop();

        try
        {
            acceptLoop?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        stopSource.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;

            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                client?.Dispose();
                logger.Warning($"[FirstStepsTweaks] Agent bridge listener error: {exception.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                await connectionHandler.HandleAsync(client, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Warning($"[FirstStepsTweaks] Agent bridge connection error: {exception.Message}");
            }
        }
    }
}
