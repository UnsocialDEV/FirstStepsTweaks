using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminListSnapshot
    {
        public GraveAdminListSnapshot(int radius, IReadOnlyList<GraveAdminListEntry> entries)
        {
            Radius = radius;
            Entries = (entries ?? Array.Empty<GraveAdminListEntry>()).ToList();
        }

        public int Radius { get; }

        public IReadOnlyList<GraveAdminListEntry> Entries { get; }
    }
}
