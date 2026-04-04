using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Teleport;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class LandClaimEscapeService
    {
        private const int MaxSearchShells = 64;

        private readonly ILandClaimAccessor landClaimAccessor;
        private readonly ITeleportColumnSafetyScanner safetyScanner;
        private readonly LandClaimEscapePlanner planner;
        private readonly IWorldCoordinateReader coordinateReader;

        public LandClaimEscapeService(ILandClaimAccessor landClaimAccessor, ITeleportColumnSafetyScanner safetyScanner)
            : this(landClaimAccessor, safetyScanner, new LandClaimEscapePlanner(), new WorldCoordinateReader())
        {
        }

        public LandClaimEscapeService(
            ILandClaimAccessor landClaimAccessor,
            ITeleportColumnSafetyScanner safetyScanner,
            LandClaimEscapePlanner planner)
            : this(landClaimAccessor, safetyScanner, planner, new WorldCoordinateReader())
        {
        }

        public LandClaimEscapeService(
            ILandClaimAccessor landClaimAccessor,
            ITeleportColumnSafetyScanner safetyScanner,
            LandClaimEscapePlanner planner,
            IWorldCoordinateReader coordinateReader)
        {
            this.landClaimAccessor = landClaimAccessor;
            this.safetyScanner = safetyScanner;
            this.planner = planner;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public bool TryResolveDestination(IServerPlayer player, out Vec3d destination, out string message)
        {
            destination = null;
            message = "Teleport is only available to in-game players.";

            BlockPos playerPos = coordinateReader.GetBlockPosition(player);
            Vec3d exactPosition = coordinateReader.GetExactPosition(player);
            if (playerPos == null || exactPosition == null)
            {
                return false;
            }

            return TryResolveDestination(
                playerPos,
                exactPosition.X,
                exactPosition.Y,
                exactPosition.Z,
                out destination,
                out message);
        }

        public bool TryResolveDestination(BlockPos playerPos, double playerX, double playerY, double playerZ, out Vec3d destination, out string message)
        {
            destination = null;
            message = "Teleport is only available to in-game players.";

            if (playerPos == null)
            {
                return false;
            }

            LandClaimInfo currentClaim = landClaimAccessor?.GetClaimAt(playerPos) ?? LandClaimInfo.None;
            if (!currentClaim.Exists)
            {
                message = "You are not inside a land claim.";
                return false;
            }

            int referenceY = (int)System.Math.Floor(playerY);
            IReadOnlyList<BlockPos> candidates = currentClaim.Areas.Count > 0
                ? planner.PlanColumns(currentClaim.Areas, playerX, playerZ, referenceY, playerPos.dimension, MaxSearchShells)
                : planner.PlanFallbackColumns(playerX, playerZ, playerPos.dimension, MaxSearchShells);

            foreach (BlockPos candidate in candidates)
            {
                Vec3d safeDestination = safetyScanner?.FindSafeDestination(candidate.X, candidate.Z, referenceY, playerPos.dimension);
                if (safeDestination == null)
                {
                    continue;
                }

                BlockPos targetPos = new BlockPos(candidate.X, (int)System.Math.Floor(safeDestination.Y), candidate.Z, playerPos.dimension);
                LandClaimInfo targetClaim = landClaimAccessor?.GetClaimAt(targetPos) ?? LandClaimInfo.None;
                if (targetClaim.Exists)
                {
                    continue;
                }

                destination = safeDestination;
                message = string.Empty;
                return true;
            }

            message = "Unable to find a safe block outside the land claim.";
            return false;
        }
    }
}
