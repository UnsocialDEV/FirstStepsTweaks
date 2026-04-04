using System;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeEventPublisher
{
    private readonly AgentBridgeSubscriptionRegistry subscriptions;
    private readonly AgentBridgeJsonSerializer serializer;

    public AgentBridgeEventPublisher(
        AgentBridgeSubscriptionRegistry subscriptions,
        AgentBridgeJsonSerializer serializer)
    {
        this.subscriptions = subscriptions;
        this.serializer = serializer;
    }

    public void OnPlayerJoin(IServerPlayer player)
    {
        Publish("player_join", player, null);
    }

    public void OnPlayerLeave(IServerPlayer player)
    {
        Publish("player_leave", player, null);
    }

    public void OnPlayerDeath(IServerPlayer player, DamageSource source)
    {
        Publish("death", player, source?.GetType().Name);
    }

    public void OnGameWorldSave()
    {
        Publish("save", null, null);
    }

    private void Publish(string eventType, IServerPlayer? player, string? detail)
    {
        // Event fan-out shares the same subscription channel as metrics so a single
        // external bridge client can listen for both server telemetry and player lifecycle updates.
        var message = new AgentBridgeEventMessage
        {
            EventType = eventType,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            PlayerUid = player?.PlayerUID,
            PlayerName = player?.PlayerName,
            Detail = detail
        };

        _ = subscriptions.PublishAsync(serializer.Serialize(message), CancellationToken.None);
    }
}
