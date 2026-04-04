using System.Threading;
using FirstStepsTweaks.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.AgentBridge;

public sealed class AgentBridgeMetricsPublisher
{
    private const double PublishIntervalMilliseconds = 5000;
    private readonly ICoreServerAPI api;
    private readonly AgentBridgeSubscriptionRegistry subscriptions;
    private readonly AgentBridgeJsonSerializer serializer;
    private readonly AgentBridgeUptimeTracker uptimeTracker;
    private readonly AgentBridgeTickTimeTracker tickTimeTracker;
    private bool disposed;

    public AgentBridgeMetricsPublisher(
        ICoreServerAPI api,
        AgentBridgeSubscriptionRegistry subscriptions,
        AgentBridgeJsonSerializer serializer,
        AgentBridgeUptimeTracker uptimeTracker,
        AgentBridgeTickTimeTracker tickTimeTracker)
    {
        this.api = api;
        this.subscriptions = subscriptions;
        this.serializer = serializer;
        this.uptimeTracker = uptimeTracker;
        this.tickTimeTracker = tickTimeTracker;
    }

    public void Start()
    {
        ScheduleNextPublish();
    }

    public void Stop()
    {
        disposed = true;
    }

    private void ScheduleNextPublish()
    {
        if (disposed)
        {
            return;
        }

        api.Event.Timer(Publish, PublishIntervalMilliseconds);
    }

    private void Publish()
    {
        if (disposed)
        {
            return;
        }

        // Subscribed bridge clients receive a lightweight heartbeat so external tools can
        // watch player counts, uptime, and recent tick cost without polling the game thread.
        var metrics = new AgentBridgeMetricsMessage
        {
            Players = api.World.AllOnlinePlayers.Length,
            UptimeSeconds = uptimeTracker.GetUptimeSeconds(),
            TickMs = tickTimeTracker.GetAverageTickMilliseconds()
        };

        _ = subscriptions.PublishAsync(serializer.Serialize(metrics), CancellationToken.None);
        ScheduleNextPublish();
    }
}
