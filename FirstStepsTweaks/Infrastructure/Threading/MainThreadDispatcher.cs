using System;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Threading;

public sealed class MainThreadDispatcher : IMainThreadDispatcher
{
    private readonly ICoreServerAPI api;

    public MainThreadDispatcher(ICoreServerAPI api)
    {
        this.api = api;
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        api.Event.Timer(() =>
        {
            try
            {
                action();
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }, 0);

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        }

        return completion.Task;
    }
}
