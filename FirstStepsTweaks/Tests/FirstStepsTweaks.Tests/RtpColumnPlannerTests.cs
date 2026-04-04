using System;
using System.Linq;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Vintagestory.API.MathTools;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public sealed class RtpColumnPlannerTests
    {
        [Fact]
        public void PlanColumns_ProducesOnlyColumnsWithinConfiguredRing()
        {
            var config = new RtpConfig
            {
                MinRadius = 2500,
                MaxRadius = 5000,
                MaxAttempts = 32
            };
            var planner = new RtpColumnPlanner(config, new Random(1234));

            BlockPos[] columns = planner.PlanColumns(0, 0, 0).ToArray();

            Assert.NotEmpty(columns);
            Assert.All(columns, column =>
            {
                double distance = Math.Sqrt(((column.X + 0.5) * (column.X + 0.5)) + ((column.Z + 0.5) * (column.Z + 0.5)));
                Assert.InRange(distance, config.MinRadius, config.MaxRadius);
            });
        }

        [Fact]
        public void PlanColumns_HonorsNonZeroCenter()
        {
            var config = new RtpConfig
            {
                MinRadius = 25,
                MaxRadius = 40,
                MaxAttempts = 16
            };
            var planner = new RtpColumnPlanner(config, new Random(4321));

            const double centerX = 120.5;
            const double centerZ = -340.5;
            BlockPos[] columns = planner.PlanColumns(centerX, centerZ, 0).ToArray();

            Assert.NotEmpty(columns);
            Assert.All(columns, column =>
            {
                double dx = (column.X + 0.5) - centerX;
                double dz = (column.Z + 0.5) - centerZ;
                double distance = Math.Sqrt((dx * dx) + (dz * dz));
                Assert.InRange(distance, config.MinRadius, config.MaxRadius);
            });
        }
    }
}
