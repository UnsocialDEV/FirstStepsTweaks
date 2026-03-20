using System;
using System.Linq;
using FirstStepsTweaks.Services;
using Vintagestory.API.MathTools;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public sealed class LandClaimEscapePlannerTests
    {
        private readonly LandClaimEscapePlanner planner = new LandClaimEscapePlanner();

        [Fact]
        public void PlanColumns_PrefersNearestFaceExit_ForSimpleClaim()
        {
            BlockPos[] columns = planner.PlanColumns(
                new[] { CreateArea(0, 0, 0, 2, 10, 2) },
                2.9,
                1.5,
                5,
                0,
                2).ToArray();

            Assert.NotEmpty(columns);
            Assert.Equal(3, columns[0].X);
            Assert.Equal(1, columns[0].Z);
        }

        [Fact]
        public void PlanColumns_PrefersDiagonalExit_WhenOrthogonalColumnsRemainInsideClaimUnion()
        {
            BlockPos[] columns = planner.PlanColumns(
                new[]
                {
                    CreateArea(0, 0, 0, 0, 10, 1),
                    CreateArea(1, 0, 0, 1, 10, 0)
                },
                0.9,
                0.9,
                5,
                0,
                2).ToArray();

            Assert.NotEmpty(columns);
            Assert.Equal(1, columns[0].X);
            Assert.Equal(1, columns[0].Z);
            Assert.DoesNotContain(columns, pos => pos.X == 1 && pos.Z == 0);
            Assert.DoesNotContain(columns, pos => pos.X == 0 && pos.Z == 1);
        }

        [Fact]
        public void PlanColumns_DedupesColumnsAcrossMultipleAreas_AndKeepsCloserColumnsEarlier()
        {
            BlockPos[] columns = planner.PlanColumns(
                new[]
                {
                    CreateArea(0, 0, 0, 0, 10, 0),
                    CreateArea(2, 0, 0, 2, 10, 0)
                },
                0.2,
                0.2,
                5,
                0,
                2).ToArray();

            Assert.Equal(1, columns.Count(pos => pos.X == 1 && pos.Z == 0));

            int sharedColumnIndex = Array.FindIndex(columns, pos => pos.X == 1 && pos.Z == 0);
            int fartherColumnIndex = Array.FindIndex(columns, pos => pos.X == 3 && pos.Z == 0);

            Assert.True(sharedColumnIndex >= 0);
            Assert.True(fartherColumnIndex > sharedColumnIndex);
        }

        [Fact]
        public void PlanColumns_ExhaustsCurrentShellBeforeExpandingOutward()
        {
            BlockPos[] columns = planner.PlanColumns(
                new[] { CreateArea(0, 0, 0, 0, 10, 0) },
                0.1,
                0.1,
                5,
                0,
                2).ToArray();

            int firstShellTwoIndex = Array.FindIndex(columns, pos => Math.Max(Math.Abs(pos.X), Math.Abs(pos.Z)) == 2);

            Assert.Equal(8, firstShellTwoIndex);
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
    }
}
