using System.Reflection;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public sealed class LandClaimEscapeServiceTests
    {
        [Fact]
        public void TryResolveDestination_ReturnsMessage_WhenPlayerIsNotInsideClaim()
        {
            var service = new LandClaimEscapeService(
                new DelegateLandClaimAccessor(_ => LandClaimInfo.None),
                new DelegateTeleportColumnSafetyScanner((_, _, _, _) => null));

            bool result = service.TryResolveDestination(new BlockPos(0, 5, 0, 0), 0.5, 5, 0.5, out Vec3d destination, out string message);

            Assert.False(result);
            Assert.Null(destination);
            Assert.Equal("You are not inside a land claim.", message);
        }

        [Fact]
        public void TryResolveDestination_SkipsSafeSpot_WhenItIsInsideAnotherClaim()
        {
            LandClaimInfo currentClaim = new LandClaimInfo(
                "current",
                "Current",
                "owner-1",
                "Owner One",
                new[] { CreateArea(0, 0, 0, 0, 10, 0) });

            LandClaimInfo blockingClaim = new LandClaimInfo(
                "blocking",
                "Blocking",
                "owner-2",
                "Owner Two",
                new[] { CreateArea(1, 0, 0, 1, 10, 0) });

            var service = new LandClaimEscapeService(
                new DelegateLandClaimAccessor(pos =>
                {
                    if (pos.X == 0 && pos.Z == 0)
                    {
                        return currentClaim;
                    }

                    if (pos.X == 1 && pos.Z == 0)
                    {
                        return blockingClaim;
                    }

                    return LandClaimInfo.None;
                }),
                new DelegateTeleportColumnSafetyScanner((x, z, y, dimension) =>
                {
                    if (x == 1 && z == 0)
                    {
                        return new Vec3d(1.5, y, 0.5);
                    }

                    if (x == 2 && z == 0)
                    {
                        return new Vec3d(2.5, y, 0.5);
                    }

                    return null;
                }));

            bool result = service.TryResolveDestination(new BlockPos(0, 5, 0, 0), 0.9, 5, 0.5, out Vec3d destination, out string message);

            Assert.True(result);
            Assert.NotNull(destination);
            Assert.Equal(2.5, destination.X);
            Assert.Equal(0.5, destination.Z);
            Assert.Equal(string.Empty, message);
        }

        [Fact]
        public void TryResolveDestination_ReturnsFailure_WhenNoSafeDestinationExists()
        {
            LandClaimInfo currentClaim = new LandClaimInfo(
                "current",
                "Current",
                "owner-1",
                "Owner One",
                new[] { CreateArea(0, 0, 0, 0, 10, 0) });

            var service = new LandClaimEscapeService(
                new DelegateLandClaimAccessor(pos => pos.X == 0 && pos.Z == 0 ? currentClaim : LandClaimInfo.None),
                new DelegateTeleportColumnSafetyScanner((_, _, _, _) => null));

            bool result = service.TryResolveDestination(new BlockPos(0, 5, 0, 0), 0.1, 5, 0.1, out Vec3d destination, out string message);

            Assert.False(result);
            Assert.Null(destination);
            Assert.Equal("Unable to find a safe block outside the land claim.", message);
        }

        [Fact]
        public void TryResolveDestination_UsesFallbackRingSearch_WhenClaimGeometryIsMissing()
        {
            LandClaimInfo currentClaim = new LandClaimInfo("current", "Current", "owner-1", "Owner One");

            var service = new LandClaimEscapeService(
                new DelegateLandClaimAccessor(pos => pos.X == 0 && pos.Z == 0 ? currentClaim : LandClaimInfo.None),
                new DelegateTeleportColumnSafetyScanner((x, z, y, dimension) =>
                    x == 1 && z == 0
                        ? new Vec3d(1.5, y, 0.5)
                        : null));

            bool result = service.TryResolveDestination(new BlockPos(0, 5, 0, 0), 0.9, 5, 0.5, out Vec3d destination, out string message);

            Assert.True(result);
            Assert.NotNull(destination);
            Assert.Equal(1.5, destination.X);
            Assert.Equal(0.5, destination.Z);
            Assert.Equal(string.Empty, message);
        }

        [Fact]
        public void TryResolveDestination_PlayerOverload_UsesCoordinateReaderBlockAndExactPosition()
        {
            LandClaimInfo currentClaim = new LandClaimInfo(
                "current",
                "Current",
                "owner-1",
                "Owner One",
                new[] { CreateArea(10, 0, 0, 10, 10, 0) });

            var coordinateReader = new FakeWorldCoordinateReader(
                new Vec3d(10.9, 5.25, 0.5),
                new BlockPos(10, 5, 0, 7),
                7);
            var service = new LandClaimEscapeService(
                new DelegateLandClaimAccessor(pos =>
                    pos.X == 10 && pos.Z == 0 && pos.dimension == 7
                        ? currentClaim
                        : LandClaimInfo.None),
                new DelegateTeleportColumnSafetyScanner((x, z, y, dimension) =>
                    x == 11 && z == 0 && dimension == 7
                        ? new Vec3d(11.5, y, 0.5)
                        : null),
                new LandClaimEscapePlanner(),
                coordinateReader);

            bool result = service.TryResolveDestination(CreatePlayer(), out Vec3d destination, out string message);

            Assert.True(result);
            Assert.NotNull(destination);
            Assert.Equal(11.5, destination.X);
            Assert.Equal(string.Empty, message);
            Assert.True(coordinateReader.PlayerExactPositionRequested);
            Assert.True(coordinateReader.PlayerBlockPositionRequested);
        }

        private static Cuboidi CreateArea(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        {
            return new Cuboidi
            {
                X1 = minX,
                Y1 = minY,
                Z1 = minZ,
                X2 = maxX + 1,
                Y2 = maxY + 1,
                Z2 = maxZ + 1
            };
        }

        private sealed class DelegateLandClaimAccessor : ILandClaimAccessor
        {
            private readonly System.Func<BlockPos, LandClaimInfo> resolver;

            public DelegateLandClaimAccessor(System.Func<BlockPos, LandClaimInfo> resolver)
            {
                this.resolver = resolver;
            }

            public LandClaimInfo GetClaimAt(BlockPos pos)
            {
                return resolver(pos);
            }
        }

        private sealed class DelegateTeleportColumnSafetyScanner : ITeleportColumnSafetyScanner
        {
            private readonly System.Func<int, int, int, int, Vec3d> resolver;

            public DelegateTeleportColumnSafetyScanner(System.Func<int, int, int, int, Vec3d> resolver)
            {
                this.resolver = resolver;
            }

            public Vec3d FindSafeDestination(int x, int z, int referenceY, int dimension)
            {
                return resolver(x, z, referenceY, dimension);
            }
        }

        private static IServerPlayer CreatePlayer()
        {
            return DispatchProxy.Create<IServerPlayer, ServerPlayerProxy>();
        }

        private sealed class FakeWorldCoordinateReader : IWorldCoordinateReader
        {
            private readonly Vec3d exactPosition;
            private readonly BlockPos blockPosition;
            private readonly int? dimension;

            public FakeWorldCoordinateReader(Vec3d exactPosition, BlockPos blockPosition, int? dimension)
            {
                this.exactPosition = exactPosition;
                this.blockPosition = blockPosition;
                this.dimension = dimension;
            }

            public bool PlayerExactPositionRequested { get; private set; }

            public bool PlayerBlockPositionRequested { get; private set; }

            public Vec3d GetExactPosition(IServerPlayer player)
            {
                PlayerExactPositionRequested = true;
                return exactPosition == null ? null : new Vec3d(exactPosition.X, exactPosition.Y, exactPosition.Z);
            }

            public Vec3d GetExactPosition(Vintagestory.API.Common.Entities.Entity entity)
            {
                return exactPosition == null ? null : new Vec3d(exactPosition.X, exactPosition.Y, exactPosition.Z);
            }

            public BlockPos GetBlockPosition(IServerPlayer player)
            {
                PlayerBlockPositionRequested = true;
                return blockPosition?.Copy();
            }

            public BlockPos GetBlockPosition(Vintagestory.API.Common.Entities.Entity entity)
            {
                return blockPosition?.Copy();
            }

            public int? GetDimension(IServerPlayer player)
            {
                return dimension;
            }

            public int? GetDimension(Vintagestory.API.Common.Entities.Entity entity)
            {
                return dimension;
            }
        }

        private class ServerPlayerProxy : DispatchProxy
        {
            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                return targetMethod?.ReturnType.IsValueType == true
                    ? System.Activator.CreateInstance(targetMethod.ReturnType)
                    : null;
            }
        }
    }
}
