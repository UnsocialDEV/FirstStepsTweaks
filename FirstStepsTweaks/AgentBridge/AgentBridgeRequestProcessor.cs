using System.Threading;
using System.Threading.Tasks;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeRequestProcessor
{
    private readonly AgentBridgeTokenValidator tokenValidator;
    private readonly AgentBridgeCommandRequestValidator requestValidator;
    private readonly AgentBridgeMainThreadCommandExecutor commandExecutor;

    public AgentBridgeRequestProcessor(
        AgentBridgeTokenValidator tokenValidator,
        AgentBridgeCommandRequestValidator requestValidator,
        AgentBridgeMainThreadCommandExecutor commandExecutor)
    {
        this.tokenValidator = tokenValidator;
        this.requestValidator = requestValidator;
        this.commandExecutor = commandExecutor;
    }

    public async Task<AgentBridgeResponse> ProcessAsync(AgentBridgeRequest? request, CancellationToken cancellationToken)
    {
        if (!requestValidator.TryValidate(request, out var validationMessage))
        {
            return AgentBridgeResponse.Error(validationMessage);
        }

        if (!tokenValidator.IsAuthorized(request!.Token))
        {
            return AgentBridgeResponse.Error("Shared token was missing or invalid.");
        }

        return await commandExecutor.ExecuteAsync(request.Command!, cancellationToken);
    }
}
