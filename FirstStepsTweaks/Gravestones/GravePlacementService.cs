using System;
using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public sealed class GravePlacementService : IGravePlacementService
    {
        private readonly ICoreServerAPI api;
        private readonly ILandClaimAccessor landClaimAccessor;

        public GravePlacementService(ICoreServerAPI api, ILandClaimAccessor landClaimAccessor)
        {
            this.api = api;
            this.landClaimAccessor = landClaimAccessor;
        }

        public GravePlacementResult FindPlacementPosition(IServerPlayer player, BlockPos deathPos, Block graveBlock)
        {
            if (deathPos == null)
            {
                return new GravePlacementResult(new BlockPos(0), false);
            }

            bool diedInForeignClaim = IsForeignClaimAtPosition(player, deathPos);
            BlockPos fallback = deathPos.UpCopy(1);

            int[] xOffsets = { 0, 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] zOffsets = { 0, 0, 0, 1, -1, 1, -1, 1, -1 };

            if (!diedInForeignClaim)
            {
                for (int yOffset = 0; yOffset <= 3; yOffset++)
                {
                    for (int index = 0; index < xOffsets.Length; index++)
                    {
                        BlockPos candidate = deathPos.AddCopy(xOffsets[index], yOffset, zOffsets[index]);
                        if (CanPlaceGraveAt(player, candidate, graveBlock))
                        {
                            return new GravePlacementResult(candidate, false);
                        }
                    }
                }
            }

            const int searchRadius = 64;
            int[] ySearchOffsets = { 0, 1, 2, 3, -1, -2 };
            for (int radius = 1; radius <= searchRadius; radius++)
            {
                foreach (BlockPos edgePos in EnumerateSquareEdge(deathPos, radius))
                {
                    foreach (int yOffset in ySearchOffsets)
                    {
                        BlockPos candidate = edgePos.AddCopy(0, yOffset, 0);
                        if (!CanPlaceGraveAt(player, candidate, graveBlock))
                        {
                            continue;
                        }

                        bool movedOutsideForeignClaim = diedInForeignClaim && !PositionsEqual(candidate, deathPos);
                        return new GravePlacementResult(candidate, movedOutsideForeignClaim);
                    }
                }
            }

            if (diedInForeignClaim)
            {
                for (int verticalOffset = 1; verticalOffset <= 4; verticalOffset++)
                {
                    BlockPos candidate = deathPos.UpCopy(verticalOffset);
                    if (CanPlaceGraveAt(player, candidate, graveBlock))
                    {
                        return new GravePlacementResult(candidate, false);
                    }
                }
            }

            return new GravePlacementResult(fallback, false);
        }

        public Vec3d FindSafeTeleportTarget(GraveData grave)
        {
            if (grave == null)
            {
                return null;
            }

            BlockPos gravePos = grave.ToBlockPos();
            int[] xOffsets = { 0, 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] zOffsets = { 0, 0, 0, 1, -1, 1, -1, 1, -1 };

            for (int yOffset = 1; yOffset <= 4; yOffset++)
            {
                for (int index = 0; index < xOffsets.Length; index++)
                {
                    BlockPos feetPos = gravePos.AddCopy(xOffsets[index], yOffset, zOffsets[index]);
                    BlockPos headPos = feetPos.UpCopy(1);
                    BlockPos groundPos = feetPos.DownCopy(1);

                    Block feetBlock = api.World.BlockAccessor.GetBlock(feetPos);
                    Block headBlock = api.World.BlockAccessor.GetBlock(headPos);
                    Block groundBlock = api.World.BlockAccessor.GetBlock(groundPos);

                    if (!IsPassableTeleportSpace(feetBlock) || !IsPassableTeleportSpace(headBlock))
                    {
                        continue;
                    }

                    if (!IsSafeTeleportGround(groundBlock))
                    {
                        continue;
                    }

                    return new Vec3d(feetPos.X + 0.5, feetPos.Y, feetPos.Z + 0.5);
                }
            }

            return new Vec3d(gravePos.X + 0.5, gravePos.Y + 1, gravePos.Z + 0.5);
        }

        private bool CanPlaceGraveAt(IServerPlayer player, BlockPos candidate, Block graveBlock)
        {
            if (candidate == null || graveBlock == null)
            {
                return false;
            }

            if (IsForeignClaimAtPosition(player, candidate))
            {
                return false;
            }

            Block existing = api.World.BlockAccessor.GetBlock(candidate);
            return existing == null || existing.IsReplacableBy(graveBlock);
        }

        private bool IsForeignClaimAtPosition(IServerPlayer player, BlockPos pos)
        {
            LandClaimInfo claim = landClaimAccessor.GetClaimAt(pos);
            return claim.Exists
                && (player == null
                    || string.IsNullOrWhiteSpace(claim.OwnerUid)
                    || !string.Equals(claim.OwnerUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<BlockPos> EnumerateSquareEdge(BlockPos center, int radius)
        {
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                yield return new BlockPos(x, center.Y, center.Z - radius, center.dimension);
                yield return new BlockPos(x, center.Y, center.Z + radius, center.dimension);
            }

            for (int z = center.Z - radius + 1; z <= center.Z + radius - 1; z++)
            {
                yield return new BlockPos(center.X - radius, center.Y, z, center.dimension);
                yield return new BlockPos(center.X + radius, center.Y, z, center.dimension);
            }
        }

        private static bool PositionsEqual(BlockPos left, BlockPos right)
        {
            return left != null
                && right != null
                && left.dimension == right.dimension
                && left.X == right.X
                && left.Y == right.Y
                && left.Z == right.Z;
        }

        private static bool IsPassableTeleportSpace(Block block)
        {
            return block != null && (block.BlockId == 0 || block.Replaceable >= 6000);
        }

        private static bool IsSafeTeleportGround(Block block)
        {
            return block != null && block.BlockId != 0 && block.Replaceable < 6000;
        }
    }
}
