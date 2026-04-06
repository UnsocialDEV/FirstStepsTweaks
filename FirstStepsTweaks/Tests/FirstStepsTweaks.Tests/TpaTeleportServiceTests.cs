using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class TpaTeleportServiceTests
{
    [Fact]
    public void BeginTeleport_TeleportsRequesterToTarget_AndRecordsRequesterBackLocation()
    {
        var backLocationStore = new FakeBackLocationStore();
        var warmupService = new FakeTeleportWarmupService();
        var teleporter = new FakePlayerTeleporter();
        var service = CreateService(backLocationStore, warmupService, teleporter);
        var requester = CreatePlayer("requester", "Requester", 1, 2, 3);
        var target = CreatePlayer("target", "Target", 10, 20, 30);
        var request = new TpaRequestRecord
        {
            RequesterUid = "requester",
            RequesterName = "Requester",
            TargetUid = "target",
            TargetName = "Target",
            Direction = TpaRequestDirection.RequesterToTarget
        };

        service.BeginTeleport(request, requester, target);
        warmupService.Request!.ExecuteTeleport();

        Assert.Same(requester, warmupService.Request.Player);
        Assert.Same(requester, backLocationStore.RecordedPlayer);
        Assert.Equal((10d, 20d, 30d), teleporter.LastDestination);
    }

    [Fact]
    public void BeginTeleport_UsesMovedPlayerWarmup_WhenTargetTravelsToRequester()
    {
        var backLocationStore = new FakeBackLocationStore();
        var warmupService = new FakeTeleportWarmupService();
        var teleporter = new FakePlayerTeleporter();
        var service = CreateService(backLocationStore, warmupService, teleporter);
        var requester = CreatePlayer("requester", "Requester", 1, 2, 3, roleCode: "suplayer");
        var target = CreatePlayer("target", "Target", 10, 20, 30, roleCode: "supporter");
        var request = new TpaRequestRecord
        {
            RequesterUid = "requester",
            RequesterName = "Requester",
            TargetUid = "target",
            TargetName = "Target",
            Direction = TpaRequestDirection.TargetToRequester
        };

        service.BeginTeleport(request, requester, target);
        warmupService.Request!.ExecuteTeleport();

        Assert.Same(target, warmupService.Request.Player);
        Assert.Equal(3, warmupService.Request.WarmupSeconds);
        Assert.Same(target, backLocationStore.RecordedPlayer);
        Assert.Equal((1d, 2d, 3d), teleporter.LastDestination);
    }

    private static TpaTeleportService CreateService(
        IBackLocationStore backLocationStore,
        ITeleportWarmupService warmupService,
        IPlayerTeleporter teleporter)
    {
        var config = new FirstStepsTweaksConfig
        {
            Teleport = new TeleportConfig
            {
                WarmupSeconds = 10,
                DonatorWarmupSeconds = 3,
                TickIntervalMs = 1000,
                CancelMoveThreshold = 0.1
            }
        };

        return new TpaTeleportService(
            config,
            backLocationStore,
            warmupService,
            teleporter,
            new PlayerTeleportWarmupResolver());
    }

    private static IServerPlayer CreatePlayer(string uid, string name, double x, double y, double z, string roleCode = "suplayer")
    {
        var player = DispatchProxy.Create<IServerPlayer, ServerPlayerProxy>();
        var proxy = (ServerPlayerProxy)(object)player;
        proxy.PlayerUid = uid;
        proxy.PlayerName = name;
        proxy.RoleCode = roleCode;
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

    private sealed class FakeTeleportWarmupService : ITeleportWarmupService
    {
        public TeleportWarmupRequest? Request { get; private set; }

        public void Begin(TeleportWarmupRequest request)
        {
            Request = request;
        }
    }

    private sealed class FakePlayerTeleporter : IPlayerTeleporter
    {
        public (double X, double Y, double Z)? LastDestination { get; private set; }

        public void Teleport(IServerPlayer player, Vec3d destination)
        {
            LastDestination = (destination.X, destination.Y, destination.Z);
        }
    }

    private class ServerPlayerProxy : DispatchProxy
    {
        public string PlayerUid { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public string RoleCode { get; set; } = string.Empty;

        public EntityPlayer? Entity { get; set; }

        public HashSet<string> Privileges { get; } = new(StringComparer.Ordinal);

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
                "get_RoleCode" => RoleCode,
                "get_Entity" => Entity,
                "HasPrivilege" => Privileges.Contains((string)args![0]!),
                _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
            };
        }
    }
}
