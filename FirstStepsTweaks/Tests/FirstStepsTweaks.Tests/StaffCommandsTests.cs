using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class StaffCommandsTests
{
    [Fact]
    public void ResolvePersistentTarget_UsesOnlineNameMatch()
    {
        IServerPlayer player = StaffTestServerPlayerFactory.Create("uid-1", "Admin");
        var resolver = new StaffTargetResolver(new PlayerLookup(StaffTestCoreServerApiFactory.Create(player)));

        StaffCommandTarget target = resolver.ResolvePersistentTarget("Admin");

        Assert.Equal("uid-1", target.PlayerUid);
        Assert.Equal("Admin", target.DisplayName);
        Assert.Same(player, target.OnlinePlayer);
    }

    [Fact]
    public void ResolvePersistentTarget_UsesOnlineUidMatch()
    {
        IServerPlayer player = StaffTestServerPlayerFactory.Create("uid-1", "Admin");
        var resolver = new StaffTargetResolver(new PlayerLookup(StaffTestCoreServerApiFactory.Create(player)));

        StaffCommandTarget target = resolver.ResolvePersistentTarget("uid-1");

        Assert.Equal("uid-1", target.PlayerUid);
        Assert.Same(player, target.OnlinePlayer);
    }

    [Fact]
    public void ResolvePersistentTarget_FallsBackToOfflineUid()
    {
        var resolver = new StaffTargetResolver(new PlayerLookup(StaffTestCoreServerApiFactory.Create()));

        StaffCommandTarget target = resolver.ResolvePersistentTarget("offline-uid");

        Assert.Equal("offline-uid", target.PlayerUid);
        Assert.Null(target.OnlinePlayer);
    }

    [Fact]
    public void ResolveOnlineTarget_ReturnsNullForOfflineUid()
    {
        var resolver = new StaffTargetResolver(new PlayerLookup(StaffTestCoreServerApiFactory.Create()));

        Assert.Null(resolver.ResolveOnlineTarget("offline-uid"));
    }
}
