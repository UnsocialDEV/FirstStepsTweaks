using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeSubscriptionRegistry
{
    private readonly ConcurrentDictionary<int, AgentBridgeSubscriber> subscribers = new();
    private int nextId;

    public int Add(Stream stream)
    {
        var id = Interlocked.Increment(ref nextId);
        subscribers[id] = new AgentBridgeSubscriber(stream);
        return id;
    }

    public void Remove(int subscriberId)
    {
        subscribers.TryRemove(subscriberId, out _);
    }

    public async Task PublishAsync(string payload, CancellationToken cancellationToken)
    {
        List<int>? failedSubscribers = null;

        foreach (var pair in subscribers)
        {
            try
            {
                await pair.Value.SendAsync(payload, cancellationToken);
            }
            catch
            {
                failedSubscribers ??= new List<int>();
                failedSubscribers.Add(pair.Key);
            }
        }

        if (failedSubscribers is null)
        {
            return;
        }

        foreach (var subscriberId in failedSubscribers)
        {
            Remove(subscriberId);
        }
    }
}
