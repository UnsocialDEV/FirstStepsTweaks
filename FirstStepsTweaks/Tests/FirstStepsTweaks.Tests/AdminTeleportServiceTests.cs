using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class AdminTeleportServiceTests
{
    [Fact]
    public void TeleportCallerToTarget_RecordsCallerBackLocation_AndTeleportsCaller()
    {
        var messenger = new FakePlayerMessenger();
        var backLocationStore = new FakeBackLocationStore();
        var teleporter = new FakePlayerTeleporter();
        var caller = CreatePlayer("caller", "Caller", 1, 2, 3);
        var target = CreatePlayer("target", "Target", 10, 20, 30);
        var service = new AdminTeleportService(
            new FakePlayerLookup(target),
            backLocationStore,
            teleporter,
            messenger);

        service.TeleportCallerToTarget(caller, "Target");

        Assert.Same(caller, backLocationStore.RecordedPlayer);
        Assert.Same(caller, teleporter.LastPlayer);
        Assert.Equal((10d, 20d, 30d), teleporter.LastDestination);
        Assert.Contains("Teleported to Target.", messenger.Messages);
    }

    [Fact]
    public void TeleportTargetToCaller_RecordsTargetBackLocation_AndTeleportsTarget()
    {
        var messenger = new FakePlayerMessenger();
        var backLocationStore = new FakeBackLocationStore();
        var teleporter = new FakePlayerTeleporter();
        var caller = CreatePlayer("caller", "Caller", 1, 2, 3);
        var target = CreatePlayer("target", "Target", 10, 20, 30);
        var service = new AdminTeleportService(
            new FakePlayerLookup(target),
            backLocationStore,
            teleporter,
            messenger);

        service.TeleportTargetToCaller(caller, "Target");

        Assert.Same(target, backLocationStore.RecordedPlayer);
        Assert.Same(target, teleporter.LastPlayer);
        Assert.Equal((1d, 2d, 3d), teleporter.LastDestination);
        Assert.Contains("Teleported Target to you.", messenger.Messages);
    }

    [Fact]
    public void TeleportCallerToTarget_RejectsOfflineTargets()
    {
        var messenger = new FakePlayerMessenger();
        var service = new AdminTeleportService(
            new FakePlayerLookup(null),
            new FakeBackLocationStore(),
            new FakePlayerTeleporter(),
            messenger);

        service.TeleportCallerToTarget(CreatePlayer("caller", "Caller", 1, 2, 3), "Missing");

        Assert.Contains("Player not found.", messenger.Messages);
    }

    [Fact]
    public void TeleportTargetToCaller_RejectsSelfTargeting()
    {
        var messenger = new FakePlayerMessenger();
        var caller = CreatePlayer("caller", "Caller", 1, 2, 3);
        var service = new AdminTeleportService(
            new FakePlayerLookup(caller),
            new FakeBackLocationStore(),
            new FakePlayerTeleporter(),
            messenger);

        service.TeleportTargetToCaller(caller, "Caller");

        Assert.Contains("You cannot teleport to yourself.", messenger.Messages);
    }

    private static IServerPlayer CreatePlayer(string uid, string name, double x, double y, double z)
    {
        var player = DispatchProxy.Create<IServerPlayer, ServerPlayerProxy>();
        var proxy = (ServerPlayerProxy)(object)player;
        proxy.PlayerUid = uid;
        proxy.PlayerName = name;
        proxy.Entity = CreateEntity(x, y, z);
        return player;
    }

    private static EntityPlayer CreateEntity(double x, double y, double z)
    {
        var entity = (EntityPlayer)RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayer));
        typeof(Vintagestory.API.Common.Entities.Entity).GetField("Pos", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(entity, new EntityPos(x, y, z));
        return entity;
    }

    private sealed class FakePlayerLookup : IPlayerLookup
    {
        private readonly IServerPlayer? player;

        public FakePlayerLookup(IServerPlayer? player)
        {
            this.player = player;
        }

        public IServerPlayer FindOnlinePlayerByUid(string uid)
        {
            return player != null && player.PlayerUID == uid ? player : null!;
        }

        public IServerPlayer FindOnlinePlayerByName(string name)
        {
            return player != null && player.PlayerName == name ? player : null!;
        }
    }

    private sealed class FakeBackLocationStore : IBackLocationStore
    {
        public IServerPlayer? RecordedPlayer { get; private set; }

        public void RecordCurrentLocation(IServerPlayer player)
        {
            RecordedPlayer = player;
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

    private sealed class FakePlayerTeleporter : IPlayerTeleporter
    {
        public IServerPlayer? LastPlayer { get; private set; }

        public (double X, double Y, double Z)? LastDestination { get; private set; }

        public void Teleport(IServerPlayer player, Vec3d destination)
        {
            LastPlayer = player;
            LastDestination = (destination.X, destination.Y, destination.Z);
        }
    }

    private sealed class FakePlayerMessenger : IPlayerMessenger
    {
        public List<string> Messages { get; } = new();

        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
            Messages.Add(message);
        }

        public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
        {
            Messages.Add(message);
        }

        public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
        {
            Messages.Add(message);
        }

        public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
        {
            Messages.Add(message);
        }

        public void SendIngameError(IServerPlayer player, string code, string message)
        {
            Messages.Add(message);
        }
    }

    private class ServerPlayerProxy : DispatchProxy
    {
        public string PlayerUid { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public EntityPlayer? Entity { get; set; }

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
