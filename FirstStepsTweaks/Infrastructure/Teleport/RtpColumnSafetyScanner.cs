using System;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class RtpColumnSafetyScanner : IRtpColumnSafetyScanner
    {
        private const int MaxVerticalScanDepth = 64;

        private readonly ICoreServerAPI api;

        public RtpColumnSafetyScanner(ICoreServerAPI api)
        {
            this.api = api;
        }

        public RtpColumnSafetyScanResult ScanCandidate(RtpChunkCandidate candidate)
        {
            var result = new RtpColumnSafetyScanResult();
            if (candidate == null || api?.World?.BlockAccessor == null || api.WorldManager == null)
            {
                result.FailureKind = RtpColumnSafetyFailureKind.PendingChunkLoad;
                result.FailureDetail = "scanner unavailable";
                return result;
            }

            EnsureChunkColumnLoaded(candidate);

            IMapChunk mapChunk = ResolveMapChunk(candidate);
            if (candidate.Dimension == 0 && (mapChunk == null || mapChunk.CurrentPass < EnumWorldGenPass.Done))
            {
                result.FailureKind = RtpColumnSafetyFailureKind.PendingChunkLoad;
                result.FailureDetail = $"chunk={candidate.ChunkX},{candidate.ChunkZ}; reason=map chunk not ready";
                return result;
            }

            int? scanStartY = ResolveScanStartY(candidate, mapChunk);
            if (!scanStartY.HasValue)
            {
                result.FailureKind = RtpColumnSafetyFailureKind.PendingChunkLoad;
                result.FailureDetail = $"chunk={candidate.ChunkX},{candidate.ChunkZ}; reason=surface height unavailable";
                return result;
            }

            int scanEndY = Math.Max(2, scanStartY.Value - MaxVerticalScanDepth);
            if (!AreChunkSectionsLoaded(candidate, scanStartY.Value, scanEndY))
            {
                result.FailureKind = RtpColumnSafetyFailureKind.PendingChunkLoad;
                result.FailureDetail = $"chunk={candidate.ChunkX},{candidate.ChunkZ}; reason=chunk sections not loaded";
                return result;
            }

            int chunkSize = api.WorldManager.ChunkSize;
            foreach (Vec2i sampleOffset in candidate.SampleOffsets)
            {
                int worldX = (candidate.ChunkX * chunkSize) + sampleOffset.X;
                int worldZ = (candidate.ChunkZ * chunkSize) + sampleOffset.Y;
                RtpColumnSafetyScanResult sampleResult = FindSafeDestination(
                    worldX,
                    worldZ,
                    candidate.Dimension,
                    scanStartY.Value,
                    scanEndY,
                    pos => IsPassableTeleportSpace(ReadLoadedBlock(pos)),
                    pos => IsSafeTeleportGround(ReadLoadedBlock(pos)));
                if (sampleResult.Success)
                {
                    return sampleResult;
                }
            }

            result.FailureKind = RtpColumnSafetyFailureKind.UnsafeTerrain;
            result.FailureDetail = $"chunk={candidate.ChunkX},{candidate.ChunkZ}; scanRange={scanStartY.Value}..{scanEndY}; samples={candidate.SampleOffsets.Count}";
            return result;
        }

        internal static RtpColumnSafetyScanResult FindSafeDestination(
            int x,
            int z,
            int dimension,
            int scanStartY,
            int scanEndY,
            System.Func<BlockPos, bool> isPassableTeleportSpace,
            System.Func<BlockPos, bool> isSafeTeleportGround)
        {
            var result = new RtpColumnSafetyScanResult();
            for (int y = scanStartY; y >= scanEndY; y--)
            {
                BlockPos feetPos = new BlockPos(x, y, z, dimension);
                BlockPos headPos = feetPos.UpCopy(1);
                BlockPos groundPos = feetPos.DownCopy(1);

                if (!isPassableTeleportSpace(feetPos))
                {
                    continue;
                }

                if (!isPassableTeleportSpace(headPos))
                {
                    continue;
                }

                if (!isSafeTeleportGround(groundPos))
                {
                    continue;
                }

                result.Destination = new Vec3d(x + 0.5, y, z + 0.5);
                return result;
            }

            result.FailureKind = RtpColumnSafetyFailureKind.UnsafeTerrain;
            result.FailureDetail = $"scanRange={scanStartY}..{scanEndY}";
            return result;
        }

        private void EnsureChunkColumnLoaded(RtpChunkCandidate candidate)
        {
            if (candidate.Dimension == 0)
            {
                api.WorldManager.LoadChunkColumnPriority(
                    candidate.ChunkX,
                    candidate.ChunkZ,
                    new ChunkLoadOptions
                    {
                        KeepLoaded = true
                    });
                return;
            }

            api.WorldManager.LoadChunkColumnForDimension(candidate.ChunkX, candidate.ChunkZ, candidate.Dimension);
        }

        private IMapChunk ResolveMapChunk(RtpChunkCandidate candidate)
        {
            return candidate.Dimension == 0
                ? api.WorldManager.GetMapChunk(candidate.ChunkX, candidate.ChunkZ)
                : null;
        }

        private int? ResolveScanStartY(RtpChunkCandidate candidate, IMapChunk mapChunk)
        {
            if (mapChunk != null && mapChunk.YMax > 0)
            {
                return mapChunk.YMax + 2;
            }

            int chunkSize = api.WorldManager.ChunkSize;
            int centerX = (candidate.ChunkX * chunkSize) + (chunkSize / 2);
            int centerZ = (candidate.ChunkZ * chunkSize) + (chunkSize / 2);
            int terrainHeight = api.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(centerX, 0, centerZ, candidate.Dimension));
            return terrainHeight > 0 ? terrainHeight + 2 : (int?)null;
        }

        private bool AreChunkSectionsLoaded(RtpChunkCandidate candidate, int scanStartY, int scanEndY)
        {
            int chunkSize = Math.Max(1, api.WorldManager.ChunkSize);
            int chunkMinY = FloorDiv(Math.Max(0, scanEndY - 1), chunkSize);
            int chunkMaxY = FloorDiv(scanStartY + 1, chunkSize);
            int blockX = candidate.ChunkX * chunkSize;
            int blockZ = candidate.ChunkZ * chunkSize;

            for (int chunkY = chunkMinY; chunkY <= chunkMaxY; chunkY++)
            {
                var probePos = new BlockPos(blockX, chunkY * chunkSize, blockZ, candidate.Dimension);
                if (api.World.BlockAccessor.GetChunkAtBlockPos(probePos) == null)
                {
                    return false;
                }
            }

            return true;
        }

        private Block ReadLoadedBlock(BlockPos pos)
        {
            IWorldChunk chunk = api.World.BlockAccessor.GetChunkAtBlockPos(pos);
            if (chunk == null)
            {
                return null;
            }

            int chunkSize = Math.Max(1, api.WorldManager.ChunkSize);
            int localX = PositiveModulo(pos.X, chunkSize);
            int localY = PositiveModulo(pos.Y, chunkSize);
            int localZ = PositiveModulo(pos.Z, chunkSize);
            return chunk.GetLocalBlockAtBlockPos(api.World, localX, localY, localZ, 0);
        }

        private static bool IsPassableTeleportSpace(Block block)
        {
            return block != null && !IsLiquid(block) && IsCollisionFree(block);
        }

        private static bool IsSafeTeleportGround(Block block)
        {
            return block != null
                && block.BlockId != 0
                && !block.Climbable
                && !IsLiquid(block)
                && block.Replaceable < 6000
                && HasSolidTopSurface(block);
        }

        private static bool IsLiquid(Block block)
        {
            return block.LiquidLevel > 0 || !string.IsNullOrWhiteSpace(block.LiquidCode) || !string.IsNullOrWhiteSpace(block.RemapToLiquidsLayer);
        }

        private static bool IsCollisionFree(Block block)
        {
            Cuboidf[] collisionBoxes = block.CollisionBoxes;
            return collisionBoxes == null || collisionBoxes.Length == 0;
        }

        private static bool HasSolidTopSurface(Block block)
        {
            SmallBoolArray sideSolid = block.SideSolid;
            if (sideSolid[BlockFacing.UP.Index])
            {
                return true;
            }

            Cuboidf[] collisionBoxes = block.CollisionBoxes;
            if (collisionBoxes == null)
            {
                return false;
            }

            foreach (Cuboidf box in collisionBoxes)
            {
                if (box != null && box.Y2 >= 0.99f)
                {
                    return true;
                }
            }

            return false;
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

        private static int PositiveModulo(int value, int modulus)
        {
            int remainder = value % modulus;
            return remainder < 0 ? remainder + modulus : remainder;
        }
    }
}
