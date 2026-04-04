using System;
using System.Collections.Generic;
using FirstStepsTweaks.Config;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpColumnPlanner : IRtpColumnPlanner
    {
        private const int HorizontalChecksPerAttempt = 4;
        private const int LocalSpreadRadius = 4;

        private readonly RtpConfig config;
        private readonly Random random;

        public RtpColumnPlanner(RtpConfig config, Random random = null)
        {
            this.config = config ?? new RtpConfig();
            this.random = random ?? new Random();
        }

        public IReadOnlyList<BlockPos> PlanColumns(double centerX, double centerZ, int dimension)
        {
            int attempts = Math.Max(1, config.MaxAttempts);
            int minRadius = Math.Max(0, config.MinRadius);
            int maxRadius = Math.Max(minRadius, config.MaxRadius);
            var plannedColumns = new List<BlockPos>(attempts * HorizontalChecksPerAttempt);
            var seenColumns = new HashSet<long>();

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                double angle = random.NextDouble() * Math.PI * 2;
                double distance = minRadius + (random.NextDouble() * (maxRadius - minRadius));

                int baseX = (int)Math.Round(centerX + Math.Cos(angle) * distance);
                int baseZ = (int)Math.Round(centerZ + Math.Sin(angle) * distance);

                for (int sample = 0; sample < HorizontalChecksPerAttempt; sample++)
                {
                    int x = baseX + random.Next(-LocalSpreadRadius, LocalSpreadRadius + 1);
                    int z = baseZ + random.Next(-LocalSpreadRadius, LocalSpreadRadius + 1);

                    if (!IsWithinRing(x, z, centerX, centerZ, minRadius, maxRadius))
                    {
                        continue;
                    }

                    long key = MakeColumnKey(x, z);
                    if (!seenColumns.Add(key))
                    {
                        continue;
                    }

                    plannedColumns.Add(new BlockPos(x, 0, z, dimension));
                }
            }

            return plannedColumns;
        }

        private static bool IsWithinRing(int x, int z, double centerX, double centerZ, int minRadius, int maxRadius)
        {
            double dx = (x + 0.5) - centerX;
            double dz = (z + 0.5) - centerZ;
            double distanceSquared = (dx * dx) + (dz * dz);
            double minDistanceSquared = minRadius * (double)minRadius;
            double maxDistanceSquared = maxRadius * (double)maxRadius;
            return distanceSquared >= minDistanceSquared && distanceSquared <= maxDistanceSquared;
        }

        private static long MakeColumnKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }
    }
}
