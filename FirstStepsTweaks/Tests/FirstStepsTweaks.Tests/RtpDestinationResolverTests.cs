using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class RtpDestinationResolverTests
{
    [Fact]
    public void ResolveDestination_SkipsClaimedSafeDestination_AndReturnsNextUnclaimed()
    {
        var sharedOffsets = new[] { new Vec2i(16, 16) };
        var resolver = new RtpDestinationResolver(
            new RtpConfig(),
            new FakePlanner(
                new RtpChunkCandidate(1, 0, 0, sharedOffsets),
                new RtpChunkCandidate(2, 0, 0, sharedOffsets)),
            new FakeScanner(new Dictionary<(int ChunkX, int ChunkZ), Vec3d>
            {
                [(1, 0)] = new Vec3d(1.5, 90, 0.5),
                [(2, 0)] = new Vec3d(2.5, 91, 0.5)
            }),
            new FakeLandClaimAccessor(pos =>
                pos.X == 1 && pos.Z == 0
                    ? new LandClaimInfo("claim", "Claimed", "owner", "Owner")
                    : LandClaimInfo.None),
            new FakeWorldCoordinateReader(new Vec3d(0.5, 80, 0.5), 0),
            new FakeCenterResolver(new Vec2d(0.5, 0.5)));

        RtpDestinationResolutionResult result = resolver.ResolveDestination(CreatePlayer(0.5, 80, 0.5));

        Assert.True(result.Success);
        Assert.NotNull(result.Destination);
        Assert.Equal(2.5, result.Destination.X);
        Assert.Equal(91, result.Destination.Y);
        Assert.Equal(0.5, result.Destination.Z);
        Assert.Equal(1, result.ClaimRejectedCount);
    }

    [Fact]
    public void ResolveDestination_UsesCoordinateReaderDimension()
    {
        var coordinateReader = new FakeWorldCoordinateReader(new Vec3d(5.5, 80, 6.5), 9);
        var planner = new RecordingPlanner(new RtpChunkCandidate(2, 0, 9, new[] { new Vec2i(16, 16) }));
        var resolver = new RtpDestinationResolver(
            new RtpConfig { UsePlayerPositionAsCenter = true },
            planner,
            new FakeScanner(new Dictionary<(int ChunkX, int ChunkZ), Vec3d>
            {
                [(2, 0)] = new Vec3d(2.5, 90, 0.5)
            }),
            new FakeLandClaimAccessor(_ => LandClaimInfo.None),
            coordinateReader,
            new FakeCenterResolver(new Vec2d(5.5, 6.5)));

        RtpDestinationResolutionResult result = resolver.ResolveDestination(CreatePlayer(0.5, 80, 0.5));

        Assert.True(result.Success);
        Assert.NotNull(result.Destination);
        Assert.Equal(9, planner.LastDimension);
        Assert.Equal(5.5, planner.LastCenterX);
        Assert.Equal(6.5, planner.LastCenterZ);
        Assert.True(coordinateReader.PlayerPositionRequested);
        Assert.True(coordinateReader.PlayerDimensionRequested);
        Assert.NotNull(result.SearchSession);
    }

    [Fact]
    public void ResolveDestination_ReturnsPendingAndUnsafeCounts_ForCurrentBatch()
    {
        var sharedOffsets = new[] { new Vec2i(16, 16) };
        var searchSession = new RtpSearchSession(
            new Vec2d(512000, 512000),
            new Vec3d(10, 80, 20),
            0,
            new[]
            {
                new RtpChunkCandidate(1, 0, 0, sharedOffsets),
                new RtpChunkCandidate(2, 0, 0, sharedOffsets)
            });

        var resolver = new RtpDestinationResolver(
            new RtpConfig(),
            new FakePlanner(),
            new ResultScanner(
                new RtpColumnSafetyScanResult
                {
                    FailureKind = RtpColumnSafetyFailureKind.PendingChunkLoad,
                    FailureDetail = "pending"
                },
                new RtpColumnSafetyScanResult
                {
                    FailureKind = RtpColumnSafetyFailureKind.UnsafeTerrain,
                    FailureDetail = "unsafe"
                }),
            new FakeLandClaimAccessor(_ => LandClaimInfo.None),
            new FakeWorldCoordinateReader(new Vec3d(10, 80, 20), 0),
            new FakeCenterResolver(new Vec2d(512000, 512000)));

        RtpDestinationResolutionResult result = resolver.ResolveDestination(CreatePlayer(10, 80, 20), searchSession);

        Assert.False(result.Success);
        Assert.Equal(1, result.PendingChunkCount);
        Assert.Equal(1, result.UnsafeTerrainCount);
        Assert.Equal(0, result.ClaimRejectedCount);
        Assert.Same(searchSession, result.SearchSession);
    }

    private static IServerPlayer CreatePlayer(double x, double y, double z)
    {
        var player = DispatchProxy.Create<IServerPlayer, ServerPlayerProxy>();
        var playerProxy = (ServerPlayerProxy)(object)player;
        playerProxy.Entity = CreateEntity(x, y, z);
        return player;
    }

    private static Entity CreateEntity(double x, double y, double z)
    {
        var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayer));
        typeof(Entity).GetField("Pos", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(entity, new EntityPos(x, y, z));
        return entity;
    }

    private sealed class FakePlanner : IRtpColumnPlanner
    {
        private readonly IReadOnlyList<RtpChunkCandidate> chunkCandidates;

        public FakePlanner(params RtpChunkCandidate[] chunkCandidates)
        {
            this.chunkCandidates = chunkCandidates;
        }

        public IReadOnlyList<RtpChunkCandidate> PlanColumns(double centerX, double centerZ, int dimension)
        {
            return chunkCandidates;
        }
    }

    private sealed class RecordingPlanner : IRtpColumnPlanner
    {
        private readonly IReadOnlyList<RtpChunkCandidate> chunkCandidates;

        public RecordingPlanner(params RtpChunkCandidate[] chunkCandidates)
        {
            this.chunkCandidates = chunkCandidates;
        }

        public double LastCenterX { get; private set; }

        public double LastCenterZ { get; private set; }

        public int LastDimension { get; private set; }

        public IReadOnlyList<RtpChunkCandidate> PlanColumns(double centerX, double centerZ, int dimension)
        {
            LastCenterX = centerX;
            LastCenterZ = centerZ;
            LastDimension = dimension;
            return chunkCandidates;
        }
    }

    private sealed class FakeScanner : IRtpColumnSafetyScanner
    {
        private readonly IReadOnlyDictionary<(int ChunkX, int ChunkZ), Vec3d> destinations;

        public FakeScanner(IReadOnlyDictionary<(int ChunkX, int ChunkZ), Vec3d> destinations)
        {
            this.destinations = destinations;
        }

        public RtpColumnSafetyScanResult ScanCandidate(RtpChunkCandidate candidate)
        {
            return destinations.TryGetValue((candidate.ChunkX, candidate.ChunkZ), out Vec3d destination)
                ? new RtpColumnSafetyScanResult { Destination = destination }
                : new RtpColumnSafetyScanResult { FailureKind = RtpColumnSafetyFailureKind.UnsafeTerrain, FailureDetail = "fake scanner found no safe destination" };
        }
    }

    private sealed class ResultScanner : IRtpColumnSafetyScanner
    {
        private readonly Queue<RtpColumnSafetyScanResult> results;

        public ResultScanner(params RtpColumnSafetyScanResult[] results)
        {
            this.results = new Queue<RtpColumnSafetyScanResult>(results);
        }

        public RtpColumnSafetyScanResult ScanCandidate(RtpChunkCandidate candidate)
        {
            return results.Dequeue();
        }
    }

    private sealed class FakeCenterResolver : IRtpCenterResolver
    {
        private readonly Vec2d center;

        public FakeCenterResolver(Vec2d center)
        {
            this.center = center;
        }

        public Vec2d Resolve(Vec3d currentPosition)
        {
            return center;
        }
    }

    private sealed class FakeLandClaimAccessor : ILandClaimAccessor
    {
        private readonly System.Func<BlockPos, LandClaimInfo> resolver;

        public FakeLandClaimAccessor(System.Func<BlockPos, LandClaimInfo> resolver)
        {
            this.resolver = resolver;
        }

        public LandClaimInfo GetClaimAt(BlockPos pos)
        {
            return resolver(pos);
        }
    }

    private sealed class FakeWorldCoordinateReader : IWorldCoordinateReader
    {
        private readonly Vec3d position;
        private readonly int? dimension;

        public FakeWorldCoordinateReader(Vec3d position, int? dimension)
        {
            this.position = position;
            this.dimension = dimension;
        }

        public bool PlayerPositionRequested { get; private set; }

        public bool PlayerDimensionRequested { get; private set; }

        public Vec3d GetExactPosition(IServerPlayer player)
        {
            PlayerPositionRequested = true;
            return new Vec3d(position.X, position.Y, position.Z);
        }

        public Vec3d GetExactPosition(Entity entity)
        {
            return new Vec3d(position.X, position.Y, position.Z);
        }

        public BlockPos GetBlockPosition(IServerPlayer player)
        {
            return new BlockPos((int)Math.Floor(position.X), (int)Math.Floor(position.Y), (int)Math.Floor(position.Z), dimension ?? 0);
        }

        public BlockPos GetBlockPosition(Entity entity)
        {
            return new BlockPos((int)Math.Floor(position.X), (int)Math.Floor(position.Y), (int)Math.Floor(position.Z), dimension ?? 0);
        }

        public int? GetDimension(IServerPlayer player)
        {
            PlayerDimensionRequested = true;
            return dimension;
        }

        public int? GetDimension(Entity entity)
        {
            return dimension;
        }
    }

    private class FakeWorldManagerApi : DispatchProxy
    {
        public int MapSizeX { get; set; }

        public int MapSizeZ { get; set; }

        public static IWorldManagerAPI Create(int mapSizeX, int mapSizeZ)
        {
            var proxy = DispatchProxy.Create<IWorldManagerAPI, FakeWorldManagerApi>();
            var proxyState = (FakeWorldManagerApi)(object)proxy;
            proxyState.MapSizeX = mapSizeX;
            proxyState.MapSizeZ = mapSizeZ;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_MapSizeX" => MapSizeX,
                "get_MapSizeZ" => MapSizeZ,
                _ => targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null
            };
        }
    }

    private class ServerPlayerProxy : DispatchProxy
    {
        public Entity? Entity { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_Entity")
            {
                return Entity;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
