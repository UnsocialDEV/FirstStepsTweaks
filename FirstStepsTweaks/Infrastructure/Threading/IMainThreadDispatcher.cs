using System;
using System.Threading;
using System.Threading.Tasks;

namespace FirstStepsTweaks.Infrastructure.Threading;

public interface IMainThreadDispatcher
{
    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
}
