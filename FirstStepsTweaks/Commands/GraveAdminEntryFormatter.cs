using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Services;
using System;
using System.Text;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminEntryFormatter
    {
        private readonly IWorldCoordinateDisplayFormatter coordinateDisplayFormatter;

        public GraveAdminEntryFormatter(IWorldCoordinateDisplayFormatter coordinateDisplayFormatter)
        {
            this.coordinateDisplayFormatter = coordinateDisplayFormatter;
        }

        public string Format(GraveData grave, string claimState, int? listIndex = null, double? distanceBlocks = null)
        {
            if (grave == null)
            {
                return string.Empty;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long ageMinutes = Math.Max(0, (now - grave.CreatedUnixMs) / 60000L);
            string displayPosition = coordinateDisplayFormatter.FormatBlockPosition(grave.Dimension, grave.X, grave.Y, grave.Z);
            string worldPosition = $"{grave.Dimension}:{grave.X},{grave.Y},{grave.Z}";
            var builder = new StringBuilder();

            if (listIndex.HasValue)
            {
                builder.AppendLine($"[{listIndex.Value}] graveId={grave.GraveId}");
            }
            else
            {
                builder.AppendLine($"graveId={grave.GraveId}");
            }

            builder.AppendLine($"owner={grave.OwnerName}");
            builder.AppendLine($"ownerUid={grave.OwnerUid}");
            builder.AppendLine($"displayPos={displayPosition}");
            builder.AppendLine($"worldPos={worldPosition}");

            if (distanceBlocks.HasValue)
            {
                builder.AppendLine($"distanceBlocks={Math.Round(distanceBlocks.Value, 1):0.#}");
            }

            builder.AppendLine($"ageMinutes={ageMinutes}");
            builder.Append($"claimState={claimState}");
            return builder.ToString();
        }
    }
}
