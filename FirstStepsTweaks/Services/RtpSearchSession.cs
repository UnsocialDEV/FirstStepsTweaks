using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpSearchSession
    {
        public const int DefaultBatchSize = 8;

        public RtpSearchSession(
            Vec2d center,
            Vec3d currentPosition,
            int dimension,
            IReadOnlyList<RtpChunkCandidate> chunkCandidates,
            int batchStartIndex = 0,
            int batchRetryCount = 0,
            int completedBatchCount = 0,
            int pendingChunkCount = 0,
            int unsafeTerrainCount = 0,
            int claimRejectedCount = 0,
            int batchSize = DefaultBatchSize)
        {
            Center = center;
            CurrentPosition = currentPosition;
            Dimension = dimension;
            ChunkCandidates = chunkCandidates ?? Array.Empty<RtpChunkCandidate>();
            BatchStartIndex = Math.Max(0, batchStartIndex);
            BatchRetryCount = Math.Max(0, batchRetryCount);
            CompletedBatchCount = Math.Max(0, completedBatchCount);
            PendingChunkCount = Math.Max(0, pendingChunkCount);
            UnsafeTerrainCount = Math.Max(0, unsafeTerrainCount);
            ClaimRejectedCount = Math.Max(0, claimRejectedCount);
            BatchSize = Math.Max(1, batchSize);
        }

        public Vec2d Center { get; }

        public Vec3d CurrentPosition { get; }

        public int Dimension { get; }

        public IReadOnlyList<RtpChunkCandidate> ChunkCandidates { get; }

        public int BatchStartIndex { get; }

        public int BatchRetryCount { get; }

        public int CompletedBatchCount { get; }

        public int PendingChunkCount { get; }

        public int UnsafeTerrainCount { get; }

        public int ClaimRejectedCount { get; }

        public int BatchSize { get; }

        public int TotalBatchCount => ChunkCandidates.Count == 0
            ? 0
            : (ChunkCandidates.Count + BatchSize - 1) / BatchSize;

        public bool HasCurrentBatch => BatchStartIndex < ChunkCandidates.Count;

        public bool HasNextBatch => BatchStartIndex + BatchSize < ChunkCandidates.Count;

        public IReadOnlyList<RtpChunkCandidate> GetCurrentBatch()
        {
            int remaining = ChunkCandidates.Count - BatchStartIndex;
            if (remaining <= 0)
            {
                return Array.Empty<RtpChunkCandidate>();
            }

            int count = Math.Min(BatchSize, remaining);
            var batch = new RtpChunkCandidate[count];
            for (int index = 0; index < count; index++)
            {
                batch[index] = ChunkCandidates[BatchStartIndex + index];
            }

            return batch;
        }

        public RtpSearchSession AdvanceRetry()
        {
            return new RtpSearchSession(
                Center,
                CurrentPosition,
                Dimension,
                ChunkCandidates,
                BatchStartIndex,
                BatchRetryCount + 1,
                CompletedBatchCount,
                PendingChunkCount,
                UnsafeTerrainCount,
                ClaimRejectedCount,
                BatchSize);
        }

        public RtpSearchSession AdvanceBatch(int pendingChunkCount, int unsafeTerrainCount, int claimRejectedCount)
        {
            return new RtpSearchSession(
                Center,
                CurrentPosition,
                Dimension,
                ChunkCandidates,
                BatchStartIndex + BatchSize,
                batchRetryCount: 0,
                completedBatchCount: CompletedBatchCount + 1,
                pendingChunkCount: PendingChunkCount + Math.Max(0, pendingChunkCount),
                unsafeTerrainCount: UnsafeTerrainCount + Math.Max(0, unsafeTerrainCount),
                claimRejectedCount: ClaimRejectedCount + Math.Max(0, claimRejectedCount),
                batchSize: BatchSize);
        }
    }
}
