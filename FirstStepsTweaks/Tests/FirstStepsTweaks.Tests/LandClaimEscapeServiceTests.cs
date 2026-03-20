using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using Vintagestory.API.MathTools;
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
    }
}
