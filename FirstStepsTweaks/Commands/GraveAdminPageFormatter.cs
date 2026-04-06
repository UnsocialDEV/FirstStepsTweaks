using FirstStepsTweaks.Infrastructure.Coordinates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminPageFormatter
    {
        public const int PageSize = 5;
        private const string HeaderSeparator = "========================================";
        private const string EntrySeparator = "----------------------------------------";
        private readonly GraveAdminEntryFormatter entryFormatter;

        public GraveAdminPageFormatter(GraveAdminEntryFormatter entryFormatter)
        {
            this.entryFormatter = entryFormatter;
        }

        public bool TryFormat(IReadOnlyList<GraveAdminListEntry> entries, int radius, int page, out string message)
        {
            message = string.Empty;
            IReadOnlyList<GraveAdminListEntry> allEntries = entries ?? Array.Empty<GraveAdminListEntry>();

            if (allEntries.Count == 0)
            {
                message = $"No active gravestones found within {radius} blocks in your current dimension.";
                return true;
            }

            int totalPages = (allEntries.Count + PageSize - 1) / PageSize;
            if (page < 1 || page > totalPages)
            {
                message = $"Page {page} is out of range. Valid pages: 1-{totalPages}.";
                return false;
            }

            int skip = (page - 1) * PageSize;
            IReadOnlyList<GraveAdminListEntry> pageEntries = allEntries.Skip(skip).Take(PageSize).ToList();
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var builder = new StringBuilder();

            builder.AppendLine($"Nearby gravestones ({allEntries.Count}) within {radius} blocks | Page {page}/{totalPages}");
            builder.AppendLine(HeaderSeparator);
            builder.AppendLine("Use /graveadmin restore # [player], /graveadmin remove #, /graveadmin teleport #, or /graveadmin dupeitems # player.");
            builder.AppendLine(HeaderSeparator);

            for (int index = 0; index < pageEntries.Count; index++)
            {
                GraveAdminListEntry entry = pageEntries[index];
                if (entry?.Grave == null)
                {
                    continue;
                }

                if (index > 0)
                {
                    builder.AppendLine(EntrySeparator);
                }

                builder.Append(entryFormatter.Format(entry.Grave, entry.ClaimState, entry.Index, entry.DistanceBlocks));
                builder.AppendLine();
            }

            message = builder.ToString().TrimEnd();
            return true;
        }
    }
}
