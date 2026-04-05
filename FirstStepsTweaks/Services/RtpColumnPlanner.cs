using System;
using System.Collections.Generic;
using FirstStepsTweaks.Config;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpColumnPlanner : IRtpColumnPlanner
    {
        private readonly RtpConfig config;
        private readonly int chunkSize;
        private readonly Random random;
        private readonly IReadOnlyList<Vec2i> sampleOffsets;

        public RtpColumnPlanner(RtpConfig config, int chunkSize, Random random = null)
        {
            this.config = config ?? new RtpConfig();
            this.chunkSize = Math.Max(1, chunkSize);
            this.random = random ?? new Random();
            sampleOffsets = BuildSampleOffsets(this.chunkSize);
        }

        public IReadOnlyList<RtpChunkCandidate> PlanColumns(double centerX, double centerZ, int dimension)
        {
            int attempts = Math.Max(1, config.MaxAttempts);
            int minRadius = Math.Max(0, config.MinRadius);
            int maxRadius = Math.Max(minRadius, config.MaxRadius);
            var plannedChunks = new List<RtpChunkCandidate>(attempts);
            var seenChunks = new HashSet<long>();
            int guard = Math.Max(attempts * 32, attempts);

            for (int attempt = 0; attempt < guard && plannedChunks.Count < attempts; attempt++)
            {
                double angle = random.NextDouble() * Math.PI * 2;
                double distance = minRadius + (random.NextDouble() * (maxRadius - minRadius));

                int worldX = (int)Math.Round(centerX + Math.Cos(angle) * distance);
                int worldZ = (int)Math.Round(centerZ + Math.Sin(angle) * distance);
                int chunkX = FloorDiv(worldX, chunkSize);
                int chunkZ = FloorDiv(worldZ, chunkSize);

                if (!IsWithinRing(chunkX, chunkZ, centerX, centerZ, minRadius, maxRadius))
                {
                    continue;
                }

                long key = MakeChunkKey(chunkX, chunkZ);
                if (!seenChunks.Add(key))
                {
                    continue;
                }

                plannedChunks.Add(new RtpChunkCandidate(chunkX, chunkZ, dimension, sampleOffsets));
            }

            return plannedChunks;
        }

        private bool IsWithinRing(int chunkX, int chunkZ, double centerX, double centerZ, int minRadius, int maxRadius)
        {
            double sampleX = (chunkX * chunkSize) + (chunkSize / 2d);
            double sampleZ = (chunkZ * chunkSize) + (chunkSize / 2d);
            double dx = sampleX - centerX;
            double dz = sampleZ - centerZ;
            double distanceSquared = (dx * dx) + (dz * dz);
            double minDistanceSquared = minRadius * (double)minRadius;
            double maxDistanceSquared = maxRadius * (double)maxRadius;
            return distanceSquared >= minDistanceSquared && distanceSquared <= maxDistanceSquared;
        }

        private static long MakeChunkKey(int chunkX, int chunkZ)
        {
            return ((long)chunkX << 32) ^ (uint)chunkZ;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
            {
                quotient--;
            }

            return quotient;
        }

        private static IReadOnlyList<Vec2i> BuildSampleOffsets(int chunkSize)
        {
            int center = Math.Min(chunkSize - 1, chunkSize / 2);
            int quarter = Math.Min(chunkSize - 1, Math.Max(0, chunkSize / 4));
            int threeQuarter = Math.Min(chunkSize - 1, Math.Max(0, (chunkSize * 3) / 4));

            return new[]
            {
                new Vec2i(center, center),
                new Vec2i(quarter, quarter),
                new Vec2i(threeQuarter, quarter),
                new Vec2i(quarter, threeQuarter),
                new Vec2i(threeQuarter, threeQuarter)
            };
        }
    }
}
