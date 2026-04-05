using System;
using System.Linq;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public sealed class RtpColumnPlannerTests
    {
        [Fact]
        public void PlanColumns_ProducesUniqueChunksWithinConfiguredRing()
        {
            var config = new RtpConfig
            {
                MinRadius = 2500,
                MaxRadius = 5000,
                MaxAttempts = 24
            };
            var planner = new RtpColumnPlanner(config, chunkSize: 32, new Random(1234));

            RtpChunkCandidate[] chunks = planner.PlanColumns(0, 0, 0).ToArray();

            Assert.NotEmpty(chunks);
            Assert.Equal(chunks.Length, chunks.Select(chunk => $"{chunk.ChunkX}:{chunk.ChunkZ}").Distinct().Count());
            Assert.All(chunks, chunk =>
            {
                double centerX = (chunk.ChunkX * 32) + 16;
                double centerZ = (chunk.ChunkZ * 32) + 16;
                double distance = Math.Sqrt((centerX * centerX) + (centerZ * centerZ));
                Assert.InRange(distance, config.MinRadius, config.MaxRadius);
            });
        }

        [Fact]
        public void PlanColumns_IncludesFixedChunkSampleOffsets()
        {
            var planner = new RtpColumnPlanner(new RtpConfig { MaxAttempts = 1 }, chunkSize: 32, new Random(4321));

            RtpChunkCandidate candidate = Assert.Single(planner.PlanColumns(0, 0, 0));

            Assert.Equal(5, candidate.SampleOffsets.Count);
            Assert.Contains(candidate.SampleOffsets, offset => offset.X == 16 && offset.Y == 16);
            Assert.Contains(candidate.SampleOffsets, offset => offset.X == 8 && offset.Y == 8);
            Assert.Contains(candidate.SampleOffsets, offset => offset.X == 24 && offset.Y == 8);
            Assert.Contains(candidate.SampleOffsets, offset => offset.X == 8 && offset.Y == 24);
            Assert.Contains(candidate.SampleOffsets, offset => offset.X == 24 && offset.Y == 24);
        }
    }
}
