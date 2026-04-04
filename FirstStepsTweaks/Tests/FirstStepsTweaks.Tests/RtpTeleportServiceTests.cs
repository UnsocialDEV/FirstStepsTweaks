using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class RtpTeleportServiceTests
{
    [Fact]
    public void Execute_BypassesCooldownButStillUsesWarmup_WhenPlayerHasPrivilege()
    {
        var config = CreateConfig(useWarmup: true);
        var messenger = new FakePlayerMessenger();
        var backLocationStore = new FakeBackLocationStore();
        var warmupService = new FakeTeleportWarmupService();
        var cooldownStore = new RtpCooldownStore();
        var player = CreatePlayer(hasBypassPrivilege: true, 1, 65, 1);
        var previousUse = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        cooldownStore.SetLastUse(player.PlayerUID, previousUse);
        var service = new RtpTeleportService(
            config,
            messenger,
            backLocationStore,
            warmupService,
            new FakePlayerTeleporter(),
            new PlayerTeleportWarmupResolver(),
            cooldownStore,
            new FakeDestinationResolver(new Vec3d(100.5, 70, 200.5)));

        service.Execute(player);

        Assert.NotNull(warmupService.Request);
        Assert.False(backLocationStore.RecordCalled);
        Assert.False(warmupService.Request!.AllowBypass);
        Assert.Contains("You bypassed the /rtp cooldown.", GetPlayerProxy(player).SentMessages);
    }

    [Fact]
    public void Execute_TeleportsImmediately_WhenWarmupIsDisabled()
    {
        var config = CreateConfig(useWarmup: false);
        var messenger = new FakePlayerMessenger();
        var backLocationStore = new FakeBackLocationStore();
        var warmupService = new FakeTeleportWarmupService();
        var playerTeleporter = new FakePlayerTeleporter();
        var cooldownStore = new RtpCooldownStore();
        var player = CreatePlayer(hasBypassPrivilege: true, 1, 65, 1);
        var previousUse = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        cooldownStore.SetLastUse(player.PlayerUID, previousUse);
        var service = new RtpTeleportService(
            config,
            messenger,
            backLocationStore,
            warmupService,
            playerTeleporter,
            new PlayerTeleportWarmupResolver(),
            cooldownStore,
            new FakeDestinationResolver(new Vec3d(100.5, 70, 200.5)));

        service.Execute(player);

        Assert.Null(warmupService.Request);
        Assert.True(backLocationStore.RecordCalled);
        Assert.Contains("Teleported to a random location.", messenger.InfoMessages);
        Assert.Contains("You bypassed the /rtp cooldown.", GetPlayerProxy(player).SentMessages);
        Assert.True(cooldownStore.TryGetLastUse(player.PlayerUID, out long updatedUse));
        Assert.True(updatedUse >= previousUse);
        Assert.Equal(1, playerTeleporter.CallCount);
        Assert.Equal(100.5, playerTeleporter.LastDestination!.X);
        Assert.Equal(70, playerTeleporter.LastDestination.Y);
        Assert.Equal(200.5, playerTeleporter.LastDestination.Z);
    }

    private static FirstStepsTweaksConfig CreateConfig(bool useWarmup)
    {
        return new FirstStepsTweaksConfig
        {
            Teleport = new TeleportConfig
            {
                WarmupSeconds = 10,
                DonatorWarmupSeconds = 10,
                TickIntervalMs = 1000,
                CancelMoveThreshold = 0.1
            },
            Rtp = new RtpConfig
            {
                UseWarmup = useWarmup,
                CooldownSeconds = 300
            }
        };
    }

    private static IServerPlayer CreatePlayer(bool hasBypassPrivilege, double x, double y, double z)
    {
        var player = DispatchProxy.Create<IServerPlayer, ServerPlayerProxy>();
        var proxy = GetPlayerProxy(player);
        proxy.PlayerUid = "player-1";
        proxy.PlayerName = "Traveler";
        proxy.Entity = CreateEntity(x, y, z);

        if (hasBypassPrivilege)
        {
            proxy.Privileges.Add(TeleportBypass.Privilege);
        }

        return player;
    }

    private static ServerPlayerProxy GetPlayerProxy(IServerPlayer player)
    {
        return (ServerPlayerProxy)(object)player;
    }

    private static Entity CreateEntity(double x, double y, double z)
    {
        var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayer));
        typeof(Entity).GetField("Pos", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(entity, new EntityPos(x, y, z));
        return entity;
    }

    private sealed class FakeDestinationResolver : IRtpDestinationResolver
    {
        private readonly Vec3d destination;

        public FakeDestinationResolver(Vec3d destination)
        {
            this.destination = destination;
        }

        public bool TryResolveDestination(IServerPlayer player, out Vec3d destination)
        {
            destination = this.destination;
            return true;
        }
    }

    private sealed class FakePlayerMessenger : IPlayerMessenger
    {
        public List<string> InfoMessages { get; } = new();

        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
            InfoMessages.Add(message);
        }

        public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
        {
        }

        public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
        {
        }

        public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
        {
        }

        public void SendIngameError(IServerPlayer player, string code, string message)
        {
        }
    }

    private sealed class FakeBackLocationStore : IBackLocationStore
    {
        public bool RecordCalled { get; private set; }

        public void RecordCurrentLocation(IServerPlayer player)
        {
            RecordCalled = true;
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
        public int CallCount { get; private set; }

        public Vec3d? LastDestination { get; private set; }

        public void Teleport(IServerPlayer player, Vec3d destination)
        {
            CallCount++;
            LastDestination = destination;
        }
    }

    private class ServerPlayerProxy : DispatchProxy
    {
        public string PlayerUid { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public Entity? Entity { get; set; }

        public HashSet<string> Privileges { get; } = new(StringComparer.Ordinal);

        public List<string> SentMessages { get; } = new();

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
                "HasPrivilege" => Privileges.Contains((string)args![0]!),
                "SendMessage" => RecordMessage(args),
                _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
            };
        }

        private object? RecordMessage(object?[]? args)
        {
            if (args?.Length > 1 && args[1] is string message)
            {
                SentMessages.Add(message);
            }

            return null;
        }
    }
}
