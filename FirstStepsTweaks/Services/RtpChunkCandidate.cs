using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpChunkCandidate
    {
        public RtpChunkCandidate(int chunkX, int chunkZ, int dimension, IReadOnlyList<Vec2i> sampleOffsets)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Dimension = dimension;
            SampleOffsets = sampleOffsets ?? new Vec2i[0];
        }

        public int ChunkX { get; }

        public int ChunkZ { get; }

        public int Dimension { get; }

        public IReadOnlyList<Vec2i> SampleOffsets { get; }
    }
}
