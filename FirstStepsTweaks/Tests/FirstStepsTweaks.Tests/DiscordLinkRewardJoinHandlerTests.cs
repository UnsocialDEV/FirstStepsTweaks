using System.Reflection;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordLinkRewardJoinHandlerTests
{
    [Fact]
    public void OnPlayerNowPlaying_WhenPendingRewardDelivered_SendsPlayerMessage()
    {
        var stateStore = new FakeDiscordLinkRewardStateStore();
        stateStore.MarkPendingReward("player-1");
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var messenger = new FakePlayerMessenger();
        var player = CreatePlayer("player-1");
        var handler = new DiscordLinkRewardJoinHandler(
            new DiscordLinkRewardService(stateStore, itemGiver),
            new DelayedPlayerActionScheduler(CreateApi(player)),
            messenger);

        handler.OnPlayerNowPlaying(player);

        Assert.Equal(1, itemGiver.GiveCount);
        Assert.Equal(1, messenger.DualCount);
        Assert.Equal("Discord account linked. You received 10 rusty gears.", messenger.LastDualMessage);
    }

    [Fact]
    public void OnPlayerNowPlaying_WhenNoPendingReward_DoesNothing()
    {
        var messenger = new FakePlayerMessenger();
        var player = CreatePlayer("player-1");
        var handler = new DiscordLinkRewardJoinHandler(
            new DiscordLinkRewardService(new FakeDiscordLinkRewardStateStore(), new FakeDiscordLinkRewardItemGiver()),
            new DelayedPlayerActionScheduler(CreateApi(player)),
            messenger);

        handler.OnPlayerNowPlaying(player);

        Assert.Equal(0, messenger.DualCount);
    }

    private static IServerPlayer CreatePlayer(string playerUid)
    {
        var proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
        ((TestServerPlayerProxy)(object)proxy).Values["get_PlayerUID"] = playerUid;
        return proxy;
    }

    private static ICoreServerAPI CreateApi(IServerPlayer player)
    {
        IServerWorldAccessor world = DispatchProxy.Create<IServerWorldAccessor, TestServerWorldAccessorProxy>();
        ((TestServerWorldAccessorProxy)(object)world).AllOnlinePlayers = new IServerPlayer[] { player };

        IServerEventAPI eventApi = DispatchProxy.Create<IServerEventAPI, TestServerEventApiProxy>();
        ICoreServerAPI api = DispatchProxy.Create<ICoreServerAPI, TestCoreServerApiProxy>();
        var apiProxy = (TestCoreServerApiProxy)(object)api;
        apiProxy.Event = eventApi;
        apiProxy.World = world;
        return api;
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

    private sealed class FakePlayerMessenger : IPlayerMessenger
    {
        public int DualCount { get; private set; }

        public string? LastDualMessage { get; private set; }

        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
        }

        public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
        {
        }

        public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
        {
            DualCount++;
            LastDualMessage = message;
        }

        public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
        {
            DualCount++;
            LastDualMessage = message;
        }

        public void SendIngameError(IServerPlayer player, string code, string message)
        {
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

    private class TestCoreServerApiProxy : DispatchProxy
    {
        public IServerEventAPI? Event { get; set; }

        public IServerWorldAccessor? World { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Event" => Event,
                "get_World" => World,
                _ => targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null
            };
        }
    }

    private class TestServerEventApiProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "RegisterCallback")
            {
                ((Delegate)args![0]!).DynamicInvoke(0f);
                return 0L;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    private class TestServerWorldAccessorProxy : DispatchProxy
    {
        public IServerPlayer[] AllOnlinePlayers { get; set; } = Array.Empty<IServerPlayer>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_AllOnlinePlayers")
            {
                return AllOnlinePlayers;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
