using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Infrastructure.LandClaims
{
    public sealed class LandClaimInfo
    {
        private static readonly IReadOnlyList<Cuboidi> NoAreas = Array.Empty<Cuboidi>();

        public static LandClaimInfo None { get; } = new LandClaimInfo(string.Empty, string.Empty, string.Empty, string.Empty, NoAreas);

        public LandClaimInfo(string key, string claimName, string ownerUid, string ownerName, IEnumerable<Cuboidi> areas = null)
        {
            Key = key ?? string.Empty;
            ClaimName = claimName ?? string.Empty;
            OwnerUid = ownerUid ?? string.Empty;
            OwnerName = ownerName ?? string.Empty;
            Areas = CloneAreas(areas);
        }

        public string Key { get; }
        public string ClaimName { get; }
        public string OwnerUid { get; }
        public string OwnerName { get; }
        public IReadOnlyList<Cuboidi> Areas { get; }
        public bool Exists => !string.IsNullOrWhiteSpace(Key);

        private static IReadOnlyList<Cuboidi> CloneAreas(IEnumerable<Cuboidi> areas)
        {
            if (areas == null)
            {
                return NoAreas;
            }

            var clonedAreas = new List<Cuboidi>();
            foreach (Cuboidi area in areas)
            {
                if (area != null)
                {
                    clonedAreas.Add(area.Clone());
                }
            }

            return clonedAreas.Count == 0 ? NoAreas : clonedAreas.ToArray();
        }
    }
}
