using System;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpDestinationResolver : IRtpDestinationResolver
    {
        private readonly IRtpColumnPlanner planner;
        private readonly IRtpColumnSafetyScanner safetyScanner;
        private readonly ILandClaimAccessor landClaimAccessor;
        private readonly IWorldCoordinateReader coordinateReader;
        private readonly IRtpCenterResolver centerResolver;
        private readonly ILogger logger;

        public RtpDestinationResolver(
            RtpConfig config,
            IRtpColumnPlanner planner,
            IRtpColumnSafetyScanner safetyScanner,
            ILandClaimAccessor landClaimAccessor)
            : this(
                config,
                planner,
                safetyScanner,
                landClaimAccessor,
                new WorldCoordinateReader(),
                new RtpCenterResolver(config, (IWorldManagerAPI)null),
                null)
        {
        }

        public RtpDestinationResolver(
            RtpConfig config,
            IRtpColumnPlanner planner,
            IRtpColumnSafetyScanner safetyScanner,
            ILandClaimAccessor landClaimAccessor,
            IWorldCoordinateReader coordinateReader,
            IRtpCenterResolver centerResolver,
            ILogger logger = null)
        {
            var resolvedConfig = config ?? new RtpConfig();
            this.planner = planner;
            this.safetyScanner = safetyScanner;
            this.landClaimAccessor = landClaimAccessor;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
            this.centerResolver = centerResolver ?? new RtpCenterResolver(resolvedConfig, (IWorldManagerAPI)null);
            this.logger = logger;
        }

        public RtpDestinationResolutionResult ResolveDestination(IServerPlayer player, RtpSearchSession searchSession = null)
        {
            var result = new RtpDestinationResolutionResult();
            string playerUid = player?.PlayerUID ?? "<unknown>";

            int? dimension = coordinateReader.GetDimension(player);
            if (!dimension.HasValue || planner == null || safetyScanner == null || centerResolver == null)
            {
                result.FailureReason = "search prerequisites unavailable";
                LogWarning(
                    $"[FirstStepsTweaks][RTP] Failed before search for player '{playerUid}'. " +
                    $"dimension={FormatNullable(dimension)}, plannerReady={planner != null}, scannerReady={safetyScanner != null}, centerResolverReady={centerResolver != null}");
                return result;
            }

            RtpSearchSession session = searchSession ?? CreateSearchSession(player, dimension.Value);
            if (session == null || !session.HasCurrentBatch)
            {
                result.FailureReason = "no RTP chunk candidates available";
                result.SearchSession = session;
                return result;
            }

            result.SearchSession = session;
            foreach (RtpChunkCandidate candidate in session.GetCurrentBatch())
            {
                RtpColumnSafetyScanResult scanResult = safetyScanner.ScanCandidate(candidate);
                if (scanResult?.Success == true)
                {
                    Vec3d safeDestination = scanResult.Destination;
                    if (IsInsideClaim(safeDestination, session.Dimension))
                    {
                        result.ClaimRejectedCount++;
                        continue;
                    }

                    result.Destination = safeDestination;
                    return result;
                }

                switch (scanResult?.FailureKind ?? RtpColumnSafetyFailureKind.UnsafeTerrain)
                {
                    case RtpColumnSafetyFailureKind.PendingChunkLoad:
                        result.PendingChunkCount++;
                        break;
                    case RtpColumnSafetyFailureKind.UnsafeTerrain:
                    default:
                        result.UnsafeTerrainCount++;
                        break;
                }
            }

            return result;
        }

        private RtpSearchSession CreateSearchSession(IServerPlayer player, int dimension)
        {
            Vec3d currentPosition = coordinateReader.GetExactPosition(player);
            Vec2d center = centerResolver.Resolve(currentPosition);
            if (center == null)
            {
                return null;
            }

            return new RtpSearchSession(
                center,
                currentPosition,
                dimension,
                planner.PlanColumns(center.X, center.Y, dimension));
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

        private void LogWarning(string message)
        {
            logger?.Warning(message);
        }

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "<null>";
        }
    }
}
