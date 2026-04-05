using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
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
            new SuccessfulDestinationResolver(new Vec3d(100.5, 70, 200.5)),
            new NoopDelayedPlayerActionScheduler());

        service.Execute(player);

        Assert.NotNull(warmupService.Request);
        Assert.False(backLocationStore.RecordCalled);
        Assert.False(warmupService.Request!.AllowBypass);
        Assert.Equal("Found a safe spot. Teleporting in 10 seconds. Don't move.", warmupService.Request.WarmupMessage);
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
            new SuccessfulDestinationResolver(new Vec3d(100.5, 70, 200.5)),
            new NoopDelayedPlayerActionScheduler());

        service.Execute(player);

        Assert.Null(warmupService.Request);
        Assert.True(backLocationStore.RecordCalled);
        Assert.Contains("Searching for a safe place to drop you. This can take a few seconds.", messenger.InfoMessages);
        Assert.Contains("Teleported you to a safe random location.", messenger.InfoMessages);
        Assert.Contains("You bypassed the /rtp cooldown.", GetPlayerProxy(player).SentMessages);
        Assert.True(cooldownStore.TryGetLastUse(player.PlayerUID, out long updatedUse));
        Assert.True(updatedUse >= previousUse);
        Assert.Equal(1, playerTeleporter.CallCount);
        Assert.Equal(100.5, playerTeleporter.LastDestination!.X);
        Assert.Equal(70, playerTeleporter.LastDestination.Y);
        Assert.Equal(200.5, playerTeleporter.LastDestination.Z);
    }

    [Fact]
    public void Execute_RetriesUsingSameSearchSession_WhenChunksAreStillLoading()
    {
        var config = CreateConfig(useWarmup: false);
        var messenger = new FakePlayerMessenger();
        var backLocationStore = new FakeBackLocationStore();
        var warmupService = new FakeTeleportWarmupService();
        var playerTeleporter = new FakePlayerTeleporter();
        var cooldownStore = new RtpCooldownStore();
        var player = CreatePlayer(hasBypassPrivilege: false, 1, 65, 1);
        var scheduler = new ImmediateDelayedPlayerActionScheduler(player);
        var resolver = new RetryAwareDestinationResolver();
        var service = new RtpTeleportService(
            config,
            messenger,
            backLocationStore,
            warmupService,
            playerTeleporter,
            new PlayerTeleportWarmupResolver(),
            cooldownStore,
            resolver,
            scheduler);

        service.Execute(player);

        Assert.Equal(2, resolver.CallCount);
        Assert.NotNull(resolver.InitialSession);
        Assert.NotNull(resolver.RetrySession);
        Assert.Same(resolver.InitialSession!.ChunkCandidates, resolver.RetrySession!.ChunkCandidates);
        Assert.Equal(resolver.InitialSession.BatchStartIndex, resolver.RetrySession.BatchStartIndex);
        Assert.Equal(1, resolver.RetrySession.BatchRetryCount);
        Assert.True(backLocationStore.RecordCalled);
        Assert.Equal(1, playerTeleporter.CallCount);
        Assert.Contains("Searching for a safe place to drop you. This can take a few seconds.", messenger.InfoMessages);
        Assert.Contains("Teleported you to a safe random location.", messenger.InfoMessages);
    }

    [Fact]
    public void Execute_AdvancesToNextBatch_BeforeFailingSearch()
    {
        var config = CreateConfig(useWarmup: false);
        var messenger = new FakePlayerMessenger();
        var service = new RtpTeleportService(
            config,
            messenger,
            new FakeBackLocationStore(),
            new FakeTeleportWarmupService(),
            new FakePlayerTeleporter(),
            new PlayerTeleportWarmupResolver(),
            new RtpCooldownStore(),
            new BatchAdvanceDestinationResolver(),
            new NoopDelayedPlayerActionScheduler());

        service.Execute(CreatePlayer(hasBypassPrivilege: false, 1, 65, 1));

        Assert.Contains("Couldn't find a safe place this time. Please try again in a moment.", messenger.InfoMessages);
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

    private sealed class SuccessfulDestinationResolver : IRtpDestinationResolver
    {
        private readonly Vec3d destination;

        public SuccessfulDestinationResolver(Vec3d destination)
        {
            this.destination = destination;
        }

        public RtpDestinationResolutionResult ResolveDestination(IServerPlayer player, RtpSearchSession searchSession = null)
        {
            return new RtpDestinationResolutionResult
            {
                SearchSession = searchSession,
                Destination = destination
            };
        }
    }

    private sealed class RetryAwareDestinationResolver : IRtpDestinationResolver
    {
        public RtpSearchSession? InitialSession { get; private set; }

        public RtpSearchSession? RetrySession { get; private set; }

        public int CallCount { get; private set; }

        public RtpDestinationResolutionResult ResolveDestination(IServerPlayer player, RtpSearchSession searchSession = null)
        {
            CallCount++;

            if (CallCount == 1)
            {
                InitialSession = new RtpSearchSession(
                    new Vec2d(512000, 512000),
                    new Vec3d(1, 65, 1),
                    0,
                    new[] { new RtpChunkCandidate(10, 10, 0, new[] { new Vec2i(16, 16) }) });

                return new RtpDestinationResolutionResult
                {
                    SearchSession = InitialSession,
                    PendingChunkCount = 1
                };
            }

            RetrySession = searchSession;
            return new RtpDestinationResolutionResult
            {
                SearchSession = searchSession,
                Destination = new Vec3d(100.5, 70, 200.5)
            };
        }
    }

    private sealed class BatchAdvanceDestinationResolver : IRtpDestinationResolver
    {
        public RtpDestinationResolutionResult ResolveDestination(IServerPlayer player, RtpSearchSession searchSession = null)
        {
            if (searchSession == null)
            {
                return new RtpDestinationResolutionResult
                {
                    SearchSession = new RtpSearchSession(
                        new Vec2d(512000, 512000),
                        new Vec3d(1, 65, 1),
                        0,
                        new[]
                        {
                            new RtpChunkCandidate(10, 10, 0, new[] { new Vec2i(16, 16) }),
                            new RtpChunkCandidate(20, 20, 0, new[] { new Vec2i(16, 16) }),
                            new RtpChunkCandidate(30, 30, 0, new[] { new Vec2i(16, 16) }),
                            new RtpChunkCandidate(40, 40, 0, new[] { new Vec2i(16, 16) }),
                            new RtpChunkCandidate(50, 50, 0, new[] { new Vec2i(16, 16) }),
                            new RtpChunkCandidate(60, 60, 0, new[] { new Vec2i(16, 16) }),
                            new RtpChunkCandidate(70, 70, 0, new[] { new Vec2i(16, 16) }),
                            new RtpChunkCandidate(80, 80, 0, new[] { new Vec2i(16, 16) }),
                            new RtpChunkCandidate(90, 90, 0, new[] { new Vec2i(16, 16) })
                        }),
                    UnsafeTerrainCount = 8
                };
            }

            return new RtpDestinationResolutionResult
            {
                SearchSession = searchSession,
                UnsafeTerrainCount = 1
            };
        }
    }

    private sealed class NoopDelayedPlayerActionScheduler : IDelayedPlayerActionScheduler
    {
        public void Schedule(string playerUid, int delayMs, Action<IServerPlayer> action)
        {
        }
    }

    private sealed class ImmediateDelayedPlayerActionScheduler : IDelayedPlayerActionScheduler
    {
        private readonly IServerPlayer player;

        public ImmediateDelayedPlayerActionScheduler(IServerPlayer player)
        {
            this.player = player;
        }

        public void Schedule(string playerUid, int delayMs, Action<IServerPlayer> action)
        {
            action(player);
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
