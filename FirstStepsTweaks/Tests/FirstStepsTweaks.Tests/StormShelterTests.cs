using System;
using System.Collections.Generic;
using System.Reflection;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class StormShelterTests
{
    [Fact]
    public void StormShelterStore_PersistsCoordinates()
    {
        var api = TestCoreServerApiFactory.Create();
        var player = TestServerPlayerFactory.Create("uid-1", "Admin", 12, 34, 56);
        var store = new StormShelterStore(api);

        store.SetStormShelter(player);

        Assert.True(store.TryGetStormShelter(out Vec3d position));
        Assert.NotNull(position);
        Assert.Equal(12, position.X);
        Assert.Equal(34, position.Y);
        Assert.Equal(56, position.Z);
    }

    [Fact]
    public void StormShelterStore_ReturnsFalse_WhenUnset()
    {
        var store = new StormShelterStore(TestCoreServerApiFactory.Create());

        bool found = store.TryGetStormShelter(out Vec3d position);

        Assert.False(found);
        Assert.Null(position);
    }

    [Fact]
    public void StormShelterStore_ReturnsFalse_WhenStoredDataIsMalformed()
    {
        var api = TestCoreServerApiFactory.Create();
        api.WorldManager.SaveGame.StoreData("fst_stormshelter", new[] { 1d, 2d });
        var store = new StormShelterStore(api);

        bool found = store.TryGetStormShelter(out Vec3d position);

        Assert.False(found);
        Assert.Null(position);
    }

    [Fact]
    public void StormShelterStore_OverwritesPriorLocation()
    {
        var api = TestCoreServerApiFactory.Create();
        var firstPlayer = TestServerPlayerFactory.Create("uid-1", "Admin", 1, 2, 3);
        var secondPlayer = TestServerPlayerFactory.Create("uid-1", "Admin", 4, 5, 6);
        var store = new StormShelterStore(api);

        store.SetStormShelter(firstPlayer);
        store.SetStormShelter(secondPlayer);

        Assert.True(store.TryGetStormShelter(out Vec3d position));
        Assert.NotNull(position);
        Assert.Equal(4, position.X);
        Assert.Equal(5, position.Y);
        Assert.Equal(6, position.Z);
    }

    [Fact]
    public void TeleportService_ReturnsNotSet_WhenNoStormShelterExists()
    {
        var api = TestCoreServerApiFactory.Create();
        var player = TestServerPlayerFactory.Create("uid-2", "Builder", 9, 8, 7);
        var backLocationStore = new TestBackLocationStore();
        var service = new StormShelterTeleportService(new StormShelterStore(api), backLocationStore);
        bool teleported = false;

        StormShelterTeleportResult result = service.TryTeleport(player, (_, _, _) => teleported = true);

        Assert.Equal(StormShelterTeleportResult.NotSet, result);
        Assert.False(teleported);
        Assert.False(backLocationStore.RecordCalled);
    }

    [Fact]
    public void TeleportService_RecordsBackLocation_AndTeleportsImmediately()
    {
        var api = TestCoreServerApiFactory.Create();
        var admin = TestServerPlayerFactory.Create("uid-1", "Admin", 20, 30, 40);
        var player = TestServerPlayerFactory.Create("uid-2", "Traveler", 1, 2, 3);
        var backLocationStore = new TestBackLocationStore();
        var store = new StormShelterStore(api);
        var service = new StormShelterTeleportService(store, backLocationStore);
        (double X, double Y, double Z)? teleportTarget = null;

        store.SetStormShelter(admin);
        StormShelterTeleportResult result = service.TryTeleport(player, (x, y, z) => teleportTarget = (x, y, z));

        Assert.Equal(StormShelterTeleportResult.Success, result);
        Assert.True(backLocationStore.RecordCalled);
        Assert.Same(player, backLocationStore.RecordedPlayer);
        Assert.Equal((1d, 2d, 3d), backLocationStore.RecordedPosition);
        Assert.Equal((20d, 30d, 40d), teleportTarget);
    }

    private sealed class TestBackLocationStore : IBackLocationStore
    {
        public bool RecordCalled { get; private set; }

        public IServerPlayer? RecordedPlayer { get; private set; }

        public (double X, double Y, double Z)? RecordedPosition { get; private set; }

        public void RecordCurrentLocation(IServerPlayer player)
        {
            RecordCalled = true;
            RecordedPlayer = player;
            RecordedPosition = player?.Entity?.Pos == null
                ? null
                : (player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);
        }

        public bool TryGet(string playerUid, out Vec3d location)
        {
            location = null!;
            return false;
        }

        public void Set(string playerUid, Vec3d location)
        {
        }
    }

    private static class TestCoreServerApiFactory
    {
        public static ICoreServerAPI Create()
        {
            var saveGame = DispatchProxy.Create<ISaveGame, SaveGameProxy>();
            var saveGameProxy = (SaveGameProxy)(object)saveGame;
            var worldManager = DispatchProxy.Create<IWorldManagerAPI, WorldManagerProxy>();
            var worldManagerProxy = (WorldManagerProxy)(object)worldManager;
            var api = DispatchProxy.Create<ICoreServerAPI, CoreServerApiProxy>();
            var apiProxy = (CoreServerApiProxy)(object)api;

            worldManagerProxy.SaveGame = saveGame;
            apiProxy.WorldManager = worldManager;

            return api;
        }
    }

    private static class TestServerPlayerFactory
    {
        public static IServerPlayer Create(string playerUid, string playerName, double x, double y, double z)
        {
            var player = DispatchProxy.Create<IServerPlayer, ServerPlayerProxy>();
            var playerProxy = (ServerPlayerProxy)(object)player;
            playerProxy.PlayerUid = playerUid;
            playerProxy.PlayerName = playerName;
            playerProxy.Entity = CreateEntity(x, y, z);

            return player;
        }

        private static Entity CreateEntity(double x, double y, double z)
        {
            var entity = (Entity)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayer));
            typeof(Entity).GetField("Pos", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(entity, new EntityPos(x, y, z));
            return entity;
        }
    }

    private class CoreServerApiProxy : DispatchProxy
    {
        public IWorldManagerAPI? WorldManager { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_WorldManager")
            {
                return WorldManager;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    private class WorldManagerProxy : DispatchProxy
    {
        public ISaveGame? SaveGame { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_SaveGame")
            {
                return SaveGame;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    private class SaveGameProxy : DispatchProxy
    {
        private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            switch (targetMethod.Name)
            {
                case "StoreData":
                    values[(string)args![0]!] = args[1];
                    return null;
                case "GetData":
                    if (values.TryGetValue((string)args![0]!, out object? value))
                    {
                        return value;
                    }

                    return targetMethod.ReturnType.IsValueType
                        ? Activator.CreateInstance(targetMethod.ReturnType)
                        : null;
                default:
                    return targetMethod.ReturnType.IsValueType
                        ? Activator.CreateInstance(targetMethod.ReturnType)
                        : null;
            }
        }
    }

    private class ServerPlayerProxy : DispatchProxy
    {
        public string PlayerUid { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public Entity? Entity { get; set; }

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
                "get_Entity" => Entity,
                _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
            };
        }
    }
}
