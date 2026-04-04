using System;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeMainThreadCommandExecutor
{
    private readonly ICoreServerAPI api;

    public AgentBridgeMainThreadCommandExecutor(ICoreServerAPI api)
    {
        this.api = api;
    }

    public Task<AgentBridgeResponse> ExecuteAsync(string command, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<AgentBridgeResponse>(cancellationToken);
        }

        var completion = new TaskCompletionSource<AgentBridgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Remote bridge commands must hop back onto the game thread before they touch the
        // server console so InjectConsole runs with the same threading expectations as in-game work.
        api.Event.Timer(() =>
        {
            try
            {
                api.InjectConsole(command);
                completion.TrySetResult(AgentBridgeResponse.CommandAccepted());
            }
            catch (Exception exception)
            {
                completion.TrySetResult(AgentBridgeResponse.Error($"Command execution failed: {exception.Message}"));
            }
        }, 0);

        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

        return completion.Task;
    }
}
