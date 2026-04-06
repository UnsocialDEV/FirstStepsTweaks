using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminNearbyQuery
    {
        private readonly IWorldCoordinateReader coordinateReader;

        public GraveAdminNearbyQuery(IWorldCoordinateReader coordinateReader)
        {
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public bool TryQuery(
            IServerPlayer caller,
            IReadOnlyList<GraveData> activeGraves,
            Func<GraveData, bool> isPubliclyClaimable,
            int radius,
            out IReadOnlyList<GraveAdminListEntry> entries,
            out string message)
        {
            entries = Array.Empty<GraveAdminListEntry>();
            message = string.Empty;

            Vec3d currentPosition = coordinateReader.GetExactPosition(caller);
            int? currentDimension = coordinateReader.GetDimension(caller);
            if (currentPosition == null || !currentDimension.HasValue)
            {
                message = "Unable to determine your current position.";
                return false;
            }

            Func<GraveData, bool> claimabilityResolver = isPubliclyClaimable ?? (_ => false);
            GraveData[] graves = activeGraves?
                .Where(grave => grave != null && grave.Dimension == currentDimension.Value)
                .ToArray()
                ?? Array.Empty<GraveData>();

            var nearbyEntries = graves
                .Select(grave => new
                {
                    Grave = grave,
                    DistanceBlocks = currentPosition.DistanceTo(new Vec3d(grave.X, grave.Y, grave.Z))
                })
                .Where(candidate => candidate.DistanceBlocks <= radius)
                .OrderBy(candidate => candidate.DistanceBlocks)
                .ThenBy(candidate => candidate.Grave.CreatedUnixMs)
                .ThenBy(candidate => candidate.Grave.GraveId, StringComparer.OrdinalIgnoreCase)
                .Select((candidate, index) => new GraveAdminListEntry(
                    index + 1,
                    candidate.Grave,
                    candidate.DistanceBlocks,
                    claimabilityResolver(candidate.Grave) ? "public" : "owner-only"))
                .ToList();

            entries = nearbyEntries;
            return true;
        }
    }
}
