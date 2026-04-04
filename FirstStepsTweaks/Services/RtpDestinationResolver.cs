using System;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Teleport;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpDestinationResolver : IRtpDestinationResolver
    {
        private readonly RtpConfig config;
        private readonly IRtpColumnPlanner planner;
        private readonly IRtpColumnSafetyScanner safetyScanner;
        private readonly ILandClaimAccessor landClaimAccessor;
        private readonly IWorldCoordinateReader coordinateReader;

        public RtpDestinationResolver(
            RtpConfig config,
            IRtpColumnPlanner planner,
            IRtpColumnSafetyScanner safetyScanner,
            ILandClaimAccessor landClaimAccessor)
            : this(config, planner, safetyScanner, landClaimAccessor, new WorldCoordinateReader())
        {
        }

        public RtpDestinationResolver(
            RtpConfig config,
            IRtpColumnPlanner planner,
            IRtpColumnSafetyScanner safetyScanner,
            ILandClaimAccessor landClaimAccessor,
            IWorldCoordinateReader coordinateReader)
        {
            this.config = config ?? new RtpConfig();
            this.planner = planner;
            this.safetyScanner = safetyScanner;
            this.landClaimAccessor = landClaimAccessor;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public bool TryResolveDestination(IServerPlayer player, out Vec3d destination)
        {
            destination = null;

            Vec3d currentPosition = coordinateReader.GetExactPosition(player);
            int? dimension = coordinateReader.GetDimension(player);
            if (currentPosition == null || !dimension.HasValue || planner == null || safetyScanner == null)
            {
                return false;
            }

            double centerX = config.UsePlayerPositionAsCenter ? currentPosition.X : 0;
            double centerZ = config.UsePlayerPositionAsCenter ? currentPosition.Z : 0;

            foreach (BlockPos candidate in planner.PlanColumns(centerX, centerZ, dimension.Value))
            {
                Vec3d safeDestination = safetyScanner.FindSafeDestination(candidate.X, candidate.Z, dimension.Value);
                if (safeDestination == null)
                {
                    continue;
                }

                if (IsInsideClaim(safeDestination, dimension.Value))
                {
                    continue;
                }

                destination = safeDestination;
                return true;
            }

            return false;
        }

        private bool IsInsideClaim(Vec3d destination, int dimension)
        {
            var targetPos = new BlockPos(
                (int)Math.Floor(destination.X),
                (int)Math.Floor(destination.Y),
                (int)Math.Floor(destination.Z),
                dimension);
            return (landClaimAccessor?.GetClaimAt(targetPos) ?? LandClaimInfo.None).Exists;
        }
    }
}
