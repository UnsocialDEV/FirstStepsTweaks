using System.Reflection;
using FirstStepsTweaks.Discord;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordLinkRewardServiceTests
{
    [Fact]
    public void HandleSuccessfulLink_WhenPlayerIsOnline_GrantsRewardAndMarksClaimed()
    {
        var stateStore = new FakeDiscordLinkRewardStateStore();
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var service = new DiscordLinkRewardService(stateStore, itemGiver);
        IServerPlayer player = CreatePlayer("player-1");

        DiscordLinkRewardOutcome outcome = service.HandleSuccessfulLink("player-1", player);

        Assert.Equal(DiscordLinkRewardOutcome.GrantedImmediately, outcome);
        Assert.Equal(1, itemGiver.GiveCount);
        Assert.True(stateStore.HasClaimed("player-1"));
        Assert.False(stateStore.HasPendingReward("player-1"));
    }

    [Fact]
    public void HandleSuccessfulLink_WhenPlayerIsOffline_QueuesRewardWithoutClaiming()
    {
        var stateStore = new FakeDiscordLinkRewardStateStore();
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var service = new DiscordLinkRewardService(stateStore, itemGiver);

        DiscordLinkRewardOutcome outcome = service.HandleSuccessfulLink("player-1", null!);

        Assert.Equal(DiscordLinkRewardOutcome.QueuedForNextJoin, outcome);
        Assert.Equal(0, itemGiver.GiveCount);
        Assert.False(stateStore.HasClaimed("player-1"));
        Assert.True(stateStore.HasPendingReward("player-1"));
    }

    [Fact]
    public void HandleSuccessfulLink_WhenRewardAlreadyClaimed_DoesNotGrantAgain()
    {
        var stateStore = new FakeDiscordLinkRewardStateStore();
        stateStore.MarkClaimed("player-1");
        stateStore.MarkPendingReward("player-1");
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var service = new DiscordLinkRewardService(stateStore, itemGiver);

        DiscordLinkRewardOutcome outcome = service.HandleSuccessfulLink("player-1", CreatePlayer("player-1"));

        Assert.Equal(DiscordLinkRewardOutcome.AlreadyClaimed, outcome);
        Assert.Equal(0, itemGiver.GiveCount);
        Assert.True(stateStore.HasClaimed("player-1"));
        Assert.False(stateStore.HasPendingReward("player-1"));
    }

    [Fact]
    public void DeliverPendingReward_WhenPendingRewardExists_GrantsRewardAndClearsPending()
    {
        var stateStore = new FakeDiscordLinkRewardStateStore();
        stateStore.MarkPendingReward("player-1");
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var service = new DiscordLinkRewardService(stateStore, itemGiver);

        bool delivered = service.DeliverPendingReward(CreatePlayer("player-1"));

        Assert.True(delivered);
        Assert.Equal(1, itemGiver.GiveCount);
        Assert.True(stateStore.HasClaimed("player-1"));
        Assert.False(stateStore.HasPendingReward("player-1"));
    }

    [Fact]
    public void DeliverPendingReward_WhenAlreadyClaimed_DoesNotGrantAgain()
    {
        var stateStore = new FakeDiscordLinkRewardStateStore();
        stateStore.MarkClaimed("player-1");
        stateStore.MarkPendingReward("player-1");
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var service = new DiscordLinkRewardService(stateStore, itemGiver);

        bool delivered = service.DeliverPendingReward(CreatePlayer("player-1"));

        Assert.False(delivered);
        Assert.Equal(0, itemGiver.GiveCount);
        Assert.True(stateStore.HasClaimed("player-1"));
        Assert.False(stateStore.HasPendingReward("player-1"));
    }

    private static IServerPlayer CreatePlayer(string playerUid)
    {
        var proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
        ((TestServerPlayerProxy)(object)proxy).Values["get_PlayerUID"] = playerUid;
        return proxy;
    }

    private sealed class FakeDiscordLinkRewardStateStore : IDiscordLinkRewardStateStore
    {
        private readonly HashSet<string> claimed = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);

        public bool HasClaimed(string playerUid)
        {
            return claimed.Contains(playerUid);
        }

        public void MarkClaimed(string playerUid)
        {
            claimed.Add(playerUid);
        }

        public void ClearClaimed(string playerUid)
        {
            claimed.Remove(playerUid);
        }

        public bool HasPendingReward(string playerUid)
        {
            return pending.Contains(playerUid);
        }

        public void MarkPendingReward(string playerUid)
        {
            pending.Add(playerUid);
        }

        public void ClearPendingReward(string playerUid)
        {
            pending.Remove(playerUid);
        }

        public IReadOnlyCollection<string> GetClaimedPlayerUids()
        {
            return claimed.ToArray();
        }

        public IReadOnlyCollection<string> GetPendingRewardPlayerUids()
        {
            return pending.ToArray();
        }
    }

    private sealed class FakeDiscordLinkRewardItemGiver : IDiscordLinkRewardItemGiver
    {
        public int GiveCount { get; private set; }

        public void Give(IServerPlayer player)
        {
            GiveCount++;
        }
    }

    private class TestServerPlayerProxy : DispatchProxy
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            if (Values.TryGetValue(targetMethod.Name, out object value))
            {
                return value;
            }

            if (targetMethod.ReturnType == typeof(void))
            {
                return null;
            }

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
