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
    public void TryResolveDestination_SkipsClaimedSafeDestination_AndReturnsNextUnclaimed()
    {
        var resolver = new RtpDestinationResolver(
            new RtpConfig(),
            new FakePlanner(
                new BlockPos(1, 0, 0, 0),
                new BlockPos(2, 0, 0, 0)),
            new FakeScanner(new Dictionary<(int X, int Z), Vec3d>
            {
                [(1, 0)] = new Vec3d(1.5, 90, 0.5),
                [(2, 0)] = new Vec3d(2.5, 91, 0.5)
            }),
            new FakeLandClaimAccessor(pos =>
                pos.X == 1 && pos.Z == 0
                    ? new LandClaimInfo("claim", "Claimed", "owner", "Owner")
                    : LandClaimInfo.None));

        bool result = resolver.TryResolveDestination(CreatePlayer(0.5, 80, 0.5), out Vec3d destination);

        Assert.True(result);
        Assert.NotNull(destination);
        Assert.Equal(2.5, destination.X);
        Assert.Equal(91, destination.Y);
        Assert.Equal(0.5, destination.Z);
    }

    [Fact]
    public void TryResolveDestination_UsesCoordinateReaderDimension()
    {
        var coordinateReader = new FakeWorldCoordinateReader(new Vec3d(5.5, 80, 6.5), 9);
        var planner = new RecordingPlanner(new BlockPos(2, 0, 0, 9));
        var resolver = new RtpDestinationResolver(
            new RtpConfig { UsePlayerPositionAsCenter = true },
            planner,
            new FakeScanner(new Dictionary<(int X, int Z), Vec3d>
            {
                [(2, 0)] = new Vec3d(2.5, 90, 0.5)
            }),
            new FakeLandClaimAccessor(_ => LandClaimInfo.None),
            coordinateReader);

        bool result = resolver.TryResolveDestination(CreatePlayer(0.5, 80, 0.5), out Vec3d destination);

        Assert.True(result);
        Assert.NotNull(destination);
        Assert.Equal(9, planner.LastDimension);
        Assert.Equal(5.5, planner.LastCenterX);
        Assert.Equal(6.5, planner.LastCenterZ);
        Assert.True(coordinateReader.PlayerPositionRequested);
        Assert.True(coordinateReader.PlayerDimensionRequested);
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
        private readonly IReadOnlyList<BlockPos> columns;

        public FakePlanner(params BlockPos[] columns)
        {
            this.columns = columns;
        }

        public IReadOnlyList<BlockPos> PlanColumns(double centerX, double centerZ, int dimension)
        {
            return columns;
        }
    }

    private sealed class RecordingPlanner : IRtpColumnPlanner
    {
        private readonly IReadOnlyList<BlockPos> columns;

        public RecordingPlanner(params BlockPos[] columns)
        {
            this.columns = columns;
        }

        public double LastCenterX { get; private set; }

        public double LastCenterZ { get; private set; }

        public int LastDimension { get; private set; }

        public IReadOnlyList<BlockPos> PlanColumns(double centerX, double centerZ, int dimension)
        {
            LastCenterX = centerX;
            LastCenterZ = centerZ;
            LastDimension = dimension;
            return columns;
        }
    }

    private sealed class FakeScanner : IRtpColumnSafetyScanner
    {
        private readonly IReadOnlyDictionary<(int X, int Z), Vec3d> destinations;

        public FakeScanner(IReadOnlyDictionary<(int X, int Z), Vec3d> destinations)
        {
            this.destinations = destinations;
        }

        public Vec3d FindSafeDestination(int x, int z, int dimension)
        {
            return destinations.TryGetValue((x, z), out Vec3d destination)
                ? destination
                : null;
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
