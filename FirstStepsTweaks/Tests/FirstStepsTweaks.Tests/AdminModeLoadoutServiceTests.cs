using System.Reflection;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public class AdminModeLoadoutServiceTests
    {
        [Fact]
        public void SnapshotLiveLoadout_UsesAdminModeScope()
        {
            IServerPlayer player = CreatePlayer("uid-admin", "Admin");
            var loadoutManager = new FakePlayerLoadoutManager();
            var service = new AdminModeLoadoutService(CreateApi(), loadoutManager, new FakeWorldCoordinateReader());

            service.SnapshotLiveLoadout(player);

            Assert.Equal(PlayerLoadoutScope.AdminMode, loadoutManager.LastScope);
        }

        [Fact]
        public void SnapshotInitialAdminLoadout_UsesAdminModeInitialSeedScope()
        {
            IServerPlayer player = CreatePlayer("uid-admin", "Admin");
            var loadoutManager = new FakePlayerLoadoutManager();
            var service = new AdminModeLoadoutService(CreateApi(), loadoutManager, new FakeWorldCoordinateReader());

            service.SnapshotInitialAdminLoadout(player);

            Assert.Equal(PlayerLoadoutScope.AdminModeInitialSeed, loadoutManager.LastScope);
        }

        [Fact]
        public void TryEquipLoadout_RestoresCurrentLoadout_WhenTargetRestoreFails()
        {
            IServerPlayer player = CreatePlayer("uid-admin", "Admin");
            var loadoutManager = new FakePlayerLoadoutManager { TryRestoreResults = { false, true } };
            var service = new AdminModeLoadoutService(CreateApi(), loadoutManager, new FakeWorldCoordinateReader());
            var current = new[]
            {
                new PlayerInventorySnapshot { InventoryClassName = "hotbar" }
            };
            var target = new[]
            {
                new PlayerInventorySnapshot { InventoryClassName = "character" }
            };

            bool result = service.TryEquipLoadout(player, current, target);

            Assert.False(result);
            Assert.True(loadoutManager.ClearCalled);
            Assert.Equal(2, loadoutManager.TryRestoreCallCount);
            Assert.Equal("character", loadoutManager.RestoreInputs[0][0].InventoryClassName);
            Assert.Equal("hotbar", loadoutManager.RestoreInputs[1][0].InventoryClassName);
        }

        private static ICoreServerAPI CreateApi()
        {
            ILogger logger = DispatchProxy.Create<ILogger, LoggerProxy>();
            ICoreServerAPI api = DispatchProxy.Create<ICoreServerAPI, CoreServerApiProxy>();
            ((CoreServerApiProxy)(object)api).Logger = logger;
            return api;
        }

        private static IServerPlayer CreatePlayer(string playerUid, string playerName)
        {
            IServerPlayer proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
            var state = (TestServerPlayerProxy)(object)proxy;
            state.PlayerUid = playerUid;
            state.PlayerName = playerName;
            return proxy;
        }

        private sealed class FakePlayerLoadoutManager : IPlayerLoadoutManager
        {
            public bool ClearCalled { get; private set; }

            public int TryRestoreCallCount { get; private set; }

            public PlayerLoadoutScope LastScope { get; private set; }

            public List<bool> TryRestoreResults { get; } = new();

            public List<List<PlayerInventorySnapshot>> RestoreInputs { get; } = new();

            public List<PlayerInventorySnapshot> Snapshot(IServerPlayer player, List<string> debugEntries = null)
            {
                return Snapshot(player, PlayerLoadoutScope.Gravestone, debugEntries);
            }

            public List<PlayerInventorySnapshot> Snapshot(IServerPlayer player, PlayerLoadoutScope scope, List<string> debugEntries = null)
            {
                LastScope = scope;
                return new List<PlayerInventorySnapshot>();
            }

            public void Clear(IServerPlayer player, IReadOnlyCollection<PlayerInventorySnapshot> snapshots)
            {
                ClearCalled = true;
            }

            public bool HasAnyItems(IServerPlayer player)
            {
                return false;
            }

            public int DuplicateToPlayer(IReadOnlyCollection<PlayerInventorySnapshot> snapshots, IServerPlayer targetPlayer, BlockPos fallbackPos)
            {
                return 0;
            }

            public bool TryRestore(IReadOnlyCollection<PlayerInventorySnapshot> snapshots, IServerPlayer targetPlayer, BlockPos fallbackPos, out int transferredStacks, out int failedStacks)
            {
                TryRestoreCallCount++;
                RestoreInputs.Add(snapshots == null ? new List<PlayerInventorySnapshot>() : new List<PlayerInventorySnapshot>(snapshots));
                transferredStacks = snapshots?.Count ?? 0;
                failedStacks = 0;
                int resultIndex = TryRestoreCallCount - 1;
                return resultIndex < TryRestoreResults.Count ? TryRestoreResults[resultIndex] : true;
            }
        }

        private sealed class FakeWorldCoordinateReader : IWorldCoordinateReader
        {
            public Vec3d GetExactPosition(IServerPlayer player) => new Vec3d(1, 2, 3);

            public Vec3d GetExactPosition(Vintagestory.API.Common.Entities.Entity entity) => new Vec3d(1, 2, 3);

            public BlockPos GetBlockPosition(IServerPlayer player) => new BlockPos(1, 2, 3);

            public BlockPos GetBlockPosition(Vintagestory.API.Common.Entities.Entity entity) => new BlockPos(1, 2, 3);

            public int? GetDimension(IServerPlayer player) => 0;

            public int? GetDimension(Vintagestory.API.Common.Entities.Entity entity) => 0;
        }

        private class TestServerPlayerProxy : DispatchProxy
        {
            public string PlayerUid { get; set; } = string.Empty;

            public string PlayerName { get; set; } = string.Empty;

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod == null)
                {
                    return null;
                }

                return targetMethod.Name switch
                {
                    "get_PlayerUID" => PlayerUid,
                    "get_PlayerName" => PlayerName,
                    _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
                };
            }
        }

        private class CoreServerApiProxy : DispatchProxy
        {
            public ILogger? Logger { get; set; }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod?.Name == "get_Logger")
                {
                    return Logger;
                }

                return targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }

        private class LoggerProxy : DispatchProxy
        {
            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                return targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }
    }
}
